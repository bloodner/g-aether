using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Observable row in the Processes panel. CPU % and RAM are mutable so the
    /// UI updates smoothly on each refresh without rebuilding the whole list
    /// (which would kill scroll position and cause flicker).
    /// </summary>
    public partial class ProcessInfo : ObservableObject
    {
        public int Pid { get; init; }
        public string Name { get; init; } = "";
        public string? ExePath { get; init; }

        [ObservableProperty]
        private double _cpuPercent;

        [ObservableProperty]
        private string _cpuDisplay = "—";

        [ObservableProperty]
        private long _workingSetBytes;

        [ObservableProperty]
        private string _ramDisplay = "";

        [ObservableProperty]
        private BitmapSource? _icon;

        // Per-process state used to compute CPU % between refreshes.
        internal TimeSpan LastCpuTime { get; set; }
        internal DateTime LastSampleAt { get; set; }
        internal bool HasBaseline { get; set; }
    }

    /// <summary>
    /// Enumerates user-visible processes and keeps an observable collection in
    /// sync across refreshes. Maintains per-process CPU-time baselines so CPU %
    /// reflects live usage between samples (same math Task Manager uses).
    /// </summary>
    public class ProcessMonitor
    {
        private readonly ObservableCollection<ProcessInfo> _processes = new();
        private readonly Dictionary<int, ProcessInfo> _byPid = new();
        private readonly Dictionary<string, BitmapSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _ownPid = Process.GetCurrentProcess().Id;

        public ObservableCollection<ProcessInfo> Processes => _processes;

        /// <summary>Enumerate once, merge into the observable collection.</summary>
        public void Refresh()
        {
            var now = DateTime.UtcNow;
            var seen = new HashSet<int>();

            foreach (var raw in Process.GetProcesses())
            {
                try
                {
                    // Only user-visible apps — processes with a top-level window.
                    if (raw.MainWindowHandle == IntPtr.Zero) continue;
                    if (raw.Id == _ownPid) continue;

                    int pid = raw.Id;
                    seen.Add(pid);

                    if (!_byPid.TryGetValue(pid, out var info))
                    {
                        info = CreateInfo(raw);
                        _byPid[pid] = info;
                        _processes.Add(info);
                    }

                    UpdateSample(info, raw, now);
                }
                catch
                {
                    // Access denied / process exited mid-enumeration — skip.
                }
                finally
                {
                    raw.Dispose();
                }
            }

            // Prune PIDs that are gone.
            for (int i = _processes.Count - 1; i >= 0; i--)
            {
                var p = _processes[i];
                if (!seen.Contains(p.Pid))
                {
                    _byPid.Remove(p.Pid);
                    _processes.RemoveAt(i);
                }
            }
        }

        private ProcessInfo CreateInfo(Process raw)
        {
            string? exePath = null;
            try { exePath = raw.MainModule?.FileName; } catch { /* some processes deny module access */ }

            return new ProcessInfo
            {
                Pid = raw.Id,
                Name = GetFriendlyName(raw, exePath),
                ExePath = exePath,
                Icon = exePath != null ? GetCachedIcon(exePath) : null,
            };
        }

        private static string GetFriendlyName(Process raw, string? exePath)
        {
            // Prefer the window title-less process name; falls back to exe filename.
            string name = raw.ProcessName;
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    var ver = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(ver.FileDescription))
                        return ver.FileDescription;
                }
                catch { }
            }
            return name;
        }

        private BitmapSource? GetCachedIcon(string exePath)
        {
            if (_iconCache.TryGetValue(exePath, out var cached)) return cached;

            BitmapSource? source = null;
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                }
            }
            catch { /* some paths refuse icon extraction */ }

            _iconCache[exePath] = source;
            return source;
        }

        private static void UpdateSample(ProcessInfo info, Process raw, DateTime now)
        {
            info.WorkingSetBytes = raw.WorkingSet64;
            info.RamDisplay = FormatBytes(raw.WorkingSet64);

            TimeSpan cpuNow;
            try { cpuNow = raw.TotalProcessorTime; }
            catch { return; }  // access denied on some system-owned windows

            if (!info.HasBaseline)
            {
                info.LastCpuTime = cpuNow;
                info.LastSampleAt = now;
                info.HasBaseline = true;
                info.CpuDisplay = "—";  // first sample; need a second to compute delta
                return;
            }

            double elapsedCpuMs = (cpuNow - info.LastCpuTime).TotalMilliseconds;
            double elapsedWallMs = (now - info.LastSampleAt).TotalMilliseconds;
            if (elapsedWallMs <= 0) return;

            // % of total CPU capacity (matches Task Manager's "CPU" column).
            double pct = 100.0 * elapsedCpuMs / (elapsedWallMs * Environment.ProcessorCount);
            pct = Math.Clamp(pct, 0, 100);

            info.CpuPercent = pct;
            info.CpuDisplay = pct < 0.1 ? "0%" : $"{pct:F1}%";
            info.LastCpuTime = cpuNow;
            info.LastSampleAt = now;
        }

        private static string FormatBytes(long bytes)
        {
            double mb = bytes / 1024.0 / 1024.0;
            if (mb < 1024) return $"{mb:F0} MB";
            return $"{mb / 1024:F1} GB";
        }

        /// <summary>
        /// Kill a process and its children. Safe — swallows exceptions and
        /// returns false so the caller can surface a user-friendly error.
        /// </summary>
        public bool Kill(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Kill(entireProcessTree: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"ProcessMonitor.Kill({pid}) error: " + ex.Message);
                return false;
            }
        }
    }
}
