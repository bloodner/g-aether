using System.IO;
using System.Windows;
using System.Windows.Threading;
using GHelper.WPF.Services;

namespace GHelper.WPF
{
    public partial class App : Application
    {
        private static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "G-Aether-crash.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Install-over handshake runs before any other init. If we were launched to
            // replace the old exe, copy ourselves over the target and relaunch from there.
            if (UpdaterService.TryPerformInstallOver(e.Args))
            {
                Shutdown();
                return;
            }

            // First-run relocation: if we're running from Downloads or %TEMP%, copy
            // ourselves to %LocalAppData%\Programs\G-Aether, relaunch from there,
            // and exit. Keeps the Run-on-Startup task anchored to a stable path.
            if (FirstRunRelocator.TryRelocate(e.Args))
            {
                Shutdown();
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                AppHost.Initialize();
            }
            catch (Exception ex)
            {
                WriteCrashLog("Startup failed", ex);
                MessageBox.Show(
                    $"G-Aether failed to start:\n\n{ex.Message}\n\nA detailed log has been saved to your Desktop:\n{CrashLogPath}",
                    "G-Aether - Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.WriteLine("UI Exception: " + e.Exception.ToString());
            WriteCrashLog("UI Exception", e.Exception);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.WriteLine("FATAL Domain Exception (isTerminating=" + e.IsTerminating + "): " + ex.ToString());
                WriteCrashLog("Domain Exception (isTerminating=" + e.IsTerminating + ")", ex);
            }
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            Logger.WriteLine("Process exiting (Environment.ExitCode=" + Environment.ExitCode + ")");
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.WriteLine("Task Exception: " + e.Exception?.ToString());
            if (e.Exception != null)
                WriteCrashLog("Task Exception", e.Exception);
            e.SetObserved();
        }

        private static void WriteCrashLog(string context, Exception ex)
        {
            try
            {
                string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n"
                    + $"Machine: {Environment.MachineName}\n"
                    + $"OS: {Environment.OSVersion}\n"
                    + $"x64: {Environment.Is64BitOperatingSystem}\n"
                    + $"Exception: {ex}\n"
                    + new string('-', 80) + "\n";
                File.AppendAllText(CrashLogPath, log);
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppHost.Shutdown();
            base.OnExit(e);
        }
    }
}
