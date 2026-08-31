using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ServerMaster.ViewModels.Dashboard;

namespace ServerMaster.ViewModels.Services;

public class SessionManager
{
    private readonly ConcurrentDictionary<Guid, ServerDashboardViewModel> _activeSessions = new();

    public event EventHandler? SessionChanged;

    public void Register(Guid profileId, ServerDashboardViewModel vm)
    {
        _activeSessions[profileId] = vm;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Unregister(Guid profileId)
    {
        if (_activeSessions.TryRemove(profileId, out _))
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ServerDashboardViewModel? Get(Guid profileId)
    {
        return _activeSessions.TryGetValue(profileId, out var vm) ? vm : null;
    }

    public IEnumerable<ServerDashboardViewModel> GetActiveSessions()
    {
        return _activeSessions.Values;
    }
}
