using System.IO;
using System.Text.RegularExpressions;

namespace GHelper.WPF.Services
{
    public enum UsageProfile
    {
        LightMobile,
        HeavyMobile,
        DesktopCasual,
        DesktopGaming,
        Mixed,
    }

    public class UsageStats
    {
        public int TotalLogLines { get; set; }
        public int GpuReadings { get; set; }
        public double AvgGpuUsage { get; set; }
        public double MaxGpuUsage { get; set; }
        public int HighGpuReadings { get; set; }
        public double PercentOnBattery { get; set; }
        public UsageProfile Profile { get; set; }
        public string ProfileReason { get; set; } = "";
    }

    public class Recommendation
    {
        public required string SettingName { get; init; }
        public required string CurrentValue { get; init; }
        public required string RecommendedValue { get; init; }
        public required string Reason { get; init; }
        public required Action Apply { get; init; }
        public bool IsChange => CurrentValue != RecommendedValue;
    }

    public static class UsageAnalyzer
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GHelper", "log.txt");

        private static readonly Regex GpuUsageRegex = new(@"GPU usage:.*?(\d+)%", RegexOptions.Compiled);

        public static UsageStats Analyze()
        {
            var stats = new UsageStats();

            if (File.Exists(LogPath))
            {
                try
                {
                    var lines = File.ReadAllLines(LogPath);
                    stats.TotalLogLines = lines.Length;

                    double gpuTotal = 0;
                    foreach (var line in lines)
                    {
                        var match = GpuUsageRegex.Match(line);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int usage))
                        {
                            stats.GpuReadings++;
                            gpuTotal += usage;
                            if (usage > stats.MaxGpuUsage) stats.MaxGpuUsage = usage;
                            if (usage > 50) stats.HighGpuReadings++;
                        }
                    }

                    if (stats.GpuReadings > 0)
                        stats.AvgGpuUsage = gpuTotal / stats.GpuReadings;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("UsageAnalyzer log parse error: " + ex.Message);
                }
            }

            bool onBattery = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus
                != System.Windows.Forms.PowerLineStatus.Online;

            stats.PercentOnBattery = onBattery ? 70 : 20;
            stats.Profile = ClassifyProfile(stats, onBattery);
            stats.ProfileReason = ExplainProfile(stats, onBattery);

            return stats;
        }

        private static UsageProfile ClassifyProfile(UsageStats stats, bool onBattery)
        {
            double highGpuPercent = stats.GpuReadings > 0
                ? (double)stats.HighGpuReadings / stats.GpuReadings * 100
                : 0;

            if (onBattery && stats.AvgGpuUsage < 15)
                return UsageProfile.LightMobile;
            if (onBattery && stats.AvgGpuUsage >= 15)
                return UsageProfile.HeavyMobile;
            if (!onBattery && highGpuPercent > 30)
                return UsageProfile.DesktopGaming;
            if (!onBattery && stats.AvgGpuUsage < 20)
                return UsageProfile.DesktopCasual;

            return UsageProfile.Mixed;
        }

        private static string ExplainProfile(UsageStats stats, bool onBattery)
        {
            string power = onBattery ? "on battery" : "plugged in";
            string gpu = stats.GpuReadings > 0
                ? $"avg GPU usage {stats.AvgGpuUsage:F0}%"
                : "no GPU data yet";

            return stats.Profile switch
            {
                UsageProfile.LightMobile => $"You're {power} with {gpu}. Optimizing for battery life and quiet operation.",
                UsageProfile.HeavyMobile => $"You're {power} with {gpu}. Balancing performance with battery life.",
                UsageProfile.DesktopCasual => $"You're {power} with {gpu}. Keeping things quiet with good responsiveness.",
                UsageProfile.DesktopGaming => $"You're {power} with {gpu}. Maximizing performance for demanding workloads.",
                UsageProfile.Mixed => $"You're {power} with {gpu}. Applying balanced defaults.",
                _ => $"You're {power}. Applying balanced defaults."
            };
        }
    }
}
