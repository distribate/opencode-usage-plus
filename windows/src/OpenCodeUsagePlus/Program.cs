using System;
using System.Threading;
using OpenCodeUsagePlus.Core.Storage;
using OpenCodeUsagePlus.Tray;

namespace OpenCodeUsagePlus;

internal static class Program
{
    private const string MutexName = "OpenCodeUsagePlus.SingleInstance";
    private const string ShowEventName = "OpenCodeUsagePlus.Show";

    [STAThread]
    private static int Main(string[] args)
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
            AppLog.Error("unhandled dispatcher exception", eventArgs.Exception);
            eventArgs.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AppLog.Error("unhandled exception", eventArgs.ExceptionObject as Exception);

        using var host = new TrayHost(showEvent);
        host.Start();

        application.Run();
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
}
