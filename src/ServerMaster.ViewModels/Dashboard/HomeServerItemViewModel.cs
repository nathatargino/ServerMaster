using CommunityToolkit.Mvvm.ComponentModel;
using ServerMaster.Core.Models;
using ServerMaster.ViewModels.Services;

namespace ServerMaster.ViewModels.Dashboard;

public sealed partial class HomeServerItemViewModel : ObservableObject
{
    private readonly SessionManager _sessions;
    
    public ServerProfile Profile { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private ServerState _state = ServerState.Stopped;

    public bool IsRunning => State is ServerState.Running or ServerState.Starting;

    public HomeServerItemViewModel(ServerProfile profile, SessionManager sessions)
    {
        Profile = profile;
        _sessions = sessions;
        
        RefreshState();
        _sessions.SessionChanged += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshState);
    }

    private void RefreshState()
    {
        var session = _sessions.Get(Profile.Id);
        if (session != null)
        {
            State = session.State;
            session.PropertyChanged -= Session_PropertyChanged;
            session.PropertyChanged += Session_PropertyChanged;
        }
        else
        {
            State = ServerState.Stopped;
        }
    }

    private void Session_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerDashboardViewModel.State))
        {
            var session = (ServerDashboardViewModel)sender!;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                State = session.State;
            });
        }
    }
}
