using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Models;

namespace ServerMaster.ViewModels.Wizard;

/// <summary>
/// Step 2 – Server name, description and game version selection.
/// </summary>
public sealed partial class Step2IdentityViewModel : ObservableObject
{
    private readonly WizardHostViewModel _host;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _serverName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string? _selectedVersion;

    [ObservableProperty]
    private MinecraftVariant _selectedVariant = MinecraftVariant.Paper;

    [ObservableProperty]
    private string _selectedGameMode = "survival";

    public ObservableCollection<string> AvailableVersions { get; } = [];

    public ObservableCollection<string> GameModes { get; } =
    [
        "survival",
        "creative",
        "adventure",
        "hardcore"
    ];

    public ObservableCollection<MinecraftVariant> MinecraftVariants { get; } =
    [
        MinecraftVariant.Paper,
        MinecraftVariant.Purpur,
        MinecraftVariant.Forge,
        MinecraftVariant.Fabric,
        MinecraftVariant.Vanilla
    ];

    /// <summary>Show Minecraft-specific fields only when game is Minecraft.</summary>
    public bool IsMinecraft => _host.SelectedGame == GameType.Minecraft;

    public Step2IdentityViewModel(WizardHostViewModel host)
    {
        _host = host;
    }

    /// <summary>Called by WizardHostViewModel just before this page is shown.</summary>
    public void OnNavigatedTo()
    {
        OnPropertyChanged(nameof(IsMinecraft));
        _ = LoadVersionsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        _host.SetIdentity(ServerName, Description, SelectedVersion ?? "1.0.0", SelectedVariant, SelectedGameMode);
        _host.Next();
    }

    [RelayCommand]
    private void Back() => _host.Back();

    private bool CanGoNext() =>
        !string.IsNullOrWhiteSpace(ServerName) && !string.IsNullOrWhiteSpace(SelectedVersion);

    private async Task LoadVersionsAsync()
    {
        if (!IsMinecraft) 
        { 
            Dispatcher.UIThread.Post(() => 
            {
                AvailableVersions.Clear();
                // User requested Hytale versions starting from 0.6.2
                var hytaleVersions = new[] { 
                    "0.6.2", "0.6.1", "0.6.0", "0.5.9", "0.5.8", 
                    "0.5.7", "0.5.6", "0.5.5", "0.5.4", "0.5.3" 
                };
                foreach (var v in hytaleVersions)
                    AvailableVersions.Add(v);
                
                SelectedVersion = AvailableVersions.FirstOrDefault();
            });
            return; 
        }

        try
        {
            using var http = new HttpClient();
            var json = await http.GetStringAsync(
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json").ConfigureAwait(false);

            // Quick parse of versions array from manifest using regex, focusing on release versions
            // Mojang JSON structure usually has "id": "1.21.1", "type": "release"
            // We force "1\." to avoid capturing new metadata releases like "26.2" from the unified meta formats
            var matches = System.Text.RegularExpressions.Regex.Matches(
                json, @"""id""\s*:\s*""(1\.\d+(?:\.\d+)?)""\s*,\s*""type""\s*:\s*""release""");

            Dispatcher.UIThread.Post(() => 
            {
                AvailableVersions.Clear();
                var distinctVersions = matches.Cast<System.Text.RegularExpressions.Match>()
                                              .Select(m => m.Groups[1].Value)
                                              .Distinct()
                                              .Take(10)
                                              .ToList();

                foreach (var v in distinctVersions)
                    AvailableVersions.Add(v);

                // Fallback in case regex missed completely
                if (AvailableVersions.Count == 0)
                {
                    foreach (var v in new[] { "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.19.4" })
                        AvailableVersions.Add(v);
                }

                SelectedVersion = AvailableVersions.FirstOrDefault();
            });
        }
        catch
        {
            // Fallback popular versions if no internet
            Dispatcher.UIThread.Post(() => 
            {
                AvailableVersions.Clear();
                foreach (var v in new[] { "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.19.4" })
                    AvailableVersions.Add(v);
                SelectedVersion = "1.21.4";
            });
        }
    }
}

