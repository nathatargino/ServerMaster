using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Services;
using Avalonia.Threading;

namespace ServerMaster.ViewModels.Wizard;

/// <summary>
/// Optional Step 4b – Playit Third-Party setup code.
/// Only shown when the user selects PlayitTunnel in step 4 and no secret is configured yet.
/// Connects natively to Playit.gg using the CLI.
/// </summary>
public sealed partial class Step4bPlayitSetupViewModel : ObservableObject
{
    private readonly WizardHostViewModel _host;

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _statusMessage = "Aguardando vinculação com Playit.gg...";

    public Step4bPlayitSetupViewModel(WizardHostViewModel host)
    {
        _host = host;
    }

    /// <summary>Called by the host just before this step is shown.</summary>
    public void OnNavigatedTo()
    {
        // Automatically start the claim process
        _ = Next();
    }

    [RelayCommand]
    private async Task Next()
    {
        if (IsSaving) return;
        
        IsSaving = true;
        StatusMessage = "Iniciando cliente Playit...";
        try
        {
            await PlayitTunnelService.ClaimAgentNativeAsync(_host.TargetProfileId.ToString(), msg => 
            {
                Dispatcher.UIThread.Post(() => StatusMessage = msg);
            });
            
            StatusMessage = "Túnel vinculado com sucesso! ✓";
            await Task.Delay(1000);
            _host.Next();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Skip() => _host.Next(); // allow skip if already configured

    [RelayCommand]
    private void Back() => _host.Back();
}
