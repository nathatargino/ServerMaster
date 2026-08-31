using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Models;

namespace ServerMaster.ViewModels.Wizard;

/// <summary>
/// Step 1 – The user picks between Hytale and Minecraft.
/// </summary>
public sealed partial class Step1GameSelectViewModel : ObservableObject
{
    private readonly WizardHostViewModel _host;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private GameType? _selectedGame;

    public Step1GameSelectViewModel(WizardHostViewModel host) => _host = host;

    /// <summary>Called by the game card buttons — accepts "Minecraft" or "Hytale".</summary>
    [RelayCommand]
    private void SelectGame(string gameTypeString)
    {
        SelectedGame = Enum.Parse<GameType>(gameTypeString);
        OnPropertyChanged(nameof(IsMinecraftSelected));
        OnPropertyChanged(nameof(IsHytaleSelected));
    }

    public bool IsMinecraftSelected => SelectedGame == GameType.Minecraft;
    public bool IsHytaleSelected => SelectedGame == GameType.Hytale;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        _host.SetGame(SelectedGame!.Value);
        _host.Next();
    }

    [RelayCommand]
    private void Cancel()
    {
        _host.Cancel();
    }

    private bool CanGoNext() => SelectedGame.HasValue;
}

