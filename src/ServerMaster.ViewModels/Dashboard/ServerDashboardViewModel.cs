using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Models;
using System.Diagnostics;
using Avalonia.Threading; // using Avalonia.Threading instead of DataAnnotations

namespace ServerMaster.ViewModels.Dashboard;

public sealed partial class ServerDashboardViewModel : ObservableObject, IAsyncDisposable
{
    public event EventHandler? ReturnToMenuRequested;

    public IServerEngine Engine { get; }
    private readonly INetworkTunnel _tunnel;
    public ServerProfile Profile { get; }
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Stopwatch _uptimeWatch = new();
    private readonly DispatcherTimer _uptimeTimer;

    // ── Profile Identity ─────────────────────────────────────────────────────
    [ObservableProperty] private string _serverName = "Meu Servidor";
    [ObservableProperty] private string _gameVersion = "1.20.1";

    // ── Status ───────────────────────────────────────────────────────────────
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private ServerState _state = ServerState.Idle; // bound in UI to State

    public bool IsRunning => State is ServerState.Running;

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private long _ramMb;
    
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private string _ramUsageLabel = "Calculando...";

    [ObservableProperty] private string _uptimeDisplay = "00:00:00";
    [ObservableProperty] private int _onlinePlayers = 0;

    // ── Tunnel ───────────────────────────────────────────────────────────────
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(TunnelActive))]
    [NotifyPropertyChangedFor(nameof(TunnelStatusString))]
    [NotifyPropertyChangedFor(nameof(TunnelStatusColor))]
    private TunnelState _tunnelState = TunnelState.Disconnected;
    
    public bool TunnelActive => TunnelState is TunnelState.Connected;

    public string TunnelStatusString => TunnelState switch
    {
        TunnelState.Connected => "Conectado",
        TunnelState.Connecting => "Conectando...",
        _ => "Desconectado"
    };

    public string TunnelStatusColor => TunnelState switch
    {
        TunnelState.Connected => "#22C55E",
        TunnelState.Connecting => "#EAB308",
        _ => "#EF4444" 
    };
    [ObservableProperty] private string? _publicAddress;
    [ObservableProperty] private string? _localAddress;
    public bool HasPublicAddress => !string.IsNullOrEmpty(PublicAddress) && PublicAddress != LocalAddress;

    // ── Logs & Console ───────────────────────────────────────────────────────
    public ObservableCollection<LogEntry> Logs { get; } = [];
    public ObservableCollection<LogEntry> FilteredLogs { get; } = [];

    [ObservableProperty] private string _logFilter = string.Empty;
    [ObservableProperty] private string _consoleInput = string.Empty;

    public string GameType => Engine.GameType.ToString();

    public ServerDashboardViewModel(IServerEngine engine, INetworkTunnel tunnel, ServerProfile profile)
    {
        Engine = engine;
        _tunnel = tunnel;
        Profile = profile;
        
        ServerName = profile.Name;
        GameVersion = profile.MinecraftVariant != null ? $"{profile.MinecraftVariant} {profile.GameVersion}" : profile.GameVersion;
        
        LocalAddress = $"127.0.0.1:{profile.Port}";
        if (profile.NetworkMode == Core.Models.NetworkMode.PlayitTunnel) {
            PublicAddress = "Conectando ao playit..."; // Removed "Aguardando túnel..."
        } else if (profile.NetworkMode == Core.Models.NetworkMode.PortForwarded) {
            PublicAddress = "Buscando IP Real...";
            _ = FetchPublicIpAsync(profile.Port);
        } else {
            PublicAddress = "Verificando IP...";
            // Fetch local LAN IPv4 asynchronously to show the loading text briefly
            _ = Task.Run(async () => {
                await Task.Delay(500); // Artificial delay to show the "Verificando" message as requested
                try {
                    var name = System.Net.Dns.GetHostName();
                    var entry = await System.Net.Dns.GetHostEntryAsync(name);
                    var ip = entry.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        PublicAddress = ip != null ? $"{ip}:{profile.Port}" : $"127.0.0.1:{profile.Port}";
                    });
                } catch {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => PublicAddress = $"127.0.0.1:{profile.Port}");
                }
            });
        }
        
        // Start uptime timer rendering
        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (s, e) => UptimeDisplay = _uptimeWatch.IsRunning 
            ? _uptimeWatch.Elapsed.ToString(@"hh\:mm\:ss") 
            : "00:00:00";

        SubscribeToStreams();
    }

    partial void OnLogFilterChanged(string value)
    {
        FilterLogs();
    }

    private void SubscribeToStreams()
    {
        _subscriptions.Add(Engine.LogStream.Subscribe(entry =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Logs.Add(entry);
                if (Logs.Count > 2000) Logs.RemoveAt(0); // cap at 2k lines
                
                // Add to filtered list if it matches
                if (string.IsNullOrWhiteSpace(LogFilter) || 
                    entry.Message.Contains(LogFilter, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredLogs.Add(entry);
                    if (FilteredLogs.Count > 2000) FilteredLogs.RemoveAt(0);
                }
                
                // Naive player join/leave parsing
                if (entry.Message.Contains("joined the game")) OnlinePlayers++;
                else if (entry.Message.Contains("left the game")) OnlinePlayers = Math.Max(0, OnlinePlayers - 1);
            });
        }));

        _subscriptions.Add(Engine.ResourceStream.Subscribe(snap =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CpuPercent = snap.CpuPercent;
                RamMb = snap.RamMb;
                
                // Assuming X mb total max system ram (fallback to 8GB for UI calculation)
                var max = 8192.0; 
                RamPercent = Math.Min(100.0, (RamMb / max) * 100.0);
                RamUsageLabel = $"{RamPercent:F1}% do servidor utilizado";
            });
        }));

        _subscriptions.Add(_tunnel.StatusStream.Subscribe(status =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                TunnelState = status.State;
                if (!string.IsNullOrEmpty(status.PublicAddress))
                {
                    PublicAddress = status.PublicAddress;
                }
                OnPropertyChanged(nameof(HasPublicAddress));
            });
        }));
    }

    private void FilterLogs()
    {
        FilteredLogs.Clear();
        var search = LogFilter?.ToLowerInvariant() ?? "";
        
        foreach (var log in Logs)
        {
            if (string.IsNullOrWhiteSpace(search) || log.Message.ToLowerInvariant().Contains(search))
            {
                FilteredLogs.Add(log);
            }
        }
    }

    private async Task FetchPublicIpAsync(int port)
    {
        try {
            using var http = new System.Net.Http.HttpClient();
            var ip = await http.GetStringAsync("https://api.ipify.org");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PublicAddress = $"{ip.Trim()}:{port}");
        } catch {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PublicAddress = $"192.168.x.x:{port} (Falha IPify)");
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task StartServerAsync()
    {
        State = ServerState.Starting;
        _uptimeWatch.Restart();
        _uptimeTimer.Start();
        
        if (Profile.NetworkMode == Core.Models.NetworkMode.PlayitTunnel)
        {
            // Wire CLI output to the dashboard log console
            if (_tunnel is Core.Services.PlayitTunnelService pts)
            {
                pts.LogCallback = msg =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        var entry = new Core.Models.LogEntry(
                            DateTimeOffset.Now,
                            Core.Models.LogLevel.Information,
                            msg
                        );
                        Logs.Add(entry);
                        FilteredLogs.Add(entry);
                    });
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await _tunnel.StartAsync(Profile.Port);
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var entry = new Core.Models.LogEntry(
                            DateTimeOffset.Now,
                            Core.Models.LogLevel.Error,
                            $"[Erro Playit] {ex.Message}"
                        );
                        Logs.Add(entry);
                        FilteredLogs.Add(entry);
                    });
                }
            });
        }
        
        await Engine.StartAsync();
        State = ServerState.Running;
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        State = ServerState.Stopping;
        await Engine.StopAsync();
        
        if (Profile.NetworkMode == Core.Models.NetworkMode.PlayitTunnel) {
            await _tunnel.StopAsync();
        }

        State = ServerState.Stopped;
        
        _uptimeWatch.Stop();
        _uptimeTimer.Stop();
    }

    [RelayCommand]
    private async Task RestartServerAsync()
    {
        await StopServerAsync();
        await StartServerAsync();
    }

    [RelayCommand]
    private async Task SendConsoleInputAsync()
    {
        if (string.IsNullOrWhiteSpace(ConsoleInput)) return;
        
        await Engine.SendCommandAsync(ConsoleInput);
        
        // Reset input immediately will clear text box but user can just press up arrow later if history implemented
        ConsoleInput = string.Empty; 
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        FilteredLogs.Clear();
    }

    [RelayCommand]
    private void ReturnToMenu()
    {
        ReturnToMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ServerMaster_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        var lines = Logs.Select(l => $"[{l.Timestamp:HH:mm:ss}] [{l.Level}] {l.Message}");
        await File.WriteAllLinesAsync(path, lines);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        await Engine.StopAsync();
        await _tunnel.StopAsync();
        _uptimeTimer.Stop();
    }
}

