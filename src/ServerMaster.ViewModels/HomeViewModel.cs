using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Models;
using ServerMaster.Core.Services;
using ServerMaster.ViewModels.Dashboard;
using ServerMaster.ViewModels.Services;
using ServerMaster.ViewModels.Wizard;

namespace ServerMaster.ViewModels;

public enum ServerAutoAction
{
    None,
    Start,
    Stop
}

public class OpenServerEventArgs : System.EventArgs
{
    public ServerProfile Profile { get; }
    public ServerAutoAction AutoAction { get; }

    public OpenServerEventArgs(ServerProfile profile, ServerAutoAction autoAction)
    {
        Profile = profile;
        AutoAction = autoAction;
    }
}

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly AppSettingsService _settings;
    private readonly ServerRepository _repository;
    private readonly SessionManager _sessions;
    public event System.EventHandler? CreateServerRequested;
    public event System.EventHandler<ServerProfile>? EditServerRequested;
    public event System.EventHandler<OpenServerEventArgs>? OpenServerRequested;

    [ObservableProperty] private string _greeting = string.Empty;
    [ObservableProperty] private ObservableCollection<HomeServerItemViewModel> _servers = [];
    [ObservableProperty] private ObservableCollection<ServerDashboardViewModel> _activeTunnels = [];

    public HomeViewModel(AppSettingsService settings, ServerRepository repository, SessionManager sessions)
    {
        _settings = settings;
        _repository = repository;
        _sessions = sessions;
        
        Greeting = $"Olá, {_settings.Current.Username}!";
        _sessions.SessionChanged += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateActiveTunnels);
        
        LoadServers(); // Fix: Load servers on app startup!
    }

    private void UpdateActiveTunnels()
    {
        // Unsubscribe old sessions
        foreach (var old in ActiveTunnels)
            old.PropertyChanged -= OnSessionPropertyChanged;

        ActiveTunnels.Clear();
        foreach (var vm in _sessions.GetActiveSessions())
        {
            if (vm.Profile.NetworkMode == Core.Models.NetworkMode.PlayitTunnel)
            {
                ActiveTunnels.Add(vm);
                vm.PropertyChanged += OnSessionPropertyChanged;
            }
        }
    }

    public IEnumerable<ServerDashboardViewModel> VisibleActiveTunnels => ActiveTunnels.Where(t => t.TunnelActive);
    public bool HasVisibleTunnels => VisibleActiveTunnels.Any();

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When PublicAddress or TunnelState changes, force the list to refresh binding
        if (e.PropertyName is nameof(ServerMaster.ViewModels.Dashboard.ServerDashboardViewModel.PublicAddress)
            or nameof(ServerMaster.ViewModels.Dashboard.ServerDashboardViewModel.TunnelState)
            or nameof(ServerMaster.ViewModels.Dashboard.ServerDashboardViewModel.TunnelActive))
        {
            OnPropertyChanged(nameof(ActiveTunnels));
            OnPropertyChanged(nameof(VisibleActiveTunnels));
            OnPropertyChanged(nameof(HasVisibleTunnels));
        }
    }

    public void OnNavigatedTo()
    {
        LoadServers();
    }

    private void LoadServers()
    {
        Servers.Clear();
        foreach (var profile in _repository.GetAllProfiles())
        {
            Servers.Add(new HomeServerItemViewModel(profile, _sessions));
        }
    }

    [RelayCommand]
    private void CreateNewServer()
    {
        CreateServerRequested?.Invoke(this, System.EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenServer(HomeServerItemViewModel item)
    {
        // If it's already running, don't start it again, just open it!
        var action = item.IsRunning ? ServerAutoAction.None : ServerAutoAction.Start;
        OpenServerRequested?.Invoke(this, new OpenServerEventArgs(item.Profile, action));
    }

    [RelayCommand]
    private void StopServer(HomeServerItemViewModel item)
    {
        OpenServerRequested?.Invoke(this, new OpenServerEventArgs(item.Profile, ServerAutoAction.Stop));
    }

    [RelayCommand]
    private void EditServer(HomeServerItemViewModel item)
    {
        EditServerRequested?.Invoke(this, item.Profile);
    }

    [ObservableProperty] private bool _isDeleteModalOpen;
    [ObservableProperty] private ServerProfile? _serverToDelete;

    [RelayCommand]
    private void DeleteServer(HomeServerItemViewModel item)
    {
        ServerToDelete = item.Profile;
        IsDeleteModalOpen = true;
    }

    [RelayCommand]
    private void ConfirmDeleteServer()
    {
        if (ServerToDelete != null)
        {
            _sessions.Unregister(ServerToDelete.Id);
            _repository.DeleteProfile(ServerToDelete);
            
            var playitSecretPath = System.IO.Path.Combine(AppContext.BaseDirectory, $"playit-secret-{ServerToDelete.Id}.toml");
            if (System.IO.File.Exists(playitSecretPath))
                System.IO.File.Delete(playitSecretPath);
                
            LoadServers();
            ServerToDelete = null;
        }
        IsDeleteModalOpen = false;
    }

    [RelayCommand]
    private void CancelDeleteServer()
    {
        ServerToDelete = null;
        IsDeleteModalOpen = false;
    }
}
