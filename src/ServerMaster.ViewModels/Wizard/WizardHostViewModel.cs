using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Models;

namespace ServerMaster.ViewModels.Wizard;

/// <summary>
/// Orchestrates the 5-step creation wizard.
/// Stores the in-progress <see cref="ServerProfile"/> and coordinates step navigation.
/// </summary>
public sealed partial class WizardHostViewModel : ObservableObject
{
    private readonly IServerFactory _factory;
    private readonly INetworkTunnel _tunnel;
    private readonly ServerMaster.Core.Services.ServerRepository _repository;

    // ── Edit mode ────────────────────────────────────────────────────────────
    public bool IsEditMode { get; private set; }
    private ServerProfile? _editingProfile;
    // ── In-progress profile being built by the wizard ────────────────────────
    public   Guid             TargetProfileId { get; private set; } = Guid.NewGuid();
    public   ServerProfile?   FinalProfile    { get; private set; }
    internal GameType         SelectedGame    { get; private set; }
    private  string           _name           = string.Empty;
    private  string           _description    = string.Empty;
    private  string           _version        = string.Empty;
    private  string           _gameMode       = "survival";
    private  MinecraftVariant _variant        = MinecraftVariant.Paper;
    private  ResourceLimits   _resources      = new();
    private  Core.Models.NetworkMode _networkMode;
    private  int              _port           = 25565;
    private  int              _maxPlayers     = 20;
    private  bool             _allowPiratePlayers;
    private  List<string>     _modules        = [];

    // ── Navigation ───────────────────────────────────────────────────────────
    [ObservableProperty] private object _currentStep = null!;
    [ObservableProperty] private int              _stepIndex;
    public int TotalSteps => _steps.Count;



    private readonly List<ObservableObject> _steps;

    public WizardHostViewModel(IServerFactory factory, INetworkTunnel tunnel, ServerMaster.Core.Services.ServerRepository repository)
    {
        _factory = factory;
        _tunnel  = tunnel;
        _repository = repository;

        _steps =
        [
            new Step1GameSelectViewModel(this),
            new Step2IdentityViewModel(this),
            new Step3HardwareViewModel(this),
            new Step4NetworkViewModel(this),
            new Step4bPlayitSetupViewModel(this),
            new Step5ModulesViewModel(this)
        ];

        CurrentStep = _steps[0];
    }

    /// <summary>
    /// Enters edit mode by pre-populating wizard data from an existing profile,
    /// then navigating directly to step 2 (skipping game type selection).
    /// </summary>
    public void LoadForEdit(ServerProfile profile)
    {
        IsEditMode = true;
        _editingProfile = profile;

        // Pre-fill host fields
        SelectedGame    = profile.Game;
        _name           = profile.Name;
        _description    = profile.Description ?? string.Empty;
        _version        = profile.GameVersion;
        _gameMode       = profile.GameMode ?? "survival";
        _variant        = profile.MinecraftVariant ?? MinecraftVariant.Paper;
        _resources      = profile.Resources ?? new();
        _networkMode    = profile.NetworkMode;
        _port           = profile.Port;
        _maxPlayers     = profile.MaxPlayers;
        _allowPiratePlayers = profile.AllowPiratePlayers;
        _modules        = new List<string>(profile.Modules ?? []);

        // Pre-fill each step VM
        if (_steps[1] is Step2IdentityViewModel step2)
        {
            step2.ServerName       = profile.Name;
            step2.Description      = profile.Description ?? string.Empty;
            step2.SelectedVariant  = profile.MinecraftVariant ?? MinecraftVariant.Paper;
            step2.OnNavigatedTo();
            // selected version will be set after version list loads; this is async, so we set it after
            _ = Task.Run(async () =>
            {
                await Task.Delay(800); // give LoadVersionsAsync a moment
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    step2.SelectedVersion = profile.GameVersion);
            });
        }
        if (_steps[2] is Step3HardwareViewModel step3)
        {
            step3.RamMb         = profile.Resources?.RamMb    ?? 2048;
            step3.RamMinMb      = profile.Resources?.RamMinMb  ?? 512;
            step3.MaxCpuPercent = profile.Resources?.MaxCpuPercent ?? 80;
        }
        if (_steps[3] is Step4NetworkViewModel step4)
        {
            step4.NetworkMode         = profile.NetworkMode;
            step4.ServerPort          = profile.Port;
            step4.MaxPlayers          = profile.MaxPlayers;
            step4.AllowPiratePlayers  = profile.AllowPiratePlayers;
        }
        if (_steps[4] is Step5ModulesViewModel step5)
        {
            foreach (var m in step5.Modules)
            {
                m.IsSelected = profile.Modules?.Contains(m.Id) == true;
                if (m.IsSelected && !step5.SelectedModules.Contains(m))
                    step5.SelectedModules.Add(m);
            }
        }

        // Skip step 1 (game type) — jump to step 2
        StepIndex = 1;
        CurrentStep = _steps[1];
    }

    // ── Wizard setters (called by each step VM) ──────────────────────────────

    internal void SetGame(GameType game) => SelectedGame = game;

    internal void SetIdentity(string name, string description, string version, MinecraftVariant variant, string gameMode)
    {
        _name = name; _description = description;
        _version = version; _variant = variant; _gameMode = gameMode;
    }

    internal void SetResources(ResourceLimits limits) => _resources = limits;

    internal void SetNetwork(Core.Models.NetworkMode networkMode, int port, bool allowPiratePlayers, int maxPlayers)
    {
        _networkMode = networkMode;
        _port = port;
        _allowPiratePlayers = allowPiratePlayers;
        _maxPlayers = maxPlayers;
    }

    internal void SetModules(List<string> modules) => _modules = modules;

    // ── Navigation ───────────────────────────────────────────────────────────

    // Returns true if Step4b should be skipped (not PlayitTunnel or secret already configured)
    private bool ShouldSkipPlayitSetup()
    {
        if (_networkMode != Core.Models.NetworkMode.PlayitTunnel) return true;
        var secretPath = Path.Combine(AppContext.BaseDirectory, "playit-secret.toml");
        return File.Exists(secretPath);
    }

    internal void Next()
    {
        if (StepIndex >= _steps.Count - 1) return;

        var nextIndex = StepIndex + 1;
        var next = _steps[nextIndex];

        // Skip Step4b when not PlayitTunnel or secret already exists
        if (next is Step4bPlayitSetupViewModel && ShouldSkipPlayitSetup())
        {
            nextIndex++;
            if (nextIndex >= _steps.Count) return;
            next = _steps[nextIndex];
        }

        NavigateTo(nextIndex);
    }

    internal void Back()
    {
        if (StepIndex <= 0) return;

        var prevIndex = StepIndex - 1;
        var prev = _steps[prevIndex];

        // Skip Step4b in reverse too — same condition
        if (prev is Step4bPlayitSetupViewModel && ShouldSkipPlayitSetup())
        {
            prevIndex--;
            if (prevIndex < 0) return;
        }

        NavigateTo(prevIndex);
    }

    private void NavigateTo(int index)
    {
        StepIndex = index;
        CurrentStep = _steps[index];

        if (CurrentStep is Step2IdentityViewModel step2)
            step2.OnNavigatedTo();
        else if (CurrentStep is Step4NetworkViewModel step4)
            step4.OnNavigatedTo();
        else if (CurrentStep is Step4bPlayitSetupViewModel step4b)
            step4b.OnNavigatedTo();
    }

    // ── Server creation ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds the final profile and kicks off the engine preparation pipeline.
    /// Raises <see cref="ServerCreated"/> when done so the shell can navigate to the dashboard.
    /// </summary>
    public async Task CreateServerAsync()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ServerMaster", "Servers", _name.Replace(" ", "_"));

        var profile = new ServerProfile
        {
            Id               = TargetProfileId,
            Name             = _name,
            Description      = _description,
            Game             = SelectedGame,
            MinecraftVariant = SelectedGame == GameType.Minecraft ? _variant : null,
            GameVersion      = _version,
            GameMode         = SelectedGame == GameType.Minecraft ? _gameMode : "survival",
            Resources        = _resources,
            NetworkMode      = _networkMode,
            Port             = _port,
            MaxPlayers       = _maxPlayers,
            AllowPiratePlayers = _allowPiratePlayers,
            Modules          = _modules,
            ServerDirectory  = baseDir
        };
        
        FinalProfile = profile;
        
        _repository.SaveProfile(profile);

        var engine = _factory.Create(profile);

        var progress = new Progress<string>();
        await engine.PrepareAsync(profile, progress);

        ServerCreated?.Invoke(this, engine);
    }

    /// <summary>Saves changes to the currently-edited profile without recreating the engine.</summary>
    public Task SaveEditAsync()
    {
        if (_editingProfile == null) return Task.CompletedTask;

        _editingProfile.Name              = _name;
        _editingProfile.Description       = _description;
        _editingProfile.GameVersion        = _version;
        _editingProfile.MinecraftVariant   = _editingProfile.Game == GameType.Minecraft ? _variant : null;
        _editingProfile.GameMode           = _editingProfile.Game == GameType.Minecraft ? _gameMode : "survival";
        _editingProfile.Resources          = _resources;
        _editingProfile.NetworkMode        = _networkMode;
        _editingProfile.Port               = _port;
        _editingProfile.MaxPlayers         = _maxPlayers;
        _editingProfile.AllowPiratePlayers  = _allowPiratePlayers;
        _editingProfile.Modules            = _modules;

        FinalProfile = _editingProfile;
        _repository.SaveProfile(_editingProfile);
        ServerEdited?.Invoke(this, _editingProfile);
        return Task.CompletedTask;
    }

    public event EventHandler<ServerProfile>? ServerEdited;

    /// <summary>Fired when the server has been prepared and is ready to enter the dashboard.</summary>
    public event EventHandler<IServerEngine>? ServerCreated;

    public event System.EventHandler? Cancelled;
    public void Cancel() => Cancelled?.Invoke(this, System.EventArgs.Empty);
}
