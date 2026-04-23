using System.Diagnostics;
using System.IO;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// On first launch from a throwaway location (Downloads, Temp), copies the
    /// running exe to a stable per-user install path, relaunches from there,
    /// and exits. Keeps the Run-on-Startup task from being anchored to a
    /// folder the user is likely to clean out.
    ///
    /// Runs after the updater handshake and before anything else in
    /// App.OnStartup. Because the app manifest already requires admin, the
    /// launching process is elevated — the relaunched child inherits that
    /// and Windows does not show a second UAC prompt.
    /// </summary>
    public static class FirstRunRelocator
    {
        public const string RelocatedArg = "--relocated";
        public const string PortableArg = "--portable";
        public const string PortableSentinelFile = ".portable";

        private static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "G-Aether");

        private static readonly string InstallExe = Path.Combine(InstallDir, "G-Aether.exe");

        /// <summary>
        /// Returns true if relocation happened and the caller should Shutdown().
        /// Returns false (and lets startup continue normally) in every other case,
        /// including failures — a broken relocation shouldn't block the user from
        /// using the app in place.
        /// </summary>
        public static bool TryRelocate(string[] args)
        {
            // Sentinel: we relaunched from InstallExe; don't recurse.
            if (args.Contains(RelocatedArg)) return false;

            // Explicit portable opt-out via CLI flag.
            if (args.Contains(PortableArg)) return false;

            string current = GetRunningExePath();
            if (string.IsNullOrEmpty(current)) return false;

            // Already running from the stable path — nothing to do.
            if (PathEquals(current, InstallExe)) return false;

            // Portable sentinel file next to the exe — respect power users.
            string? currentDir = Path.GetDirectoryName(current);
            if (currentDir != null && File.Exists(Path.Combine(currentDir, PortableSentinelFile)))
                return false;

            // Only relocate from locations a user is likely to wipe.
            if (!IsThrowawayLocation(current)) return false;

            try
            {
                Directory.CreateDirectory(InstallDir);

                if (ShouldOverwrite(current, InstallExe))
                {
                    CopyWithRetry(current, InstallExe);
                    Logger.WriteLine($"Relocator: copied {current} -> {InstallExe}");
                }
                else
                {
                    Logger.WriteLine($"Relocator: installed copy at {InstallExe} is newer, launching it.");
                }

                TryCreateStartMenuShortcut();

                Process.Start(new ProcessStartInfo
                {
                    FileName = InstallExe,
                    UseShellExecute = true,
                    WorkingDirectory = InstallDir,
                    ArgumentList = { RelocatedArg },
                });
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Relocator failed, continuing from current location: " + ex.Message);
                return false;
            }
        }

        private static bool IsThrowawayLocation(string exePath)
        {
            string dir = Path.GetDirectoryName(exePath) ?? "";
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string userTemp = Path.GetTempPath();
            return IsUnder(dir, downloads) || IsUnder(dir, userTemp);
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            return full.Equals(r, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathEquals(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Overwrite when the running exe is same-or-newer than the one already
        /// installed. If the installed copy is newer (user re-downloaded an old
        /// build), don't clobber it — just launch what's there.
        /// </summary>
        private static bool ShouldOverwrite(string source, string target)
        {
            if (!File.Exists(target)) return true;
            try
            {
                var s = FileVersionInfo.GetVersionInfo(source);
                var t = FileVersionInfo.GetVersionInfo(target);
                var sv = new Version(s.FileMajorPart, s.FileMinorPart, s.FileBuildPart, s.FilePrivatePart);
                var tv = new Version(t.FileMajorPart, t.FileMinorPart, t.FileBuildPart, t.FilePrivatePart);
                return sv >= tv;
            }
            catch
            {
                return true;
            }
        }

        // Same retry pattern UpdaterService uses — AV scanners and lingering handles.
        private static void CopyWithRetry(string source, string target)
        {
            const int maxAttempts = 10;
            Exception? last = null;
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    File.Copy(source, target, overwrite: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    last = ex;
                    Thread.Sleep(500);
                }
            }
            throw new IOException($"Failed to copy to {target} after {maxAttempts} attempts", last);
        }

        private static string GetRunningExePath()
        {
            // Single-file publish: Assembly.Location is empty; MainModule.FileName is reliable.
            var mm = Process.GetCurrentProcess().MainModule;
            return mm?.FileName ?? "";
        }

        /// <summary>
        /// Creates (or refreshes) a Start Menu shortcut pointing at the installed
        /// exe. Idempotent — overwriting an identical .lnk is a no-op to the user.
        /// Uses WScript.Shell COM via dynamic so we don't take a reference on
        /// IWshRuntimeLibrary. Failure here is non-fatal; relocation still succeeds.
        /// </summary>
        private static void TryCreateStartMenuShortcut()
        {
            try
            {
                string startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenuPrograms, "G-Aether.lnk");

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    Logger.WriteLine("Relocator: WScript.Shell unavailable, skipping shortcut.");
                    return;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = InstallExe;
                shortcut.WorkingDirectory = InstallDir;
                shortcut.Description = "G-Aether";
                shortcut.IconLocation = InstallExe + ",0";
                shortcut.Save();

                Logger.WriteLine($"Relocator: Start Menu shortcut at {shortcutPath}");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Relocator: shortcut creation failed (non-fatal): " + ex.Message);
            }
        }
    }
}
