using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ServerMaster.ViewModels.Wizard;

/// <summary>
/// Step 4 – Network mode: Playit.gg public tunnel vs LAN only.
/// </summary>
public sealed partial class Step4NetworkViewModel : ObservableObject
{
    private readonly WizardHostViewModel _host;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsLanOnly))]
    [NotifyPropertyChangedFor(nameof(IsPortForwarded))]
    [NotifyPropertyChangedFor(nameof(IsPlayitTunnel))]
    private Core.Models.NetworkMode _networkMode = Core.Models.NetworkMode.PlayitTunnel;

    public bool IsLanOnly
    {
        get => NetworkMode == Core.Models.NetworkMode.LanOnly;
        set { if (value) NetworkMode = Core.Models.NetworkMode.LanOnly; }
    }

    public bool IsPortForwarded
    {
        get => NetworkMode == Core.Models.NetworkMode.PortForwarded;
        set { if (value) NetworkMode = Core.Models.NetworkMode.PortForwarded; }
    }

    public bool IsPlayitTunnel
    {
        get => NetworkMode == Core.Models.NetworkMode.PlayitTunnel;
        set { if (value) NetworkMode = Core.Models.NetworkMode.PlayitTunnel; }
    }
    [ObservableProperty] private int  _serverPort = 25565;
    [ObservableProperty] private int  _maxPlayers = 20;
    [ObservableProperty] private bool _allowPiratePlayers = false;

    public Step4NetworkViewModel(WizardHostViewModel host) => _host = host;

    public void OnNavigatedTo()
    {
        // Set default port based on selected game if user hasn't changed it manually
        // We assume 25565 is the starting default for Minecraft and 5520 for Hytale
        if (_host.SelectedGame == Core.Models.GameType.Hytale && ServerPort == 25565)
            ServerPort = 5520;
        else if (_host.SelectedGame == Core.Models.GameType.Minecraft && ServerPort == 5520)
            ServerPort = 25565;
            
        OnPropertyChanged(nameof(IsMinecraft));
    }

    public bool IsMinecraft => _host.SelectedGame == Core.Models.GameType.Minecraft;

    [RelayCommand]
    private void Next()
    {
        _host.SetNetwork(NetworkMode, ServerPort, AllowPiratePlayers, MaxPlayers);
        _host.Next();
    }

    [RelayCommand]
    private void Back() => _host.Back();
}
