using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace GHelper.WPF.Services
{
    public enum StartupSource
    {
        HkcuRun,
        HklmRun,
        UserStartupFolder,
        CommonStartupFolder,
    }

    /// <summary>
    /// One autostart entry. <see cref="IsEnabled"/> is observable so the
    /// toggle control reflects state changes after a write.
    /// </summary>
    public partial class StartupEntry : ObservableObject
    {
        /// <summary>Registry value name for Run entries; .lnk filename for folder entries.</summary>
        public string Name { get; init; } = "";

        /// <summary>Path/command line for display (registry value data, or .lnk target).</summary>
        public string? Path { get; init; }

        public StartupSource Source { get; init; }

        [ObservableProperty]
        private bool _isEnabled;

        /// <summary>Short human-readable label for the Source column.</summary>
        public string SourceLabel => Source switch
        {
            StartupSource.HkcuRun => "Registry (user)",
            StartupSource.HklmRun => "Registry (system)",
            StartupSource.UserStartupFolder => "Startup folder (user)",
            StartupSource.CommonStartupFolder => "Startup folder (system)",
            _ => Source.ToString(),
        };
    }

    /// <summary>
    /// Reads and toggles Windows autostart entries from four sources: per-user
    /// and system Run registry keys, and per-user and common Startup folders.
    ///
    /// Enabled/disabled state lives in the <c>StartupApproved</c> subtree
    /// Windows itself uses — same mechanism as Task Manager and Settings →
    /// Apps → Startup. Toggling here never deletes the original registry
    /// value or .lnk file; it flips a single byte in StartupApproved so the
    /// user can re-enable an app later exactly as it was.
    /// </summary>
    public static class StartupTriageService
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedFolderPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        /// <summary>Enumerate all currently-tracked autostart entries.</summary>
        public static List<StartupEntry> Scan()
        {
            var entries = new List<StartupEntry>();

            // Registry Run keys (HKCU + HKLM)
            AddRunEntries(entries, Registry.CurrentUser, StartupSource.HkcuRun);
            AddRunEntries(entries, Registry.LocalMachine, StartupSource.HklmRun);

            // Startup folders (user + common)
            AddFolderEntries(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserStartupFolder);
            AddFolderEntries(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonStartupFolder);

            return entries
                .OrderBy(e => e.IsEnabled ? 0 : 1)  // enabled first
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Toggle an entry's enabled state by writing to StartupApproved.
        /// Returns true on success, false (with log) if the write failed —
        /// typically due to non-elevated access on HKLM sources.
        /// </summary>
        public static bool SetEnabled(StartupEntry entry, bool enabled)
        {
            try
            {
                var hive = entry.Source is StartupSource.HklmRun ? Registry.LocalMachine : Registry.CurrentUser;
                string approvedPath = entry.Source switch
                {
                    StartupSource.UserStartupFolder or StartupSource.CommonStartupFolder => ApprovedFolderPath,
                    _ => ApprovedRunPath,
                };

                using var key = hive.CreateSubKey(approvedPath, writable: true);
                if (key == null) return false;

                // 12-byte marker. First byte: 0x02 = enabled, 0x03 = disabled. Remaining
                // bytes are a FILETIME timestamp — Windows writes the current time there
                // but tolerates zeros, which keeps the write simple and audit-friendly.
                byte[] data = new byte[12];
                data[0] = enabled ? (byte)0x02 : (byte)0x03;

                key.SetValue(entry.Name, data, RegistryValueKind.Binary);
                entry.IsEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"StartupTriageService.SetEnabled({entry.Name}) error: " + ex.Message);
                return false;
            }
        }

        // --- Registry Run enumeration ----------------------------------------

        private static void AddRunEntries(List<StartupEntry> into, RegistryKey hive, StartupSource source)
        {
            try
            {
                using var run = hive.OpenSubKey(RunPath, writable: false);
                if (run == null) return;

                // Approved lives in the same hive as the Run key it describes, except
                // for HKLM entries which some versions of Windows still track in HKCU.
                // We check both and prefer hive-local if present.
                byte[]? approvedMarker;
                using var approvedLocal = hive.OpenSubKey(ApprovedRunPath, writable: false);
                using var approvedHkcu = source == StartupSource.HklmRun
                    ? Registry.CurrentUser.OpenSubKey(ApprovedRunPath, writable: false)
                    : null;

                foreach (var name in run.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    string path = run.GetValue(name)?.ToString() ?? "";

                    approvedMarker = approvedLocal?.GetValue(name) as byte[]
                                     ?? approvedHkcu?.GetValue(name) as byte[];
                    bool enabled = IsMarkerEnabled(approvedMarker);

                    into.Add(new StartupEntry
                    {
                        Name = name,
                        Path = path,
                        Source = source,
                        IsEnabled = enabled,
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Startup scan ({source}) error: " + ex.Message);
            }
        }

        // --- Startup folder enumeration --------------------------------------

        private static void AddFolderEntries(List<StartupEntry> into, string folder, StartupSource source)
        {
            try
            {
                if (!Directory.Exists(folder)) return;

                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedFolderPath, writable: false);

                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    string name = System.IO.Path.GetFileName(file);
                    byte[]? marker = approved?.GetValue(name) as byte[];

                    into.Add(new StartupEntry
                    {
                        Name = name,
                        Path = file,
                        Source = source,
                        IsEnabled = IsMarkerEnabled(marker),
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Startup folder scan ({source}) error: " + ex.Message);
            }
        }

        /// <summary>
        /// Missing marker = enabled (the default for brand-new entries Windows
        /// hasn't tracked yet). 0x02 = enabled, 0x03 = disabled. Anything else
        /// we interpret as enabled so we don't hide legitimate entries by
        /// misreading an unusual marker.
        /// </summary>
        private static bool IsMarkerEnabled(byte[]? marker)
        {
            if (marker == null || marker.Length == 0) return true;
            return marker[0] != 0x03;
        }
    }
}
