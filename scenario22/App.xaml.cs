using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Scenario22.Services;

namespace Scenario22;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _mutex = new Mutex(true, "scenario22.SingleInstance", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            TryActivateRunningInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var settingsService = new SettingsService();
        var state = settingsService.Load();
        StartupService.CleanupLegacy();
        ThemeService.Apply(state.Preferences.UseDarkTheme);

        if (state.Preferences.StartWithWindows)
        {
            StartupService.SetEnabled(true);
        }

        var startHidden = e.Args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        var window = new MainWindow(state, settingsService, startHidden);
        MainWindow = window;

        if (settingsService.LastLoadWarning is not null)
        {
            window.QueueLoadWarning(settingsService.LastLoadWarning);
        }

        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLogCrash(e.Exception);
        System.Windows.MessageBox.Show(
            Strings.T("L_CrashText") + "\n\n" + e.Exception.Message,
            "scenario22",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void TryActivateRunningInstance()
    {
        try
        {
            var message = NativeMethods.RegisterWindowMessage("Scenario22.ActivateWindow");
            var handle = IntPtr.Zero;
            for (var attempt = 0; attempt < 12 && handle == IntPtr.Zero; attempt++)
            {
                handle = NativeMethods.FindWindow(null, "scenario22");
                if (handle == IntPtr.Zero)
                {
                    Thread.Sleep(80);
                }
            }

            if (handle != IntPtr.Zero)
            {
                NativeMethods.PostMessage(handle, message, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(handle);
            }
        }
        catch
        {
            // If activation fails, just exit the second instance.
        }
    }

    private static void TryLogCrash(Exception exception)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "scenario22");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "crash.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // The mutex may not be owned if startup failed early.
            }
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }
}
