using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OpenCodeUsagePlus.Core.Storage;
using OpenCodeUsagePlus.Tray;

namespace OpenCodeUsagePlus;

internal static class Program
{
    private const string MutexName = "OpenCodeUsagePlus.SingleInstance";
    private const string ShowEventName = "OpenCodeUsagePlus.Show";

    private static int _crashDialogShown;

    [STAThread]
    private static int Main(string[] args)
    {
        AppEnvironment.PlainWindow =
            args.Any(argument => argument.Equals("--safe-mode", StringComparison.OrdinalIgnoreCase))
            || Environment.GetEnvironmentVariable("OPENCODE_USAGE_PLAIN_WINDOW") == "1";

        AppLog.Info(
            $"start version={typeof(Program).Assembly.GetName().Version} " +
            $"os={Environment.OSVersion} runtime={Environment.Version} " +
            $"plainWindow={AppEnvironment.PlainWindow} args=[{string.Join(" ", args)}]");

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            var exception = eventArgs.ExceptionObject as Exception;
            var path = CrashReporter.Report("AppDomain.UnhandledException", exception);
            if (eventArgs.IsTerminating)
            {
                ShowCrashDialog(path, exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            CrashReporter.Report("UnobservedTaskException", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            if (args.Any(argument => argument.Equals("--diagnose", StringComparison.OrdinalIgnoreCase)))
            {
                return Diagnostics.Run();
            }

            return RunTray();
        }
        catch (Exception exception)
        {
            var path = CrashReporter.Report("Main", exception);
            ShowCrashDialog(path, exception);
            return 1;
        }
    }

    private static int RunTray()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance();
            return 0;
        }

        using var showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowEventName);

        // Only the context menu and NotifyIcon come from WinForms; visual styles must be enabled
        // before the first WinForms control (the menu) is created.
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

        var application = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
        };

        application.DispatcherUnhandledException += (_, eventArgs) =>
        {
            var path = CrashReporter.Report("DispatcherUnhandledException", eventArgs.Exception);
            ShowCrashDialog(path, eventArgs.Exception);
            eventArgs.Handled = true;
        };

        using var host = new TrayHost(showEvent);
        host.Start();

        AppLog.Info("tray: entering message loop");
        application.Run();
        AppLog.Info("tray: message loop exited");
        host.Stop();
        return 0;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var handle))
            {
                using (handle)
                {
                    handle.Set();
                }
            }
        }
        catch
        {
            // The other instance may be shutting down.
        }
    }

    private static void ShowCrashDialog(string reportPath, Exception? exception)
    {
        if (Interlocked.Exchange(ref _crashDialogShown, 1) != 0)
        {
            return;
        }

        try
        {
            var detail = exception is null
                ? "(no exception object)"
                : $"{exception.GetType().FullName}: {exception.Message}";

            System.Windows.MessageBox.Show(
                "OpenCode Usage Plus hit an unexpected error.\n\n" +
                $"A report was written to:\n{reportPath}\n\n{detail}",
                "OpenCode Usage Plus",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Nothing else we can do.
        }
    }
}
