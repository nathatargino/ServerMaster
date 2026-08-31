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

    private readonly Subject<LogEntry>        _logSubject      = new();
    private readonly Subject<ResourceSnapshot> _resourceSubject = new();

    public GameType    GameType => GameType.Hytale;
    public ServerState State { get; private set; } = ServerState.Idle;

    public IObservable<LogEntry>        LogStream      => _logSubject;
    public IObservable<ResourceSnapshot> ResourceStream => _resourceSubject;
    private ServerProfile  _profile = null!;
    private Process?       _process;
    private CancellationTokenSource? _logCts;
    private bool           _isMockMode;

    public HytaleServer(ProcessManagerService processManager, ResourceMonitorService resourceMonitor)
    {
        _processManager  = processManager;
        _resourceMonitor = resourceMonitor;
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
            var hytalePath = Path.Combine(appDataPath, @"Hytale\install\release\package\game\latest");
            var hytaleServerPath = Path.Combine(hytalePath, "Server");
            
            var hasRealJar = File.Exists(Path.Combine(hytaleServerPath, "HytaleServer.jar"));

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

                    _isMockMode = true;

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
                progress?.Report("Identificada Engine Oficial Hytale (Java)...");
                Emit(LogLevel.Information, "[ServerMaster] HytaleServer.jar encontrado! Executaremos o backend autÃªntico atravÃ©s da JVM.");
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

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_profile == null) throw new InvalidOperationException("O servidor nÃ£o foi preparado.");

        State = ServerState.Starting;
        var p = _profile;

        try
        {
            Emit(LogLevel.Information, "[ServerMaster] Preparando inicializaÃ§Ã£o da Engine (Node.js Hytale Mock)...");

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var hytalePath = Path.Combine(appDataPath, @"Hytale\install\release\package\game\latest");
        var hytaleServerPath = Path.Combine(hytalePath, "Server");
        
        var hasRealJar = File.Exists(Path.Combine(hytaleServerPath, "HytaleServer.jar"));

        var cmd = hasRealJar ? "java" : "node";
        var ram = p.Resources;
        var args = hasRealJar 
            ? $"-Xms{ram.RamMinMb}M -Xmx{ram.RamMb}M -XX:+UseG1GC -jar HytaleServer.jar --assets ../Assets.zip --bind 0.0.0.0:{p.Port} --backup --backup-dir backups --backup-frequency 30"
            : "server.js";

        var execContext = hasRealJar ? hytaleServerPath : p.ServerDirectory;

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
            return Task.CompletedTask;
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

        return Task.CompletedTask;
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
}


