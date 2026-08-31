using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Models;

namespace ServerMaster.ViewModels.Wizard;

public sealed partial class ModuleOption : ObservableObject
{
    public ModuleOption(string id, string displayName, string description, bool isMinecraftOnly)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        IsMinecraftOnly = isMinecraftOnly;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsMinecraftOnly { get; }

    [ObservableProperty]
    private bool _isSelected;
    
    [RelayCommand]
    private void Toggle()
    {
        IsSelected = !IsSelected;
    }
}

/// <summary>
/// Step 5 – Optional plugin / mod packages.
/// Also the final step that triggers server creation.
/// </summary>
public sealed partial class Step5ModulesViewModel : ObservableObject
{
    private readonly WizardHostViewModel _host;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    public string ActionLabel => _host.IsEditMode ? "Salvar Alterações ✓" : "Criar Servidor ✓";

    public ObservableCollection<ModuleOption> Modules { get; } =
    [
        new("essentialsx",   "EssentialsX",   "Comandos essenciais (/home, /tp, /warp)", true),
        new("worldguard",    "WorldGuard",    "Proteção de regiões e blocos",            true),
        new("vault",         "Vault",         "API de economia e permissões",            true),
        new("luckperms",     "LuckPerms",     "Sistema de permissões avançado",          false),
        new("dynmap",        "Dynmap",        "Mapa web em tempo real do servidor",      true),
    ];

    public ObservableCollection<ModuleOption> SelectedModules { get; } = [];

    public Step5ModulesViewModel(WizardHostViewModel host) => _host = host;

    public bool IsMinecraft => _host.SelectedGame == GameType.Minecraft;

    public IEnumerable<ModuleOption> VisibleModules =>
        IsMinecraft ? Modules : Modules.Where(m => !m.IsMinecraftOnly);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateServerAsync()
    {
        IsBusy = true;
        StatusMessage = _host.IsEditMode ? "Salvando alterações…" : "Criando servidor…";
        try
        {
            _host.SetModules([.. SelectedModules.Select(m => m.Id)]);
            if (_host.IsEditMode)
                await _host.SaveEditAsync();
            else
                await _host.CreateServerAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL WIZARD ERROR]: {ex}");
            StatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreate() => !IsBusy;

    [RelayCommand]
    private void Back() => _host.Back();

    public void ToggleModule(ModuleOption module)
    {
        module.IsSelected = !module.IsSelected;
        if (module.IsSelected && !SelectedModules.Contains(module))
            SelectedModules.Add(module);
        else if (!module.IsSelected && SelectedModules.Contains(module))
            SelectedModules.Remove(module);
    }
}
