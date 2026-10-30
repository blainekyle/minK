using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using mink.Services;
using mink.ViewModels;
using mink.Views;

namespace mink;

public partial class App : Application
{
    private SingleInstanceService? _ipcServer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = vm,
            };
            desktop.MainWindow = mainWindow;

            if (Program.ResolvedStartupPaths.Length > 0)
            {
                vm.OpenStartupFiles(Program.ResolvedStartupPaths);
            }
            else if (desktop.Args != null && desktop.Args.Length > 0)
            {
                string[] fallbackPaths = SingleInstanceService.ResolveCliPaths(desktop.Args, System.Environment.CurrentDirectory);
                if (fallbackPaths.Length > 0)
                {
                    vm.OpenStartupFiles(fallbackPaths);
                }
            }

            if (!Program.IsDedicatedInstance)
            {
                _ipcServer = new SingleInstanceService(incomingPaths =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (incomingPaths.Length > 0)
                        {
                            vm.OpenStartupFiles(incomingPaths);
                        }

                        if (mainWindow.WindowState == WindowState.Minimized)
                        {
                            mainWindow.WindowState = WindowState.Normal;
                        }

                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.Topmost = true;
                        mainWindow.Topmost = false;
                        mainWindow.Focus();
                    });
                });
                _ipcServer.StartServer();

                desktop.Exit += (_, _) =>
                {
                    _ipcServer?.Dispose();
                    _ipcServer = null;
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
