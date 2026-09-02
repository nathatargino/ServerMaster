using System.Diagnostics;
using System.Reactive.Subjects;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Models;
using ServerMaster.Core.Services;

namespace ServerMaster.Core.Engines;

/// <summary>
/// Strategy implementation for Minecraft servers.
/// Supports Vanilla, Paper, Purpur, Forge and Fabric variants.
/// </summary>
public sealed class MinecraftServer : IServerEngine, IAsyncDisposable
{
    // Download API base URLs
    private const string PaperApiBase  = "https://api.papermc.io/v2/projects/paper";
    private const string PurpurApiBase = "https://api.purpurmc.org/v2/purpur";

    private readonly ProcessManagerService  _processManager;
    private readonly ResourceMonitorService _resourceMonitor;
    private readonly JavaManagerService     _javaManager;

    private ServerProfile  _profile = null!;
    private Process?       _process;
    private CancellationTokenSource? _logCts;

    private readonly Subject<LogEntry>        _logSubject      = new();
    private readonly Subject<ResourceSnapshot> _resourceSubject = new();

    public GameType    GameType       => GameType.Minecraft;
    public ServerState State { get; private set; } = ServerState.Idle;

    public IObservable<LogEntry>        LogStream      => _logSubject;
    public IObservable<ResourceSnapshot> ResourceStream => _resourceSubject;

    public MinecraftServer(ProcessManagerService processManager, ResourceMonitorService resourceMonitor, JavaManagerService javaManager)
    {
        _processManager  = processManager;
        _resourceMonitor = resourceMonitor;
        _javaManager     = javaManager;

        // Forward resource monitor output to our subject
        _resourceMonitor.ResourceStream.Subscribe(_resourceSubject);
    }

    // ── IServerEngine ────────────────────────────────────────────────────────

    public void Initialize(ServerProfile profile)
    {
        _profile = profile;
    }

    public async Task PrepareAsync(ServerProfile profile, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        _profile = profile;
        State    = ServerState.Preparing;
        Emit(LogLevel.Information, $"[ServerMaster] Preparando servidor Minecraft ({profile.MinecraftVariant} {profile.GameVersion})…");

        Directory.CreateDirectory(profile.ServerDirectory);
        
        // Prevent stale JAR files from bypassing the correct version download
        var existingJar = Path.Combine(profile.ServerDirectory, "server.jar");
        if (File.Exists(existingJar)) File.Delete(existingJar);

        progress?.Report("Baixando servidor…");
        await DownloadServerJarAsync(ct);

        progress?.Report("Escrevendo server.properties…");
        await WriteServerPropertiesAsync(ct);

        progress?.Report("Aceitando EULA…");
        await WriteEulaAsync(ct);

        State = ServerState.Idle;
        Emit(LogLevel.Information, "[ServerMaster] Preparação concluída. Pronto para iniciar.");
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        State = ServerState.Starting;
        Emit(LogLevel.Information, "[ServerMaster] Verificando requisitos do Java…");

        string javaPath;
        try
        {
            javaPath = await _javaManager.EnsureJavaInstalledAsync(_profile.GameVersion, (msg) => Emit(LogLevel.Information, msg), ct);
        }
        catch (Exception ex)
        {
            Emit(LogLevel.Error, $"[ServerMaster] Falha ao configurar o Java: {ex.Message}");
            State = ServerState.Stopped;
            return;
        }

        Emit(LogLevel.Information, "[ServerMaster] Iniciando processo do servidor…");

        var args = BuildJavaArgs();
        
        try
        {
            _process = _processManager.Start(_profile.ServerDirectory, javaPath, args);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Emit(LogLevel.Error, $"[ServerMaster] ERRO CRÍTICO: Não foi possível iniciar o Java. Verifique se o JRE/JDK está instalado e acessível no PATH do Windows. Detalhes: {ex.Message}");
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
            Emit(LogLevel.Warning, "[ServerMaster] Processo encerrado.");
        };
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited) return;
        State = ServerState.Stopping;
        Emit(LogLevel.Information, "[ServerMaster] Enviando comando 'stop'…");
        _processManager.SendCommand(_process, "stop");
        await _processManager.KillAsync(_process, TimeSpan.FromSeconds(30));
        _logCts?.Cancel();
        State = ServerState.Stopped;
    }

    public Task SendCommandAsync(string command, CancellationToken ct = default)
    {
        if (_process is not null)
            _processManager.SendCommand(_process, command);
        return Task.CompletedTask;
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private string BuildJavaArgs()
    {
        var ram = _profile.Resources;
        return $"-Xms{ram.RamMinMb}M -Xmx{ram.RamMb}M " +
               "-XX:+UseG1GC -XX:+ParallelRefProcEnabled " +
               "-XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions " +
               "-jar server.jar nogui";
    }

    private async Task DownloadServerJarAsync(CancellationToken ct)
    {
        var jarPath = Path.Combine(_profile.ServerDirectory, "server.jar");
        if (File.Exists(jarPath))
        {
            Emit(LogLevel.Information, "[ServerMaster] server.jar já existe, pulando download.");
            return;
        }

        var url = _profile.MinecraftVariant switch
        {
            MinecraftVariant.Paper  => await GetPaperBuildUrlAsync(_profile.GameVersion, ct),
            MinecraftVariant.Purpur => $"{PurpurApiBase}/{_profile.GameVersion}/latest/download",
            _                       => await GetVanillaUrlAsync(_profile.GameVersion, ct)
        };

        Emit(LogLevel.Information, $"[ServerMaster] Downloading: {url}");
        using var http  = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(jarPath, bytes, ct);
        Emit(LogLevel.Information, $"[ServerMaster] Download concluído ({bytes.Length / 1024} KB).");
    }

    private static async Task<string> GetPaperBuildUrlAsync(string version, CancellationToken ct)
    {
        using var http = new HttpClient();
        string versionJson;
        try 
        {
            versionJson = await http.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}", ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            throw new InvalidOperationException($"A versão {version} de Minecraft não possui suporte/builds disponíveis no PaperMC. Sugerimos voltar a Escolha do Jogo e selecionar outra versão (ex: 1.21.4 ou 1.21.1) ou mudar o núcleo para Vanilla.");
        }
        
        using var verDoc = System.Text.Json.JsonDocument.Parse(versionJson);
        var buildsElement = verDoc.RootElement.GetProperty("builds");
        int latestBuild = 0;
        
        if (buildsElement.ValueKind == System.Text.Json.JsonValueKind.Array && buildsElement.GetArrayLength() > 0)
        {
            var builds = buildsElement.EnumerateArray().Select(x => x.GetInt32()).ToList();
            latestBuild = builds.Max();
        }
        else
        {
             throw new InvalidOperationException($"Nenhuma build encontrada na API do PaperMC para a versão '{version}'.");
        }

        var buildJson = await http.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{latestBuild}", ct);
        
        using var buildDoc = System.Text.Json.JsonDocument.Parse(buildJson);
        var url = buildDoc.RootElement
            .GetProperty("downloads")
            .GetProperty("application")
            .GetProperty("name")
            .GetString();
        
        if (string.IsNullOrEmpty(url)) throw new InvalidOperationException($"URL de download ausente para {version} build {latestBuild}");
        
        return $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{latestBuild}/downloads/{url}";
    }

    private static async Task<string> GetVanillaUrlAsync(string version, CancellationToken ct)
    {
        // Mojang version manifest
        using var http    = new HttpClient();
        var manifest = await http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);
        var match    = System.Text.RegularExpressions.Regex.Match(
            manifest, $@"""id""\s*:\s*""{System.Text.RegularExpressions.Regex.Escape(version)}"".*?""url""\s*:\s*""([^""]+)""");
        if (!match.Success) throw new InvalidOperationException($"Versão Minecraft '{version}' não encontrada no manifest.");
        var versionMeta = await http.GetStringAsync(match.Groups[1].Value, ct);
        var serverUrl   = System.Text.RegularExpressions.Regex.Match(
            versionMeta, @"""server"".*?""url""\s*:\s*""([^""]+)""").Groups[1].Value;
        
        if(string.IsNullOrEmpty(serverUrl)) throw new InvalidOperationException($"Jar do Vanilla ausente para '{version}'.");
        return serverUrl;
    }

    private async Task WriteServerPropertiesAsync(CancellationToken ct)
    {
        var lines = new List<string>
        {
            $"server-port={_profile.Port}",
            $"motd={_profile.Description}",
            $"max-players={_profile.MaxPlayers}",
            $"gamemode={_profile.GameMode ?? "survival"}",
            $"online-mode={(!_profile.AllowPiratePlayers).ToString().ToLowerInvariant()}",
            "view-distance=10",
            "spawn-protection=16",
            "enable-command-block=false"
        };

        await File.WriteAllLinesAsync(
            Path.Combine(_profile.ServerDirectory, "server.properties"), lines, ct);
    }

    private async Task WriteEulaAsync(CancellationToken ct) =>
        await File.WriteAllTextAsync(
            Path.Combine(_profile.ServerDirectory, "eula.txt"), "eula=true\n", ct);

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
