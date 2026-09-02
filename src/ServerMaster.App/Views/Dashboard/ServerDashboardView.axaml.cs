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

        if (logScrollViewer != null)
        {
            logScrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;
        }

        // Hook into Datacontext changed to attach collection
        this.DataContextChanged += OnDataContextChanged;

        // Simple enter-to-send for the console terminal
        if (consoleInput != null)
        {
            consoleInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is ServerDashboardViewModel vm)
                {
                    vm.SendConsoleInputCommand.Execute(null);
                    consoleInput.SelectAll(); 
                }
            };
        }
    }

    private bool _autoScroll = true;

    private void LogScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var sv = sender as ScrollViewer;
        if (sv == null) return;
        
        // If extent changed (items added/removed), don't change _autoScroll state here
        if (e.ExtentDelta.Y != 0) return; 

        // If the user manually scrolled
        if (e.OffsetDelta.Y != 0)
        {
            // If they scrolled very close to the bottom (within 10 pixels), re-enable auto scroll
            var distanceFromBottom = sv.Extent.Height - sv.Viewport.Height - sv.Offset.Y;
            _autoScroll = distanceFromBottom < 10;
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ServerDashboardViewModel vm)
        {
            if (vm.FilteredLogs is System.Collections.Specialized.INotifyCollectionChanged notifyCol)
            {
                notifyCol.CollectionChanged -= FilteredLogs_CollectionChanged;
                notifyCol.CollectionChanged += FilteredLogs_CollectionChanged;
            }
        }
    }

    private void FilteredLogs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_autoScroll)
        {
            var sv = this.FindControl<ScrollViewer>("LogScrollViewer");
            if (sv != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    sv.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
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

    private async void SelectLocalBackupFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var vm = DataContext as ServerDashboardViewModel;
        if (vm == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Selecionar Pasta de Backup Local",
            AllowMultiple = false
        });

        if (result != null && result.Count > 0)
        {
            var folder = result[0];
            vm.UpdateLocalBackupDirectory(folder.Path.LocalPath);
        }
    }

    private async void ImportServerZip_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ServerDashboardViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Selecionar arquivo ZIP do Servidor",
                AllowMultiple = false,
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Arquivo ZIP") { Patterns = new[] { "*.zip" } } }
            });

            if (result != null && result.Count > 0)
            {
                var file = result[0];
                await vm.ImportServerAsync(file.Path.LocalPath);
            }
        }
    }

    private async void ImportServerFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ServerDashboardViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Selecionar Pasta do Servidor",
                AllowMultiple = false
            });

            if (result != null && result.Count > 0)
            {
                var folder = result[0];
                await vm.ImportServerAsync(folder.Path.LocalPath);
            }
        }
    }
}
