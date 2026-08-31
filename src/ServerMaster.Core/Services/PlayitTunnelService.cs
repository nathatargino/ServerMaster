using System.Diagnostics;
using System.Reactive.Subjects;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Models;

namespace ServerMaster.Core.Services;

/// <summary>
/// Integrates with the Playit.gg CLI to expose a local port as a public TCP/UDP address.
/// Auto-downloads the CLI binary if not present.
/// StartAsync launches the CLI process and returns immediately.
/// The StatusStream updates whenever the CLI reports a connected address.
/// </summary>
public sealed class PlayitTunnelService : INetworkTunnel, IAsyncDisposable
{
    // Playit.gg CLI download URLs (Windows x64)
    private const string CliDownloadUrl = "https://github.com/playit-cloud/playit-agent/releases/download/v0.15.13/playit-windows-x86_64.exe";
    private const string CliBinaryName  = "playit.exe";
    private string SecretPath => _profile == null 
        ? throw new InvalidOperationException("Tunnel not initialized!") 
        : Path.Combine(AppContext.BaseDirectory, $"playit-secret-{_profile.Id}.toml");

    private ServerProfile? _profile;
    private readonly BehaviorSubject<TunnelStatus> _statusSubject = new(new TunnelStatus(TunnelState.Disconnected));
    private Process? _tunnelProcess;

    public void Initialize(ServerProfile profile)
    {
        _profile = profile;
    }

    /// <summary>Streams <see cref="TunnelStatus"/> updates; never completes during the app lifetime.</summary>
    public IObservable<TunnelStatus> StatusStream => _statusSubject;

    /// <summary>Optional callback to pipe CLI output lines to the server log console.</summary>
    public Action<string>? LogCallback { get; set; }

    public async Task<TunnelInfo> StartAsync(int localPort, CancellationToken ct = default)
    {
        await EnsureCliPresentAsync(ct);

        _statusSubject.OnNext(new TunnelStatus(TunnelState.Connecting, "Iniciando túnel Playit..."));
        Log("[Playit] Iniciando processo CLI...");

        var psi = new ProcessStartInfo
        {
            FileName               = GetCliPath(),
            Arguments              = $"-s --secret_path \"{SecretPath}\"",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = AppContext.BaseDirectory
        };

        _tunnelProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _tunnelProcess.OutputDataReceived += (_, e) => ParseOutput(e.Data);
        _tunnelProcess.ErrorDataReceived  += (_, e) => ParseOutput(e.Data);
        _tunnelProcess.Exited += (_, _) =>
        {
            Log("[Playit] Processo encerrado.");
            _statusSubject.OnNext(new TunnelStatus(TunnelState.Disconnected));
        };

        _tunnelProcess.Start();
        _tunnelProcess.BeginOutputReadLine();
        _tunnelProcess.BeginErrorReadLine();

        Log($"[Playit] Processo PID={_tunnelProcess.Id} iniciado. Aguardando endereço público...");

        // Return immediately — dashboard subscribes to StatusStream for address updates
        return new TunnelInfo("pending", localPort, "tcp");
    }

    private void Log(string message)
    {
        LogCallback?.Invoke(message);
    }

    private void ParseOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        // Strip ANSI escape codes
        var clean = System.Text.RegularExpressions.Regex.Replace(
            line, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", "").Trim();

        if (string.IsNullOrWhiteSpace(clean)) return;

        // Always log raw CLI output so the user can see it in the console
        Log($"[Playit] {clean}");

        // ── Auto-open browser for initial agent-claim workflow ────────────────
        if (clean.Contains("playit.gg/claim/"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                clean, @"https://playit\.gg/claim/[A-Za-z0-9\-]+");
            if (m.Success)
            {
                Log("[Playit] Abrindo link de vinculação no navegador...");
                Process.Start(new ProcessStartInfo(m.Value) { UseShellExecute = true });
                _statusSubject.OnNext(new TunnelStatus(TunnelState.Connecting, "Aguardando vinculo no navegador..."));
            }
            return; // don't try to parse a URL as an address
        }

        // Detect when tunnels are ready and mapped
        var match = System.Text.RegularExpressions.Regex.Match(clean, @"(\d+) tunnels registered");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var tunnels) && tunnels > 0)
        {
            if (_statusSubject.Value.State != TunnelState.Connected)
            {
                _ = FetchPlayitAddressAsync();
            }
            return;
        }

        // Detect "connected" keywords even without an address yet
        if (System.Text.RegularExpressions.Regex.IsMatch(
                clean, @"(?i)(connected|running|ready|established|authenticated)"))
        {
            if (_statusSubject.Value.State != TunnelState.Connected)
                _statusSubject.OnNext(new TunnelStatus(TunnelState.Connecting, "Conectando ao playit..."));
        }
    }

    private async Task FetchPlayitAddressAsync()
    {
        try 
        {
            await Task.Delay(1500); // Give playit a moment to retrieve the allocation details

            var psi = new ProcessStartInfo
            {
                FileName = GetCliPath(),
                // DO NOT USE "-s" otherwise INFO logs are piped into stdout string breaking JSON serialization
                Arguments = $"--secret_path \"{SecretPath}\" tunnels list",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            
            using var proc = Process.Start(psi);
            if (proc == null) return;
            var json = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tunnels", out var tunnels)) return;
            
            foreach (var t in tunnels.EnumerateArray())
            {
                if (t.TryGetProperty("active", out var isActive) && isActive.GetBoolean())
                {
                    if (t.TryGetProperty("alloc", out var allocNode) && allocNode.TryGetProperty("data", out var alloc))
                    {
                        string? display = null;
                        
                        if (alloc.TryGetProperty("assigned_domain", out var ad) && ad.ValueKind == System.Text.Json.JsonValueKind.String)
                            display = ad.GetString();
                        else if (alloc.TryGetProperty("ip_hostname", out var ipHost) && alloc.TryGetProperty("port_start", out var ps))
                            display = $"{ipHost.GetString()}:{ps.GetInt32()}";
                        
                        if (!string.IsNullOrEmpty(display))
                        {
                            Log($"[Playit] ✅ Túnel extraído do Payload JSON: {display}");
                            
                            // Re-marshal to UI safely via Next()
                            _statusSubject.OnNext(new TunnelStatus(TunnelState.Connected, display));
                            return;
                        }
                    }
                }
            }
            Log("[Playit] Falha: 'tunnels list' não retornou nenhum túnel ativo no JSON.");
        }
        catch (Exception ex)
        {
            Log($"[Playit] Exceção ao extrair endereço via tunnels list: {ex.Message}");
        }
    }

    private static string GetCliPath() =>
        Path.Combine(AppContext.BaseDirectory, "tools", CliBinaryName);

    private static async Task EnsureCliPresentAsync(CancellationToken ct)
    {
        var cliPath = GetCliPath();
        if (File.Exists(cliPath)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(cliPath)!);
        using var http = new HttpClient();
        var data = await http.GetByteArrayAsync(CliDownloadUrl, ct);
        await File.WriteAllBytesAsync(cliPath, data, ct);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (_tunnelProcess is { HasExited: false })
            _tunnelProcess.Kill(entireProcessTree: true);

        _statusSubject.OnNext(new TunnelStatus(TunnelState.Disconnected));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _statusSubject.Dispose();
    }



    public static async Task ClaimAgentNativeAsync(string profileId, Action<string>? onStatusUpdate = null)
    {
        onStatusUpdate?.Invoke("Verificando CLI do Playit.gg...");
        await EnsureCliPresentAsync(default);

        var secretPath = Path.Combine(AppContext.BaseDirectory, $"playit-secret-{profileId}.toml");
        
        onStatusUpdate?.Invoke("Iniciando processo de claim do agente...");
        
        var psi = new ProcessStartInfo
        {
            FileName = GetCliPath(),
            Arguments = $"-s --secret_path \"{secretPath}\"",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Falha ao iniciar o CLI do Playit para vinculação.");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)); // Give user 3 minutes to accept on browser
        var tcs = new TaskCompletionSource<bool>();
        bool openedTarget = false;

        void HandleLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", "").Trim();
            if (string.IsNullOrWhiteSpace(line)) return;

            if (line.Contains("playit.gg/claim/"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"https://playit\.gg/claim/[A-Za-z0-9\-]+");
                if (m.Success && !openedTarget)
                {
                    openedTarget = true;
                    onStatusUpdate?.Invoke("URL gerada! Abra seu navegador...");
                    Process.Start(new ProcessStartInfo(m.Value) { UseShellExecute = true });
                    onStatusUpdate?.Invoke("Aguardando aprovação no site da Playit...");
                }
            }
            
            // Success indicators
            if (line.Contains("tunnel running") || line.Contains("agent ready") || line.Contains("connected") || line.Contains("tunnels registered"))
            {
                tcs.TrySetResult(true);
            }
            
            // Error handling
            if (line.Contains("Error:", StringComparison.OrdinalIgnoreCase) || line.Contains("Fail", StringComparison.OrdinalIgnoreCase))
            {
                // Ignore benign flush errors and non-fatal network reachability issues (e.g. IPv6 ping failures)
                if (!line.Contains("Failed to write", StringComparison.OrdinalIgnoreCase) && 
                    !line.Contains("Failed to flush", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("failed to send initial ping", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("NetworkUnreachable", StringComparison.OrdinalIgnoreCase))
                {
                    // For now, we just log these. Playit's process exit will handle actual fatal crashes.
                    onStatusUpdate?.Invoke($"Aviso Playit: {line}");
                }
            }
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);
        process.Exited += (_, _) => {
            if (!tcs.Task.IsCompleted) tcs.TrySetException(new InvalidOperationException("Processo encerrou antes de concluir a vinculação."));
        };
        
        process.EnableRaisingEvents = true;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await using (cts.Token.Register(() => tcs.TrySetException(new TimeoutException("Tempo esgotado ao aguardar o usuário aprovar no navegador."))))
            {
                await tcs.Task;
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
    }
}
