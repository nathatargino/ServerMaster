using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Services;

namespace ServerMaster.ViewModels;

public sealed partial class WelcomeViewModel : ObservableObject
{
    private readonly AppSettingsService _settings;
    public event System.EventHandler? WelcomeCompleted;

    [ObservableProperty]
    private string _username = string.Empty;

    public WelcomeViewModel(AppSettingsService settings)
    {
        _settings = settings;
    }

    [RelayCommand]
    private void Continue()
    {
        if (string.IsNullOrWhiteSpace(Username)) return;

        _settings.Current.Username = Username.Trim();
        _settings.Save();

        WelcomeCompleted?.Invoke(this, System.EventArgs.Empty);
    }
}
