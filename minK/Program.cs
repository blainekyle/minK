using Avalonia;
using mink.Services;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace mink;

sealed class Program
{
    private const int ATTACH_PARENT_PROCESS = -1;
    private const int ASFW_ANY = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    public static string[] ResolvedStartupPaths { get; private set; } = Array.Empty<string>();
    public static bool IsDedicatedInstance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a is "--version" or "-v"))
        {
            if (OperatingSystem.IsWindows()) AttachConsole(ATTACH_PARENT_PROCESS);
            Console.WriteLine("minK 1.0.0");
            return 0;
        }

        if (args.Any(a => a is "--help" or "-h"))
        {
            if (OperatingSystem.IsWindows()) AttachConsole(ATTACH_PARENT_PROCESS);
            Console.WriteLine("minK 1.0.0");
            Console.WriteLine();
            Console.WriteLine("Usage: mink [options] [paths...]");
            Console.WriteLine("  mink                    Launch minK (or focus running window)");
            Console.WriteLine("  mink index.html         Open file(s) in tabs (creates on save if new)");
            Console.WriteLine("  mink .                  Open folder in workspace explorer");
            Console.WriteLine("  mink -n [paths...]      Open in a new minK window");
            Console.WriteLine("  mink --wait <file>      Wait for window to close (for Git / $EDITOR)");
            Console.WriteLine("  mink --version          Show version");
            return 0;
        }

        ResolvedStartupPaths = SingleInstanceService.ResolveCliPaths(args, Environment.CurrentDirectory);
        IsDedicatedInstance = args.Any(a => a is "--new-window" or "-n" or "--wait" or "-w");

        if (!IsDedicatedInstance)
        {
            if (OperatingSystem.IsWindows())
            {
                try { AllowSetForegroundWindow(ASFW_ANY); } catch { }
            }

            if (SingleInstanceService.TryForwardToRunningInstance(ResolvedStartupPaths))
            {
                return 0;
            }
        }

        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
