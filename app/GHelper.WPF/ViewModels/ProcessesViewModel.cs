using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public enum ProcessSortKey { Cpu, Ram, Name }

    public partial class ProcessesViewModel : ObservableObject
    {
        private readonly ProcessMonitor _monitor = new();
        private readonly DispatcherTimer _timer;
        private readonly ICollectionView _view;

        public ICollectionView Processes => _view;

        [ObservableProperty]
        private int _processCount;

        [ObservableProperty]
        private ProcessSortKey _sortKey = ProcessSortKey.Cpu;

        [ObservableProperty]
        private bool _sortDescending = true;

        // ---- Tabs: 0 = Running, 1 = Startup, 2 = Scheduled, 3 = Services ----

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRunningTab))]
        [NotifyPropertyChangedFor(nameof(IsStartupTab))]
        [NotifyPropertyChangedFor(nameof(IsScheduledTab))]
        [NotifyPropertyChangedFor(nameof(IsServicesTab))]
        private int _selectedTabIndex;

        public bool IsRunningTab => SelectedTabIndex == 0;
        public bool IsStartupTab => SelectedTabIndex == 1;
        public bool IsScheduledTab => SelectedTabIndex == 2;
        public bool IsServicesTab => SelectedTabIndex == 3;

        public string[] TabLabels { get; } = ["Running", "Startup", "Scheduled", "Services"];

        [ObservableProperty]
        private ObservableCollection<StartupEntry> _startupEntries = new();

        [ObservableProperty]
        private int _startupEnabledCount;

        [ObservableProperty]
        private int _startupTotalCount;

        [ObservableProperty]
        private ObservableCollection<ScheduledTaskEntry> _scheduledTasks = new();

        [ObservableProperty]
        private int _scheduledEnabledCount;

        [ObservableProperty]
        private int _scheduledTotalCount;

        [ObservableProperty]
        private bool _isScanningScheduled;

        [ObservableProperty]
        private ObservableCollection<ServiceEntry> _services = new();

        [ObservableProperty]
        private int _servicesAutoCount;

        [ObservableProperty]
        private int _servicesTotalCount;

        [ObservableProperty]
        private bool _isScanningServices;

        partial void OnSelectedTabIndexChanged(int value)
        {
            // Lazy-load each non-Running tab the first time it's opened — no
            // point scanning the registry, Task Scheduler, or WMI if the user
            // never visits the tab.
            if (value == 1 && StartupEntries.Count == 0)
                RefreshStartup();
            if (value == 2 && ScheduledTasks.Count == 0 && !IsScanningScheduled)
                _ = RefreshScheduledAsync();
            if (value == 3 && Services.Count == 0 && !IsScanningServices)
                _ = RefreshServicesAsync();
        }

        public ProcessesViewModel()
        {
            _view = CollectionViewSource.GetDefaultView(_monitor.Processes);
            _monitor.Processes.CollectionChanged += (_, _) => ProcessCount = _monitor.Processes.Count;
            ApplySort();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2),
                IsEnabled = false,
            };
            _timer.Tick += (_, _) => DoRefresh();
        }

        /// <summary>
        /// Called when the panel becomes visible. Kicks off an immediate refresh
        /// so the first frame has data, then starts the 2s ticker.
        /// </summary>
        public void Start()
        {
            DoRefresh();
            _timer.Start();
        }

        /// <summary>
        /// Called when the panel hides. Stops the timer so we don't enumerate
        /// processes in the background on pages where the list isn't visible.
        /// </summary>
        public void Stop() => _timer.Stop();

        [RelayCommand]
        private void Refresh() => DoRefresh();

        [RelayCommand]
        private void Kill(ProcessInfo? info)
        {
            if (info == null) return;
            bool ok = _monitor.Kill(info.Pid);
            if (ok)
            {
                ToastService.ShowOsdOnly($"Killed: {info.Name}", "\uE894", ThemeService.ColorFansPower);
                DoRefresh();
            }
            else
            {
                ToastService.ShowOsdOnly($"Couldn't kill {info.Name}",
                    "\uE7BA", ThemeService.ColorFansPower);
            }
        }

        [RelayCommand]
        private void SortByCpu() => ToggleSort(ProcessSortKey.Cpu, defaultDescending: true);

        [RelayCommand]
        private void SortByRam() => ToggleSort(ProcessSortKey.Ram, defaultDescending: true);

        [RelayCommand]
        private void SortByName() => ToggleSort(ProcessSortKey.Name, defaultDescending: false);

        private void ToggleSort(ProcessSortKey key, bool defaultDescending)
        {
            if (SortKey == key) SortDescending = !SortDescending;
            else { SortKey = key; SortDescending = defaultDescending; }
            ApplySort();
        }

        private void ApplySort()
        {
            _view.SortDescriptions.Clear();
            string prop = SortKey switch
            {
                ProcessSortKey.Cpu => nameof(ProcessInfo.CpuPercent),
                ProcessSortKey.Ram => nameof(ProcessInfo.WorkingSetBytes),
                _ => nameof(ProcessInfo.Name),
            };
            _view.SortDescriptions.Add(new SortDescription(
                prop,
                SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending));
        }

        private void DoRefresh()
        {
            try { _monitor.Refresh(); _view.Refresh(); }
            catch (Exception ex) { Logger.WriteLine("ProcessesViewModel.DoRefresh: " + ex.Message); }
        }

        // ---- Startup tab commands ----------------------------------------------

        [RelayCommand]
        private void RefreshStartup()
        {
            try
            {
                StartupEntries.Clear();
                foreach (var entry in StartupTriageService.Scan())
                    StartupEntries.Add(entry);

                StartupTotalCount = StartupEntries.Count;
                StartupEnabledCount = StartupEntries.Count(e => e.IsEnabled);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ProcessesViewModel.RefreshStartup: " + ex.Message);
            }
        }

        // ---- Scheduled tasks tab commands --------------------------------------

        [RelayCommand]
        private async Task RefreshScheduledAsync()
        {
            if (IsScanningScheduled) return;
            IsScanningScheduled = true;
            try
            {
                // Task Scheduler enumeration can be slow on machines with many
                // folders (enterprise, heavily-bloated OEM installs) — run off
                // the UI thread so the button press doesn't look frozen.
                var results = await System.Threading.Tasks.Task.Run(ScheduledTaskTriageService.Scan);

                ScheduledTasks.Clear();
                foreach (var entry in results) ScheduledTasks.Add(entry);

                ScheduledTotalCount = ScheduledTasks.Count;
                ScheduledEnabledCount = ScheduledTasks.Count(e => e.IsEnabled);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ProcessesViewModel.RefreshScheduledAsync: " + ex.Message);
            }
            finally
            {
                IsScanningScheduled = false;
            }
        }

        [RelayCommand]
        private void ToggleScheduled(ScheduledTaskEntry? entry)
        {
            if (entry == null) return;

            bool desired = !entry.IsEnabled;
            bool ok = ScheduledTaskTriageService.SetEnabled(entry, desired);
            if (ok)
            {
                ScheduledEnabledCount = ScheduledTasks.Count(e => e.IsEnabled);
                ToastService.ShowOsdOnly(
                    $"{entry.Name}: {(desired ? "enabled" : "disabled")}",
                    desired ? "\uE73E" : "\uE711",
                    desired ? ThemeService.ColorEco : ThemeService.ColorFansPower);
            }
            else
            {
                ToastService.ShowOsdOnly(
                    $"Couldn't change {entry.Name}",
                    "\uE7BA", ThemeService.ColorFansPower);
            }
        }

        // ---- Services tab commands ----------------------------------------------

        [RelayCommand]
        private async Task RefreshServicesAsync()
        {
            if (IsScanningServices) return;
            IsScanningServices = true;
            try
            {
                // WMI's Win32_Service query can take 1-3s on busy systems — run
                // off the UI thread so the first tab-open doesn't stutter.
                var results = await System.Threading.Tasks.Task.Run(ServicesTriageService.Scan);

                Services.Clear();
                foreach (var entry in results) Services.Add(entry);

                ServicesTotalCount = Services.Count;
                ServicesAutoCount = Services.Count(s => s.IsAutoStart);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ProcessesViewModel.RefreshServicesAsync: " + ex.Message);
            }
            finally
            {
                IsScanningServices = false;
            }
        }

        [RelayCommand]
        private void ToggleServiceAutoStart(ServiceEntry? entry)
        {
            if (entry == null) return;

            var desired = entry.IsAutoStart ? ServiceStartMode.Manual : ServiceStartMode.Auto;
            bool ok = ServicesTriageService.SetStartMode(entry, desired);
            if (ok)
            {
                ServicesAutoCount = Services.Count(s => s.IsAutoStart);
                ToastService.ShowOsdOnly(
                    $"{entry.DisplayName}: {(desired == ServiceStartMode.Auto ? "auto-start" : "manual")}",
                    desired == ServiceStartMode.Auto ? "\uE73E" : "\uE711",
                    desired == ServiceStartMode.Auto ? ThemeService.ColorEco : ThemeService.ColorFansPower);
            }
            else
            {
                string reason = ServicesTriageService.LastError ?? "unknown error";
                ToastService.ShowOsdOnly(
                    $"{entry.DisplayName}: {reason}",
                    "\uE7BA", ThemeService.ColorFansPower);
            }
        }

        [RelayCommand]
        private void StopService(ServiceEntry? entry)
        {
            if (entry == null || !entry.IsRunning) return;

            bool ok = ServicesTriageService.StopService(entry);
            if (ok)
            {
                ToastService.ShowOsdOnly(
                    $"Stopped: {entry.DisplayName}", "\uE711", ThemeService.ColorFansPower);
            }
            else
            {
                string reason = ServicesTriageService.LastError ?? "unknown error";
                ToastService.ShowOsdOnly(
                    $"{entry.DisplayName}: {reason}",
                    "\uE7BA", ThemeService.ColorFansPower);
            }
        }

        [RelayCommand]
        private void StartService(ServiceEntry? entry)
        {
            if (entry == null || entry.IsRunning) return;

            bool ok = ServicesTriageService.StartService(entry);
            if (ok)
            {
                ToastService.ShowOsdOnly(
                    $"Started: {entry.DisplayName}", "\uE768", ThemeService.ColorEco);
            }
            else
            {
                string reason = ServicesTriageService.LastError ?? "unknown error";
                ToastService.ShowOsdOnly(
                    $"{entry.DisplayName}: {reason}",
                    "\uE7BA", ThemeService.ColorFansPower);
            }
        }

        [RelayCommand]
        private void ToggleStartup(StartupEntry? entry)
        {
            if (entry == null) return;

            bool desired = !entry.IsEnabled;
            bool ok = StartupTriageService.SetEnabled(entry, desired);
            if (ok)
            {
                StartupEnabledCount = StartupEntries.Count(e => e.IsEnabled);
                ToastService.ShowOsdOnly(
                    $"{entry.Name}: {(desired ? "enabled" : "disabled")}",
                    desired ? "\uE73E" : "\uE711",  // Checkmark / Cancel
                    desired ? ThemeService.ColorEco : ThemeService.ColorFansPower);
            }
            else
            {
                // SetEnabled only mutates entry.IsEnabled on success, so the VM
                // state is still in sync with reality — nothing to revert.
                ToastService.ShowOsdOnly(
                    $"Couldn't change {entry.Name} (admin required?)",
                    "\uE7BA", ThemeService.ColorFansPower);
            }
        }
    }
}
