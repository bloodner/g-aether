using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public partial class AppProfilesViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<AppProfileRule> _rules = new();

        /// <summary>Populates the "add" row's process dropdown — running apps with a visible main window.</summary>
        [ObservableProperty]
        private List<string> _runningProcesses = new();

        /// <summary>Scene names available to bind to a rule (matches SceneService built-ins).</summary>
        public List<string> SceneNames { get; } =
            SceneService.BuiltInScenes.Select(s => s.Name).ToList();

        /// <summary>Currently-selected process name in the add-rule row.</summary>
        [ObservableProperty]
        private string? _newRuleProcess;

        /// <summary>Currently-selected scene name in the add-rule row.</summary>
        [ObservableProperty]
        private string? _newRuleScene;

        public AppProfilesViewModel()
        {
            foreach (var r in AppProfileService.Rules)
                Rules.Add(r);

            RefreshRunningProcesses();

            if (SceneNames.Count > 0) NewRuleScene = SceneNames[0];
        }

        [RelayCommand]
        private void AddRule()
        {
            if (string.IsNullOrWhiteSpace(NewRuleProcess) || string.IsNullOrWhiteSpace(NewRuleScene))
                return;

            string process = NewRuleProcess.Trim();
            // Tolerant: accept "blender" or "blender.exe" — canonicalize to the short form.
            if (process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                process = process.Substring(0, process.Length - 4);

            // Dedupe: if the user already has a rule for this process, replace it.
            var existing = Rules.FirstOrDefault(r =>
                r.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase));
            if (existing != null) Rules.Remove(existing);

            Rules.Add(new AppProfileRule { ProcessName = process, SceneName = NewRuleScene });
            PersistRules();

            NewRuleProcess = null;
            NewRuleScene = SceneNames.FirstOrDefault();
        }

        [RelayCommand]
        private void RemoveRule(AppProfileRule? rule)
        {
            if (rule == null) return;
            Rules.Remove(rule);
            PersistRules();
        }

        [RelayCommand]
        private void RefreshRunningProcesses()
        {
            try
            {
                var ownName = Process.GetCurrentProcess().ProcessName;
                var names = Process.GetProcesses()
                    .Where(p =>
                    {
                        try
                        {
                            // Only processes with a main window — excludes services, hidden helpers, etc.
                            return p.MainWindowHandle != IntPtr.Zero
                                && !string.IsNullOrEmpty(p.ProcessName)
                                && !p.ProcessName.Equals(ownName, StringComparison.OrdinalIgnoreCase);
                        }
                        catch { return false; }
                    })
                    .Select(p => p.ProcessName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                RunningProcesses = names;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AppProfilesViewModel.RefreshRunningProcesses: " + ex.Message);
            }
        }

        private void PersistRules()
        {
            AppProfileService.SetRules(Rules);
        }
    }
}
