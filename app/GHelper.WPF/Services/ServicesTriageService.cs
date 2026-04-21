using System.Management;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace GHelper.WPF.Services
{
    public enum ServiceStartMode
    {
        Auto,
        AutoDelayed,
        Manual,
        Disabled,
    }

    /// <summary>
    /// One Windows service we're willing to surface to the user — i.e. a
    /// non-system-path service with a start mode of Automatic, Automatic
    /// (Delayed), or Manual. <see cref="IsAutoStart"/> and <see cref="IsRunning"/>
    /// are observable so toggles reflect state after a write.
    /// </summary>
    public partial class ServiceEntry : ObservableObject
    {
        public string Name { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? Description { get; init; }
        public string? PathName { get; init; }

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAutoStart))]
        private ServiceStartMode _startMode;

        /// <summary>
        /// Surfaced to the toggle UI. Auto and AutoDelayed both count as "on";
        /// flipping off drops to Manual (preserves the ability to start on
        /// demand without us having to reason about Disabled).
        /// </summary>
        public bool IsAutoStart => StartMode is ServiceStartMode.Auto or ServiceStartMode.AutoDelayed;
    }

    /// <summary>
    /// Enumerates non-Microsoft Windows services and lets the user switch them
    /// between Automatic and Manual start. Start/Stop is available too.
    ///
    /// Filtering: we hide anything whose binary lives under %SystemRoot%\ (almost
    /// always Windows itself) and anything with StartMode=Disabled (already off;
    /// re-enabling a disabled service is a services.msc power-user move, not a
    /// triage action). We never write Disabled — it's too easy to break a
    /// vendor's install by making a critical service uncoverable.
    ///
    /// Uses WMI (Win32_Service) for both reads and writes. Writes go through
    /// InvokeMethod — the same API services.msc uses under the covers.
    /// </summary>
    public static class ServicesTriageService
    {
        /// <summary>
        /// Human-readable failure reason from the most recent operation. Null
        /// after success. Read immediately after a failed call — subsequent
        /// operations overwrite it.
        /// </summary>
        public static string? LastError { get; private set; }


        /// <summary>
        /// Enumerate user-relevant services. Expensive on machines with many
        /// services (WMI query + path filtering) — call from a background thread.
        /// </summary>
        public static List<ServiceEntry> Scan()
        {
            var results = new List<ServiceEntry>();
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DisplayName, Description, PathName, State, StartMode, DelayedAutoStart FROM Win32_Service");
                using var collection = searcher.Get();

                foreach (ManagementObject svc in collection)
                {
                    try
                    {
                        var entry = TryBuildEntry(svc, systemRoot);
                        if (entry != null) results.Add(entry);
                    }
                    catch
                    {
                        // Some services deny reads. Skip silently.
                    }
                    finally
                    {
                        svc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ServicesTriageService.Scan error: " + ex.Message);
            }

            return results
                .OrderBy(e => e.IsAutoStart ? 0 : 1)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ServiceEntry? TryBuildEntry(ManagementObject svc, string systemRoot)
        {
            string name = svc["Name"]?.ToString() ?? "";
            string displayName = svc["DisplayName"]?.ToString() ?? name;
            string? description = svc["Description"]?.ToString();
            string? pathName = svc["PathName"]?.ToString();
            string state = svc["State"]?.ToString() ?? "";
            string startMode = svc["StartMode"]?.ToString() ?? "";
            bool delayed = svc["DelayedAutoStart"] is bool d && d;

            // Filter out Windows-owned services by path — the binary's location
            // is the cleanest signal and WMI hands us the full command line.
            // PathName is quoted and often has args, so extract just the exe path.
            string? exePath = ExtractExePath(pathName);
            if (string.IsNullOrEmpty(exePath)) return null;
            if (exePath.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase)) return null;

            // Skip Disabled — re-enabling is a services.msc power move, not triage.
            var mode = ParseStartMode(startMode, delayed);
            if (mode == ServiceStartMode.Disabled) return null;

            return new ServiceEntry
            {
                Name = name,
                DisplayName = displayName,
                Description = description,
                PathName = exePath,
                IsRunning = state.Equals("Running", StringComparison.OrdinalIgnoreCase),
                StartMode = mode,
            };
        }

        /// <summary>
        /// Flip the start mode. Only Auto/Manual are valid here — we refuse
        /// Disabled to keep the triage action non-destructive.
        /// </summary>
        public static bool SetStartMode(ServiceEntry entry, ServiceStartMode mode)
        {
            LastError = null;
            if (mode == ServiceStartMode.Disabled)
            {
                LastError = "Refused: setting to Disabled isn't supported here";
                Logger.WriteLine($"ServicesTriageService: refusing to set {entry.Name} to Disabled");
                return false;
            }

            try
            {
                using var svc = new ManagementObject($"Win32_Service.Name='{entry.Name}'");

                // WMI's ChangeStartMode only accepts "Automatic", "Manual", or
                // "Disabled" — there's no direct "AutoDelayed" value. For Delayed
                // start we write Automatic here, then flip the DelayedAutoStart
                // registry bit separately via ChangeStartMode doesn't set it.
                // For simplicity, Auto from the UI always means non-delayed.
                string wmiMode = mode switch
                {
                    ServiceStartMode.Auto => "Automatic",
                    ServiceStartMode.AutoDelayed => "Automatic",
                    ServiceStartMode.Manual => "Manual",
                    _ => "Manual",
                };

                using var args = svc.GetMethodParameters("ChangeStartMode");
                args["StartMode"] = wmiMode;
                using var result = svc.InvokeMethod("ChangeStartMode", args, null);
                uint ret = ReadReturnValue(result);
                if (ret != 0)
                {
                    // Access denied (2) is common for vendor-hardened services whose
                    // SCM DACL restricts SERVICE_CHANGE_CONFIG. The underlying
                    // registry key is usually still writable by admins, so fall
                    // back to writing the Start value directly.
                    if (ret == 2 && TrySetStartModeViaRegistry(entry.Name, mode))
                    {
                        entry.StartMode = mode;
                        Logger.WriteLine($"ServicesTriageService.SetStartMode({entry.Name}) WMI denied — registry fallback succeeded");
                        return true;
                    }

                    LastError = DescribeReturnCode(ret);
                    Logger.WriteLine($"ServicesTriageService.SetStartMode({entry.Name}) WMI returned {ret} ({LastError})");
                    return false;
                }

                entry.StartMode = mode;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.WriteLine($"ServicesTriageService.SetStartMode({entry.Name}) error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Stop a running service. Returns true on success. Logs + returns false
        /// if the service refuses (has dependents, locked by SCM, etc.).
        /// </summary>
        public static bool StopService(ServiceEntry entry)
        {
            LastError = null;
            try
            {
                using var svc = new ManagementObject($"Win32_Service.Name='{entry.Name}'");
                using var result = svc.InvokeMethod("StopService", null, null);
                uint ret = ReadReturnValue(result);
                if (ret != 0)
                {
                    LastError = DescribeReturnCode(ret);
                    Logger.WriteLine($"ServicesTriageService.StopService({entry.Name}) WMI returned {ret} ({LastError})");
                    return false;
                }
                entry.IsRunning = false;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.WriteLine($"ServicesTriageService.StopService({entry.Name}) error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Start a stopped service. Returns true on success. Useful to kick off
        /// a Manual-start service without leaving the app for services.msc.
        /// </summary>
        public static bool StartService(ServiceEntry entry)
        {
            LastError = null;
            try
            {
                using var svc = new ManagementObject($"Win32_Service.Name='{entry.Name}'");
                using var result = svc.InvokeMethod("StartService", null, null);
                uint ret = ReadReturnValue(result);
                if (ret != 0)
                {
                    LastError = DescribeReturnCode(ret);
                    Logger.WriteLine($"ServicesTriageService.StartService({entry.Name}) WMI returned {ret} ({LastError})");
                    return false;
                }
                entry.IsRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.WriteLine($"ServicesTriageService.StartService({entry.Name}) error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// WMI return values come back boxed. Accept both UInt32 and Int32 just
        /// in case the provider varies. Missing result = treat as failure.
        /// </summary>
        private static uint ReadReturnValue(ManagementBaseObject? result)
        {
            if (result == null) return 1;
            var rv = result["ReturnValue"];
            return rv switch
            {
                uint u => u,
                int i => (uint)i,
                _ => 1,
            };
        }

        /// <summary>
        /// Human-readable names for the subset of Win32_Service method return
        /// codes we're most likely to see. Full list in MS docs — we map the
        /// common ones and fall through to the raw number otherwise.
        /// </summary>
        private static string DescribeReturnCode(uint code) => code switch
        {
            0 => "Success",
            2 => "Access denied",
            3 => "Dependent services running",
            5 => "Service cannot accept control",
            6 => "Service has not been started",
            7 => "Service request timeout",
            8 => "Unknown failure",
            9 => "Path not found",
            10 => "Service already running",
            11 => "Service database locked",
            14 => "Service disabled",
            15 => "Service logon failure",
            16 => "Service marked for deletion",
            21 => "Invalid parameter",
            _ => $"code {code}",
        };

        /// <summary>
        /// Fallback for SCM access-denied: write Start directly in the service's
        /// registry key. Start values: 2=Automatic, 3=Manual, 4=Disabled. Takes
        /// effect at next boot (service control by SCM can still refuse runtime
        /// operations on this service, but auto-start is now correct).
        /// </summary>
        private static bool TrySetStartModeViaRegistry(string serviceName, ServiceStartMode mode)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
                if (key == null) return false;

                int startValue = mode switch
                {
                    ServiceStartMode.Auto or ServiceStartMode.AutoDelayed => 2,
                    ServiceStartMode.Manual => 3,
                    _ => 3,
                };
                key.SetValue("Start", startValue, RegistryValueKind.DWord);

                // DelayedAutostart is only meaningful when Start=2; write 0/1
                // accordingly. Missing value = not delayed, which is our Auto default.
                int delayed = mode == ServiceStartMode.AutoDelayed ? 1 : 0;
                key.SetValue("DelayedAutostart", delayed, RegistryValueKind.DWord);
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"ServicesTriageService: registry fallback failed for {serviceName}: {ex.Message}");
                return false;
            }
        }

        private static ServiceStartMode ParseStartMode(string raw, bool delayed) => raw.ToLowerInvariant() switch
        {
            "auto" => delayed ? ServiceStartMode.AutoDelayed : ServiceStartMode.Auto,
            "manual" => ServiceStartMode.Manual,
            "disabled" => ServiceStartMode.Disabled,
            _ => ServiceStartMode.Manual,
        };

        /// <summary>
        /// WMI's PathName is the full command line — quoted exe + args. Extract
        /// just the exe path so we can location-filter cleanly.
        /// </summary>
        private static string? ExtractExePath(string? pathName)
        {
            if (string.IsNullOrWhiteSpace(pathName)) return null;
            pathName = pathName.Trim();

            if (pathName.StartsWith("\""))
            {
                int end = pathName.IndexOf('"', 1);
                return end > 1 ? pathName.Substring(1, end - 1) : pathName;
            }

            int space = pathName.IndexOf(' ');
            return space > 0 ? pathName.Substring(0, space) : pathName;
        }
    }
}
