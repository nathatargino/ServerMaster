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
            try
            {
                using var http = new HttpClient();
                // We use a GitHub raw URL to host a simple JSON array of Hytale versions
                var json = await http.GetStringAsync("https://raw.githubusercontent.com/nathatargino/ServerMaster/main/hytale-versions.json").ConfigureAwait(false);
                var versions = System.Text.Json.JsonSerializer.Deserialize<string[]>(json);

                Dispatcher.UIThread.Post(() => 
                {
                    AvailableVersions.Clear();
                    if (versions != null)
                    {
                        foreach (var v in versions)
                            AvailableVersions.Add(v);
                    }
                    
                    if (AvailableVersions.Count == 0)
                        AddHytaleFallbacks();

                    SelectedVersion = AvailableVersions.FirstOrDefault();
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() => 
                {
                    AddHytaleFallbacks();
                    SelectedVersion = AvailableVersions.FirstOrDefault();
                });
            }
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
                    AddMinecraftFallbacks();
                }

                SelectedVersion = AvailableVersions.FirstOrDefault();
            });
        }
        catch
        {
            // Fallback popular versions if no internet
            Dispatcher.UIThread.Post(() => 
            {
                AddMinecraftFallbacks();
                SelectedVersion = AvailableVersions.FirstOrDefault();
            });
        }
    }

    private void AddHytaleFallbacks()
    {
        AvailableVersions.Clear();
        var hytaleVersions = new[] { 
            "0.6.5", "0.6.4", "0.6.3", "0.6.2", "0.6.1", 
            "0.6.0", "0.5.9", "0.5.8", "0.5.7", "0.5.6" 
        };
        foreach (var v in hytaleVersions)
            AvailableVersions.Add(v);
    }

    private void AddMinecraftFallbacks()
    {
        AvailableVersions.Clear();
        var mcVersions = new[] { "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21", "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.19.4" };
        foreach (var v in mcVersions)
            AvailableVersions.Add(v);
    }
}

