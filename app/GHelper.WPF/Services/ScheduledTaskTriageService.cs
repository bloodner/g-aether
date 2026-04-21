using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32.TaskScheduler;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// One Task Scheduler entry we're willing to expose to the user — i.e.
    /// non-Microsoft tasks that trigger at boot or logon. <see cref="IsEnabled"/>
    /// is observable so the toggle button reflects state after a write.
    /// </summary>
    public partial class ScheduledTaskEntry : ObservableObject
    {
        public string Name { get; init; } = "";

        /// <summary>Absolute path in Task Scheduler, e.g. "\Adobe\AdobeGCInvoker-1.0".</summary>
        public string FullPath { get; init; } = "";

        /// <summary>"At logon", "At boot", or "Logon + boot" for display.</summary>
        public string TriggerLabel { get; init; } = "";

        /// <summary>The command line or script the task will run (truncated for display if needed).</summary>
        public string? Action { get; init; }

        public string Author { get; init; } = "";

        [ObservableProperty]
        private bool _isEnabled;

        /// <summary>Folder that owns this task, e.g. "\Adobe" or "" for the root. Shown alongside the name.</summary>
        public string Folder
        {
            get
            {
                int slash = FullPath.LastIndexOf('\\');
                return slash > 0 ? FullPath.Substring(0, slash) : "\\";
            }
        }
    }

    /// <summary>
    /// Scans Windows Task Scheduler for third-party boot/logon tasks and lets
    /// the user disable/enable them. Filters out Microsoft-owned tasks so we
    /// never expose something that toggling off could break Windows itself.
    ///
    /// Tasks are identified by their absolute path (folder + name), which
    /// Task Scheduler uses as the unique key. Toggle writes go through the
    /// managed TaskService library — same one the existing Startup.cs uses.
    /// </summary>
    public static class ScheduledTaskTriageService
    {
        /// <summary>
        /// Enumerate user-relevant boot/logon tasks. Expensive on machines with
        /// many tasks — call from a background thread.
        /// </summary>
        public static List<ScheduledTaskEntry> Scan()
        {
            var results = new List<ScheduledTaskEntry>();
            try
            {
                using var ts = new TaskService();
                WalkFolder(ts.RootFolder, results);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ScheduledTaskTriageService.Scan error: " + ex.Message);
            }

            return results
                .OrderBy(e => e.IsEnabled ? 0 : 1)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void WalkFolder(TaskFolder folder, List<ScheduledTaskEntry> into)
        {
            // Skip Microsoft's own tree wholesale — flipping things off in there
            // breaks Windows (update checks, telemetry, driver housekeeping, etc.).
            string folderPath = folder.Path ?? "";
            if (folderPath.StartsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var task in folder.Tasks)
            {
                try
                {
                    var entry = TryBuildEntry(task);
                    if (entry != null) into.Add(entry);
                }
                catch
                {
                    // Some tasks deny reads (ACL-restricted). Skip silently.
                }
                finally
                {
                    task.Dispose();
                }
            }

            foreach (var sub in folder.SubFolders)
            {
                WalkFolder(sub, into);
            }
        }

        private static ScheduledTaskEntry? TryBuildEntry(Microsoft.Win32.TaskScheduler.Task task)
        {
            var def = task.Definition;
            if (def.Settings.Hidden) return null;

            // Hide G-Aether's own tasks (autostart + boot-time charge-limit).
            // Users manage "Run on Startup" from Settings; the charge-limit task
            // is an internal dependency that shouldn't be disabled here. Power
            // users can still see/edit them in taskschd.msc.
            if (task.Name.Equals("GHelper", StringComparison.OrdinalIgnoreCase) ||
                task.Name.Equals("GHelperCharge", StringComparison.OrdinalIgnoreCase))
                return null;

            // Must trigger on boot or logon — other triggers (daily, idle, on event)
            // are someone else's problem.
            bool atBoot = false, atLogon = false;
            foreach (var trigger in def.Triggers)
            {
                if (trigger is BootTrigger) atBoot = true;
                if (trigger is LogonTrigger) atLogon = true;
            }
            if (!atBoot && !atLogon) return null;

            string author = def.RegistrationInfo.Author ?? "";
            // Belt-and-braces: even outside the \Microsoft\ tree, skip tasks
            // explicitly authored by Microsoft.
            if (author.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                return null;

            string triggerLabel = (atBoot, atLogon) switch
            {
                (true, true) => "Boot + Logon",
                (true, false) => "At boot",
                (false, true) => "At logon",
                _ => "",
            };

            string? action = null;
            if (def.Actions.Count > 0)
            {
                // ToString() on ExecAction gives "path arguments" which is readable enough.
                action = def.Actions[0].ToString()?.Trim();
                if (!string.IsNullOrEmpty(action) && action.Length > 200)
                    action = action.Substring(0, 197) + "...";
            }

            return new ScheduledTaskEntry
            {
                Name = task.Name,
                FullPath = task.Path,
                TriggerLabel = triggerLabel,
                Action = action,
                Author = author,
                IsEnabled = task.Enabled,
            };
        }

        /// <summary>
        /// Toggle the task's Enabled flag. Returns true on success.
        /// Task Scheduler writes require admin; the app runs elevated
        /// (requireAdministrator manifest) so this normally succeeds.
        /// </summary>
        public static bool SetEnabled(ScheduledTaskEntry entry, bool enabled)
        {
            try
            {
                using var ts = new TaskService();
                var task = ts.GetTask(entry.FullPath);
                if (task == null)
                {
                    Logger.WriteLine($"ScheduledTaskTriageService: task not found {entry.FullPath}");
                    return false;
                }
                task.Enabled = enabled;
                entry.IsEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"ScheduledTaskTriageService.SetEnabled({entry.FullPath}) error: " + ex.Message);
                return false;
            }
        }
    }
}
