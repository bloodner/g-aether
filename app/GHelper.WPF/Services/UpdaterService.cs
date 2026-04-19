using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Coordinates the two-stage in-place update:
    ///
    ///  1. In the *running* app: download the new exe to %TEMP%, then
    ///     <see cref="LaunchInstaller"/> spawns it with <c>--install-over</c> args
    ///     and exits the current process.
    ///
    ///  2. In the *new* exe: <see cref="PerformInstallOver"/> (invoked from
    ///     App.OnStartup on detection of the arg) waits for the old PID to exit,
    ///     copies itself onto the target path, and relaunches from that path.
    /// </summary>
    public static class UpdaterService
    {
        public const string InstallOverArg = "--install-over";
        public const string PidArg = "--pid";

        /// <summary>
        /// Launches <paramref name="downloadedExePath"/> with install-over args so it can
        /// replace <paramref name="targetExePath"/> after the current process exits.
        /// Returns false if the spawn failed — the caller should fall back to the browser.
        /// </summary>
        public static bool LaunchInstaller(string downloadedExePath, string targetExePath)
        {
            if (!File.Exists(downloadedExePath))
            {
                Logger.WriteLine("LaunchInstaller: downloaded exe not found at " + downloadedExePath);
                return false;
            }

            try
            {
                int pid = Process.GetCurrentProcess().Id;
                var psi = new ProcessStartInfo
                {
                    FileName = downloadedExePath,
                    UseShellExecute = true,  // lets Windows honor the target's requireAdministrator manifest
                    WorkingDirectory = Path.GetDirectoryName(downloadedExePath) ?? "",
                };
                psi.ArgumentList.Add(InstallOverArg);
                psi.ArgumentList.Add(targetExePath);
                psi.ArgumentList.Add(PidArg);
                psi.ArgumentList.Add(pid.ToString());

                Process.Start(psi);
                Logger.WriteLine($"Installer launched: {downloadedExePath} -> {targetExePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("LaunchInstaller failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Handles the install-over handshake. Returns true if args matched and the
        /// swap was attempted (caller should exit immediately after). Returns false if
        /// the args weren't for us — caller continues normal startup.
        /// </summary>
        public static bool TryPerformInstallOver(string[] args)
        {
            if (args == null || args.Length < 2) return false;
            if (!string.Equals(args[0], InstallOverArg, StringComparison.Ordinal)) return false;

            string targetPath = args[1];
            int pid = -1;
            for (int i = 2; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], PidArg, StringComparison.Ordinal))
                {
                    int.TryParse(args[i + 1], out pid);
                    break;
                }
            }

            try
            {
                WaitForProcessExit(pid, TimeSpan.FromSeconds(20));
                CopyWithRetry(GetRunningExePath(), targetPath);
                Logger.WriteLine($"Update installed at {targetPath}. Relaunching.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(targetPath) ?? "",
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Install-over failed: " + ex.Message);
                // Fall back: open the target path directly if the swap failed but the old exe still exists.
                try
                {
                    if (File.Exists(targetPath))
                        Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                }
                catch { }
            }

            return true;
        }

        private static string GetRunningExePath()
        {
            // Single-file publish: Assembly.Location is empty, but Process.MainModule is reliable.
            var mm = Process.GetCurrentProcess().MainModule;
            if (mm?.FileName is { Length: > 0 } p) return p;
            return Assembly.GetEntryAssembly()?.Location ?? "";
        }

        private static void WaitForProcessExit(int pid, TimeSpan timeout)
        {
            if (pid <= 0) return;
            try
            {
                var proc = Process.GetProcessById(pid);
                if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
                    Logger.WriteLine($"Update: old process {pid} didn't exit within {timeout.TotalSeconds}s — proceeding anyway");
            }
            catch (ArgumentException)
            {
                // Already exited — that's what we want.
            }
        }

        private static void CopyWithRetry(string source, string target)
        {
            const int maxAttempts = 10;
            Exception? last = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Copy(source, target, overwrite: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    last = ex;
                    Thread.Sleep(500);  // back off for AV scanners or lingering handles
                }
            }
            throw new IOException($"Failed to replace {target} after {maxAttempts} attempts", last);
        }
    }
}
