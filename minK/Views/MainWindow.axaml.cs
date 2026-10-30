using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using mink.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mink.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private PreviewWindow? _detachedPreviewWindow;

    private double _lastExplorerWidth = 215;
    private double _lastChatWidth = 355;
    private int _scrollQueued;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Opened += OnWindowOpened;
        Closed += OnWindowClosed;

        KeyDown += OnWindowKeyDown;
        AddHandler(DragDrop.DropEvent, OnFileDropped);
        ChatPromptBox.AddHandler(KeyDownEvent, OnChatPromptTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as MainViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            WireViewModelCallbacks(_vm);
            UpdateWorkspaceColumnWidths();
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.AttachEditorSurface(MonacoSurface);
            _vm.AttachDockedPreviewSurface(DockedPreviewSurfaceControl);
            UpdateWorkspaceColumnWidths();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_detachedPreviewWindow != null)
        {
            _detachedPreviewWindow.Close();
            _detachedPreviewWindow = null;
        }
    }

    private void WireViewModelCallbacks(MainViewModel vm)
    {
        vm.PickOpenFilesDialog = PickOpenFilesAsync;
        vm.PickSaveFileDialog = PickSaveFileAsync;
        vm.PickOpenFolderDialog = PickOpenFolderAsync;

        vm.ShowSettingsDialog = async (current, client) =>
        {
            var dlg = new SettingsWindow(current, client);
            await dlg.ShowDialog<bool>(this);
            return dlg.ResultSettings;
        };

        vm.ShowPreferencesDialog = async current =>
        {
            var dlg = new PreferencesWindow(current);
            await dlg.ShowDialog<bool>(this);
            return dlg.ResultSettings;
        };

        vm.ConfirmClearChatDialog = async () =>
        {
            var dlg = new ConfirmClearWindow();
            await dlg.ShowDialog<bool>(this);
            return (dlg.Confirmed, dlg.DontAskAgain);
        };

        vm.RequestOpenDetachedPreviewWindow = () =>
        {
            if (_detachedPreviewWindow != null)
            {
                _detachedPreviewWindow.Activate();
                return;
            }

            var win = new PreviewWindow
            {
                DataContext = vm
            };

            win.WindowClosingWithAction += dockBack =>
            {
                _detachedPreviewWindow = null;
                vm.OnDetachedWindowClosed(dockBack);
            };

            _detachedPreviewWindow = win;
            vm.AttachDetachedPreviewSurface(win.PreviewSurface);
            win.Show();
        };

        vm.RequestCloseDetachedPreviewWindow = () =>
        {
            _detachedPreviewWindow?.Close();
        };

        vm.ScrollChatToBottom = () =>
        {
            if (Interlocked.CompareExchange(ref _scrollQueued, 1, 0) == 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Interlocked.Exchange(ref _scrollQueued, 0);
                    ChatScrollViewer.ScrollToEnd();
                }, DispatcherPriority.Background);
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsExplorerVisible) or
            nameof(MainViewModel.IsDockedPreviewVisible) or
            nameof(MainViewModel.IsChatVisible))
        {
            UpdateWorkspaceColumnWidths();
        }
    }

    private void UpdateWorkspaceColumnWidths()
    {
        if (_vm == null || WorkspaceGrid.ColumnDefinitions.Count < 6) return;

        var cols = WorkspaceGrid.ColumnDefinitions;

        if (!_vm.IsExplorerVisible && cols[0].Width.IsAbsolute && cols[0].Width.Value > 80)
        {
            _lastExplorerWidth = cols[0].Width.Value;
        }
        if (!_vm.IsChatVisible && cols[5].Width.IsAbsolute && cols[5].Width.Value > 180)
        {
            _lastChatWidth = cols[5].Width.Value;
        }

        cols[0].Width = _vm.IsExplorerVisible
            ? new GridLength(_lastExplorerWidth, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

        cols[1].Width = new GridLength(1, GridUnitType.Star);
        cols[1].MinWidth = 220;

        cols[2].Width = _vm.IsDockedPreviewVisible
            ? new GridLength(4, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

        cols[3].Width = _vm.IsDockedPreviewVisible
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0, GridUnitType.Pixel);
        cols[3].MinWidth = _vm.IsDockedPreviewVisible ? 180 : 0;

        cols[4].Width = _vm.IsChatVisible
            ? new GridLength(4, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

        cols[5].Width = _vm.IsChatVisible
            ? new GridLength(_lastChatWidth, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);
        cols[5].MinWidth = _vm.IsChatVisible ? 240 : 0;
    }

    private void OnChatPromptTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            if (_vm != null && _vm.IsNotGenerating)
            {
                _ = _vm.SendPromptCommand.ExecuteAsync(null);
            }
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && shift && e.Key == Key.S)
        {
            e.Handled = true;
            _ = _vm.SaveFileAsCommand.ExecuteAsync(null);
        }
        else if (ctrl && e.Key == Key.S)
        {
            e.Handled = true;
            _ = _vm.SaveFileCommand.ExecuteAsync(null);
        }
        else if (ctrl && e.Key == Key.O)
        {
            e.Handled = true;
            _ = _vm.OpenFileCommand.ExecuteAsync(null);
        }
        else if (ctrl && e.Key == Key.N)
        {
            e.Handled = true;
            _vm.NewFileCommand.Execute(null);
        }
        else if (ctrl && e.Key == Key.B)
        {
            e.Handled = true;
            _vm.ToggleExplorerCommand.Execute(null);
        }
        else if (ctrl && e.Key == Key.OemComma)
        {
            e.Handled = true;
            _ = _vm.OpenPreferencesCommand.ExecuteAsync(null);
        }
    }

    private void OnFileDropped(object? sender, DragEventArgs e)
    {
        if (_vm == null) return;
        var files = e.Data.GetFiles();
        if (files == null) return;

        foreach (var item in files)
        {
            string? localPath = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(localPath))
            {
                if (File.Exists(localPath))
                {
                    _vm.OpenFileFromPath(localPath);
                }
                else if (Directory.Exists(localPath))
                {
                    _vm.LoadFolder(localPath);
                }
            }
        }
    }

    private async Task<string[]?> PickOpenFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File — minK",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Web & Code Files")
                {
                    Patterns = new[] { "*.html", "*.htm", "*.css", "*.js", "*.mjs", "*.ts", "*.json", "*.py", "*.md", "*.svg", "*.xml", "*.yaml", "*.yml" }
                },
                new("HTML Files") { Patterns = new[] { "*.html", "*.htm" } },
                new("Python Files") { Patterns = new[] { "*.py" } },
                FilePickerFileTypes.All
            }
        });

        if (files == null || files.Count == 0) return null;

        return files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToArray();
    }

    private async Task<string?> PickSaveFileAsync(string suggestedFileName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File — minK",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "html",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("HTML Document") { Patterns = new[] { "*.html", "*.htm" } },
                new("CSS Stylesheet") { Patterns = new[] { "*.css" } },
                new("JavaScript") { Patterns = new[] { "*.js" } },
                new("Python Script") { Patterns = new[] { "*.py" } },
                new("JSON") { Patterns = new[] { "*.json" } },
                new("Markdown") { Patterns = new[] { "*.md" } },
                FilePickerFileTypes.All
            }
        });

        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickOpenFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Project Folder — minK",
            AllowMultiple = false
        });

        return folders?.FirstOrDefault()?.TryGetLocalPath();
    }
}
