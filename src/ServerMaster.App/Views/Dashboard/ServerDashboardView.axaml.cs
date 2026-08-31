using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ServerMaster.ViewModels.Dashboard;

namespace ServerMaster.App.Views.Dashboard;

public partial class ServerDashboardView : UserControl
{
    public ServerDashboardView()
    {
        InitializeComponent();
        
        // Auto-scroll logic for log terminal
        var logItemsControl = this.FindControl<ItemsControl>("LogItemsControl");
        var logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        var consoleInput = this.FindControl<TextBox>("ConsoleInput");

        // Simple enter-to-send for the console terminal
        if (consoleInput != null)
        {
            consoleInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is ServerDashboardViewModel vm)
                {
                    vm.SendConsoleInputCommand.Execute(null);
                    // Select all to easily type the next command
                    consoleInput.SelectAll(); 
                }
            };
        }
    }

    private async void CopyLocalIp_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ServerDashboardViewModel vm && sender is Button btn)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(vm.LocalAddress))
            {
                await topLevel.Clipboard.SetTextAsync(vm.LocalAddress);
                
                // Visual feedback
                var oldContent = btn.Content;
                btn.Content = "Copiado!";
                await System.Threading.Tasks.Task.Delay(1500);
                btn.Content = oldContent;
            }
        }
    }

    private async void CopyPublicIp_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ServerDashboardViewModel vm && sender is Button btn)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(vm.PublicAddress) && !vm.PublicAddress.Contains("Aguardando"))
            {
                await topLevel.Clipboard.SetTextAsync(vm.PublicAddress);
                
                // Visual feedback
                var oldContent = btn.Content;
                btn.Content = "Copiado!";
                await System.Threading.Tasks.Task.Delay(1500);
                btn.Content = oldContent;
            }
        }
    }

    private async void CopyConsole_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ServerMaster.ViewModels.Dashboard.ServerDashboardViewModel vm && sender is Avalonia.Controls.Button btn)
        {
            var lines = vm.Logs.Select(l => $"[{l.Timestamp:HH:mm:ss}] [{l.Level}] {l.Message}");
            var text = string.Join(System.Environment.NewLine, lines);
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
                
                var oldContent = btn.Content;
                btn.Content = "Copiado!";
                await System.Threading.Tasks.Task.Delay(1500);
                btn.Content = oldContent;
            }
        }
    }
}
