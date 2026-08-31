using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerMaster.Core.Models;

namespace ServerMaster.ViewModels.Wizard;

/// <summary>
/// Step 3 – RAM and CPU allocation.
/// </summary>
public sealed partial class Step3HardwareViewModel : ObservableObject
{
    private readonly WizardHostViewModel _host;

    /// <summary>Maximum JVM heap in MB. Default 2048.</summary>
    [ObservableProperty] private int _ramMb = 2048;

    /// <summary>Minimum JVM heap in MB. Default 512.</summary>
    [ObservableProperty] private int _ramMinMb = 512;

    /// <summary>Max CPU % hint (UI guidance, not enforced). Default 80.</summary>
    [ObservableProperty] private int _maxCpuPercent = 80;

    public long SystemRamMb { get; } =
        GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1_048_576;

    /// <summary>Convenience: recommended max RAM = 50% of system RAM.</summary>
    public int RecommendedRamMb => (int)(SystemRamMb / 2);

    public Step3HardwareViewModel(WizardHostViewModel host) => _host = host;

    [RelayCommand]
    private void Next()
    {
        _host.SetResources(new ResourceLimits
        {
            RamMb = RamMb,
            RamMinMb = RamMinMb,
            MaxCpuPercent = MaxCpuPercent
        });
        _host.Next();
    }

    [RelayCommand]
    private void Back() => _host.Back();
}
