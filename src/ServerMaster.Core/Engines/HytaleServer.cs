using System.Diagnostics;
using System.IO.Compression;
using System.Reactive.Subjects;
using System.Text.Json;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Models;
using ServerMaster.Core.Services;

namespace ServerMaster.Core.Engines;

/// <summary>
/// Strategy implementation for Hytale dedicated servers.
/// </summary>
public sealed class HytaleServer : IServerEngine, IAsyncDisposable
{
    private readonly ProcessManagerService  _processManager;
    private readonly ResourceMonitorService _resourceMonitor;
    private readonly JavaManagerService     _javaManager;

    private readonly Subject<LogEntry>        _logSubject      = new();
    private readonly Subject<ResourceSnapshot> _resourceSubject = new();

    public GameType    GameType => GameType.Hytale;
    public ServerState State { get; private set; } = ServerState.Idle;

    public IObservable<LogEntry>        LogStream      => _logSubject;
    public IObservable<ResourceSnapshot> ResourceStream => _resourceSubject;
    private ServerProfile  _profile = null!;
    private Process?       _process;
    private CancellationTokenSource? _logCts;

    public HytaleServer(ProcessManagerService processManager, ResourceMonitorService resourceMonitor, JavaManagerService javaManager)
    {
        _processManager  = processManager;
        _resourceMonitor = resourceMonitor;
        _javaManager     = javaManager;
        _resourceMonitor.ResourceStream.Subscribe(_resourceSubject);
    }

    public void Initialize(ServerProfile profile)
    {
        _profile = profile;
    }

    public async Task PrepareAsync(ServerProfile profile, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        _profile = profile;
        State    = ServerState.Preparing;

        try
        {
            Directory.CreateDirectory(_profile.ServerDirectory);

            var packageJsonPath = Path.Combine(_profile.ServerDirectory, "package.json");
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            
            // Determine the jar path based on the selected version
            var runtimesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Runtimes", "Hytale", _profile.GameVersion);
            Directory.CreateDirectory(runtimesDir);
            var hytaleServerJar = Path.Combine(runtimesDir, "HytaleServer.jar");
            
            var hasRealJar = File.Exists(hytaleServerJar);

            if (!hasRealJar)
            {
                // Attempt to download the specific version from our community GitHub mock endpoint
                progress?.Report($"Baixando Hytale Server {_profile.GameVersion}...");
                Emit(LogLevel.Information, $"[ServerMaster] Baixando HytaleServer.jar {_profile.GameVersion} da nuvem comunitária...");
                
                try
                {
                    using var http = new HttpClient();
                    var url = $"https://raw.githubusercontent.com/nathatargino/ServerMaster/main/hytale-bins/{_profile.GameVersion}/HytaleServer.jar";
                    var bytes = await http.GetByteArrayAsync(url, ct);
                    await File.WriteAllBytesAsync(hytaleServerJar, bytes, ct);
                    hasRealJar = true;
                }
                catch
                {
                    Emit(LogLevel.Warning, $"[ServerMaster] Não foi possível baixar a versão {_profile.GameVersion} da nuvem.");
                    
                    // Fallback to local client installation if cloud fails
                    var localHytaleDir = Path.Combine(appDataPath, @"Hytale\install\release\package\game\latest");
                    var localServerDir = Path.Combine(localHytaleDir, "Server");
                    var localAssets = Path.Combine(localHytaleDir, "Assets.zip");
                    
                    var targetServerDir = Path.Combine(_profile.ServerDirectory, "Server");
                    var targetAssets = Path.Combine(_profile.ServerDirectory, "Assets.zip");

                    if (Directory.Exists(localServerDir))
                    {
                        Emit(LogLevel.Information, "[ServerMaster] Copiando estrutura de fallback local do launcher para isolamento...");
                        progress?.Report("Copiando estrutura oficial do Hytale (Server e Assets.zip)...");
                        
                        if (!Directory.Exists(targetServerDir))
                        {
                            CopyDirectory(localServerDir, targetServerDir);
                        }
                        if (File.Exists(localAssets) && !File.Exists(targetAssets))
                        {
                            File.Copy(localAssets, targetAssets, true);
                        }

                        // For local testing without GitHub mock, we just consider it has the jar locally inside ServerDirectory now
                        hasRealJar = true;
                    }
                }
            }

            if (!hasRealJar)
            {
                var serverJsPath = Path.Combine(_profile.ServerDirectory, "server.js");
                
                // Delete old TCP server if it exists to forcefully upgrade to UDP
                var oldPackage = Path.Combine(_profile.ServerDirectory, "package.json");
                if (File.Exists(oldPackage)) File.Delete(oldPackage);
                var oldModules = Path.Combine(_profile.ServerDirectory, "node_modules");
                if (Directory.Exists(oldModules)) Directory.Delete(oldModules, true);
                
                if (!File.Exists(serverJsPath))
                {
                    progress?.Report("Gerando arquitetura Node.js do Hytale Mock (UDP)...");
                    Emit(LogLevel.Information, "[ServerMaster] Construindo ambiente de Rede UDP (Hytale Node Server)...");

                    var serverJsStr = @"const dgram = require('dgram');
const server = dgram.createSocket('udp4');

const port = process.env.PORT || 5520;
let onlinePlayers = 0;

server.on('error', (err) => {
  console.log([Hytale] Server error:
);
  server.close();
});

server.on('message', (msg, rinfo) => {
  console.log([Hytale] Recebido pacote UDP de : ( bytes));
  if (msg.length > 0) {
      const response = Buffer.from('HYTALE_MOCK_ACCEPTED');
      server.send(response, 0, response.length, rinfo.port, rinfo.address);
  }
});

server.on('listening', () => {
    console.log([Hytale] Loading mock world data (Seed: -2294191));
    setTimeout(() => {
        console.log([Hytale] Starting network listener on 0.0.0.0:...);
        setTimeout(() => {
            console.log([Hytale] Server marked as ONLINE. Conecte no IP local.);
        }, 500);
    }, 1000);
});

setInterval(() => {
    console.log('[Hytale] Keep-alive packet broadcasted.');
}, 6000);

server.bind(port);";

                    await File.WriteAllTextAsync(serverJsPath, serverJsStr, ct);
                }
            }
            else
            {
                progress?.Report($"Identificada Engine Oficial Hytale (v{_profile.GameVersion})...");
                Emit(LogLevel.Information, $"[ServerMaster] HytaleServer.jar (v{_profile.GameVersion}) encontrado! Executaremos o backend autêntico através da JVM.");
            }

            // Write eula.txt
            var eulaPath = Path.Combine(_profile.ServerDirectory, "eula.txt");
            if (!File.Exists(eulaPath))
            {
                await File.WriteAllTextAsync(eulaPath, "eula=true", ct);
            }

            // Write server.json
            var jsonPath = Path.Combine(_profile.ServerDirectory, "server.json");
            var serverConfig = new
            {
                serverName = _profile.Name,
                port = _profile.Port,
                maxPlayers = 50,
                motd = _profile.Description,
                requireToken = false
            };
            
            var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(serverConfig, jsonOpts);
            await File.WriteAllTextAsync(jsonPath, jsonString, ct);

            State = ServerState.Stopped;
            progress?.Report("Servidor Hytale pronto.");
        }
        catch (Exception ex)
        {
            Emit(LogLevel.Error, $"[ServerMaster] Erro ao preparar servidor Hytale: {ex.Message}");
            State = ServerState.Stopped;
            throw;
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_profile == null) throw new InvalidOperationException("O servidor nÃ£o foi preparado.");

        State = ServerState.Starting;
        var p = _profile;

        try
        {
            Emit(LogLevel.Information, "[ServerMaster] Preparando inicialização da Engine...");

            var hytaleServerJar = Path.Combine(p.ServerDirectory, "Server", "HytaleServer.jar");
            var hasRealJar = File.Exists(hytaleServerJar);
            
            if (!hasRealJar)
            {
                // Verify if it's in the runtimes folder (for old prepared servers)
                var runtimesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Runtimes", "Hytale", p.GameVersion ?? "");
                var runtimesJar = Path.Combine(runtimesDir, "HytaleServer.jar");
                if (File.Exists(runtimesJar))
                {
                    hytaleServerJar = runtimesJar;
                    hasRealJar = true;
                    Emit(LogLevel.Warning, "[ServerMaster] Aviso: Usando estrutura sem isolamento (Runtimes). Clique em Preparar novamente ou crie um novo servidor para usar a nova arquitetura isolada!");
                }
            }

            if (!hasRealJar)
            {
                // Fallback to local launcher installation for legacy servers that weren't "prepared"
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var fallbackJar = Path.Combine(appDataPath, @"Hytale\install\release\package\game\latest\Server\HytaleServer.jar");
                if (File.Exists(fallbackJar))
                {
                    hytaleServerJar = fallbackJar;
                    hasRealJar = true;
                    Emit(LogLevel.Warning, "[ServerMaster] Aviso: Usando engine global do launcher. Clique em Preparar para aplicar a nova arquitetura isolada!");
                }
            }

        var cmd = "node";
        if (hasRealJar)
        {
            cmd = await _javaManager.EnsureJavaInstalledAsync(25, (msg) => Emit(LogLevel.Information, msg), ct);
        }

        var ram = p.Resources;
        var args = hasRealJar 
            ? $"-Xms{ram.RamMinMb}M -Xmx{ram.RamMb}M -XX:+UseG1GC -jar \"{hytaleServerJar}\" --assets \"{Path.Combine(Path.GetDirectoryName(hytaleServerJar), "..", "Assets.zip")}\" --bind 0.0.0.0:{p.Port} --auth-mode AUTHENTICATED --backup --backup-dir backups --backup-frequency 30"
            : "server.js";

        var isIsolated = hytaleServerJar.StartsWith(p.ServerDirectory, StringComparison.OrdinalIgnoreCase);
        var execContext = isIsolated ? p.ServerDirectory : Path.GetDirectoryName(hytaleServerJar);
        
        if (isIsolated)
        {
            args = $"-Xms{ram.RamMinMb}M -Xmx{ram.RamMb}M -XX:+UseG1GC -jar \"Server/HytaleServer.jar\" --assets Assets.zip --bind 0.0.0.0:{p.Port} --auth-mode AUTHENTICATED --backup --backup-dir backups --backup-frequency 30";
        }

        try
        {
            if (hasRealJar) 
                Emit(LogLevel.Information, $"[ServerMaster] Iniciando Engine Hytale (AppData) na porta {p.Port}...");
            else
                Environment.SetEnvironmentVariable("PORT", p.Port.ToString(), EnvironmentVariableTarget.Process);
                
            _process = _processManager.Start(execContext, cmd, args);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Emit(LogLevel.Error, $"[ServerMaster] ERRO CRÃTICO: NÃ£o foi possÃ­vel iniciar o executÃ¡vel '{cmd}'. " + 
                                 "Ele estÃ¡ acessÃ­vel no PATH do Windows? Detalhes: " + ex.Message);
            State = ServerState.Stopped;
            return;
        }

            _logCts  = new CancellationTokenSource();
            _ = CaptureStreamAsync(_process.StandardOutput, LogLevel.Information, _logCts.Token);
            _ = CaptureStreamAsync(_process.StandardError,  LogLevel.Warning,     _logCts.Token);

            _resourceMonitor.Start(_process);
            State = ServerState.Running;
            
            _process.EnableRaisingEvents = true;
            _process.Exited += (_, _) =>
            {
                _resourceMonitor.Stop();
                State = ServerState.Stopped;
                Emit(LogLevel.Warning, "[ServerMaster] O Processo do Hytale foi encerrado.");
            };

            Emit(LogLevel.Information, "[ServerMaster] Servidor Hytale lanÃ§ado com sucesso!");
        }
        catch (Exception ex)
        {
            State = ServerState.Stopped;
            Emit(LogLevel.Error, $"[ServerMaster] Falha ao iniciar o Hytale: {ex.Message}");
            throw;
        }
    }



    private async Task CaptureStreamAsync(StreamReader reader, LogLevel level, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                
                Emit(level, line);

                if (line.Contains("Use /auth login to authenticate"))
                {
                    Emit(LogLevel.Information, "[ServerMaster] Solicitando login de dispositivo para a Engine...");
                    _ = SendCommandAsync("/auth login device", ct);
                }
                
                // Device Auth Interception
                if (line.Contains("https://oauth.accounts.hytale.com/oauth2/device/verify?user_code="))
                {
                    try
                    {
                        var urlMatch = System.Text.RegularExpressions.Regex.Match(line, @"https://oauth\.accounts\.hytale\.com/oauth2/device/verify\?user_code=([A-Za-z0-9]+)");
                        if (urlMatch.Success)
                        {
                            var url = urlMatch.Value;
                            var codeSpan = urlMatch.Groups[1].Value;
                            
                            Emit(LogLevel.Information, $"[ServerMaster] =========================================");
                            Emit(LogLevel.Information, $"[ServerMaster] 🔑 AUTENTICAÇÃO REQUERIDA!");
                            Emit(LogLevel.Information, $"[ServerMaster] 🔑 Seu código é: {codeSpan}");
                            Emit(LogLevel.Information, $"[ServerMaster] 🔑 O navegador foi aberto para autorizar seu servidor automaticamente!");
                            Emit(LogLevel.Information, $"[ServerMaster] =========================================");
                            
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "cmd",
                                Arguments = $"/c start \"\" \"{url}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Emit(LogLevel.Warning, $"[ServerMaster] Não foi possível abrir o navegador: {ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (State != ServerState.Running) return;
        
        State = ServerState.Stopping;
        Emit(LogLevel.Information, "[ServerMaster] Enviando comando 'stop'â€¦");
        
        if (_process is not null && !_process.HasExited)
        {
            _processManager.SendCommand(_process, "process.exit()");
            await _processManager.KillAsync(_process, TimeSpan.FromSeconds(30));
            _logCts?.Cancel();
        }
        State = ServerState.Stopped;
    }

    public Task SendCommandAsync(string command, CancellationToken ct = default)
    {
        if (_process is not null)
        {
            Emit(LogLevel.Chat, $"[Console] {command}");
            _processManager.SendCommand(_process, command);
        }
        return Task.CompletedTask;
    }

    private void Emit(LogLevel level, string message) =>
        _logSubject.OnNext(new LogEntry(DateTimeOffset.Now, level, message));

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _logSubject.Dispose();
        _resourceSubject.Dispose();
        _resourceMonitor.Dispose();
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
    }
}


