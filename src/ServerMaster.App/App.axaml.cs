using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Factories;
using ServerMaster.Core.Services;
using ServerMaster.ViewModels.Dashboard;
using ServerMaster.ViewModels.Wizard;

namespace ServerMaster.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ── DI Registration ──────────────────────────────────────────────────
        var services = new ServiceCollection();

        // Core services
        services.AddTransient<ProcessManagerService>();
        services.AddTransient<ResourceMonitorService>();
        services.AddTransient<INetworkTunnel, PlayitTunnelService>();
        services.AddSingleton<IServerFactory, ServerEngineFactory>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<ServerRepository>();
        services.AddSingleton<ServerMaster.ViewModels.Services.SessionManager>();

        // ViewModels
        services.AddTransient<WizardHostViewModel>();
        services.AddTransient<ServerDashboardViewModel>();

        Services = services.BuildServiceProvider();

        // ── Auto-Update Boot ──────────────────────────────────────────────────
#if !DEBUG
        _ = Task.Run(async () =>
        {
            try { Core.Services.AutoUpdaterService.CleanupOldFiles(); } catch {}
            try { await Core.Services.AutoUpdaterService.CheckAndUpdateAsync(); } catch {}
        });
#endif

        // ── Bootstrap window ─────────────────────────────────────────────────
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = Services.GetRequiredService<AppSettingsService>();
            var shell = new AppShellViewModel(null!);
            
            if (string.IsNullOrWhiteSpace(settings.Current.Username))
            {
                var welcome = new ServerMaster.ViewModels.WelcomeViewModel(settings);
                SetupWelcomeEvents(welcome, shell);
                shell.CurrentPage = welcome;
            }
            else
            {
                var home = new ServerMaster.ViewModels.HomeViewModel(settings, Services.GetRequiredService<ServerRepository>(), Services.GetRequiredService<ServerMaster.ViewModels.Services.SessionManager>());
                SetupHomeEvents(home, shell);
                shell.CurrentPage = home;
            }
            
            var window = new MainWindow { DataContext = shell };
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupWelcomeEvents(ServerMaster.ViewModels.WelcomeViewModel welcome, AppShellViewModel shell)
    {
        welcome.WelcomeCompleted += (s, e) =>
        {
            var settings = Services.GetRequiredService<AppSettingsService>();
            var home = new ServerMaster.ViewModels.HomeViewModel(settings, Services.GetRequiredService<ServerRepository>(), Services.GetRequiredService<ServerMaster.ViewModels.Services.SessionManager>());
            home.OnNavigatedTo();
            SetupHomeEvents(home, shell);
            shell.CurrentPage = home;
        };
    }

    private void SetupHomeEvents(ServerMaster.ViewModels.HomeViewModel home, AppShellViewModel shell)
    {
        home.CreateServerRequested += (s, e) =>
        {
            var wizard = Services.GetRequiredService<WizardHostViewModel>();
            
            wizard.Cancelled += (sender, args) =>
            {
                shell.CurrentPage = home;
            };
            
            wizard.ServerCreated += (_, engine) =>
            {
                var tunnel = Services.GetRequiredService<Core.Abstractions.INetworkTunnel>();
                tunnel.Initialize(wizard.FinalProfile!); // FIX: Ensure tunnel receives the profile immediately after creation

                var dash = new ServerDashboardViewModel(engine, tunnel, wizard.FinalProfile!);
                
                var sessions = Services.GetRequiredService<ServerMaster.ViewModels.Services.SessionManager>();
                sessions.Register(wizard.FinalProfile!.Id, dash);

                dash.ReturnToMenuRequested += (sender, args) =>
                {
                    home.OnNavigatedTo(); // Refresh list to show newly created server
                    SetupHomeEvents(home, shell); // REBIND logic
                    shell.CurrentPage = home;
                };

                shell.CurrentPage = dash;
            };
            
            wizard.ServerEdited += (_, profile) =>
            {
                home.OnNavigatedTo();
                shell.CurrentPage = home;
            };

            shell.CurrentPage = wizard;
        };

        home.EditServerRequested += (s, profile) =>
        {
            var wizard = Services.GetRequiredService<WizardHostViewModel>();
            wizard.LoadForEdit(profile);

            wizard.Cancelled += (sender, args) =>
            {
                shell.CurrentPage = home;
            };

            wizard.ServerEdited += (_, _) =>
            {
                home.OnNavigatedTo();
                shell.CurrentPage = home;
            };

            shell.CurrentPage = wizard;
        };

        home.OpenServerRequested += (s, args) =>
        {
            var sessions = Services.GetRequiredService<ServerMaster.ViewModels.Services.SessionManager>();
            var profile = args.Profile;
            var dash = sessions.Get(profile.Id);

            if (dash == null)
            {
                var engineFactory = Services.GetRequiredService<Core.Abstractions.IServerFactory>();
                var tunnel = Services.GetRequiredService<Core.Abstractions.INetworkTunnel>();
                
                var engine = engineFactory.Create(profile);
                engine.Initialize(profile); // FIX: Ensure engine receives the profile!
                tunnel.Initialize(profile); // FIX: Ensure tunnel receives the profile!
                
                dash = new ServerDashboardViewModel(engine, tunnel, profile);
                sessions.Register(profile.Id, dash);
            }

            // Wire up return event safely (avoid duplicate subscriptions)
            dash.ReturnToMenuRequested -= DashReturnHandler;
            dash.ReturnToMenuRequested += DashReturnHandler;

            shell.CurrentPage = dash;

            // Handle the requested auto-action
            if (args.AutoAction == ServerMaster.ViewModels.ServerAutoAction.Start && 
               (dash.State == Core.Models.ServerState.Stopped || dash.State == Core.Models.ServerState.Idle))
                dash.StartServerCommand.Execute(null);
            else if (args.AutoAction == ServerMaster.ViewModels.ServerAutoAction.Stop && dash.State == Core.Models.ServerState.Running)
                dash.StopServerCommand.Execute(null);

            void DashReturnHandler(object? sender, EventArgs args)
            {
                home.OnNavigatedTo();
                shell.CurrentPage = home;
            }
        };
    }
}

/// <summary>Simple shell VM that holds the currently visible page.</summary>
public sealed partial class AppShellViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private object _currentPage;

    public AppShellViewModel(object initialPage) => _currentPage = initialPage;
}
