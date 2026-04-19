using System.Globalization;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Process-wide notifier for background update discovery. Fires
    /// <see cref="UpdateDiscovered"/> exactly once per app launch when an update is
    /// available and not skipped. <see cref="Latest"/> holds the last-known result
    /// so late subscribers (e.g., settings panel opened after launch) can still see it.
    /// </summary>
    public static class UpdateNotifier
    {
        private const string LastCheckKey = "update_last_check_iso";
        private const string SkipVersionKey = "update_skip_version";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

        public static event Action<UpdateCheckResult>? UpdateDiscovered;
        public static UpdateCheckResult? Latest { get; private set; }

        public static void MarkChecked() =>
            AppConfig.Set(LastCheckKey, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        public static bool IsDue()
        {
            string? lastIso = AppConfig.GetString(LastCheckKey);
            if (string.IsNullOrEmpty(lastIso)) return true;
            if (!DateTime.TryParse(lastIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last))
                return true;
            return DateTime.UtcNow - last >= CheckInterval;
        }

        public static string? SkippedVersion => AppConfig.GetString(SkipVersionKey);

        public static void SetSkippedVersion(string? version)
        {
            if (string.IsNullOrEmpty(version)) AppConfig.Remove(SkipVersionKey);
            else AppConfig.Set(SkipVersionKey, version);
        }

        /// <summary>
        /// Publishes a discovered update to subscribers. Swallows callback exceptions so
        /// one flaky listener doesn't break the others.
        /// </summary>
        internal static void Publish(UpdateCheckResult result)
        {
            Latest = result;
            var handlers = UpdateDiscovered;
            if (handlers == null) return;
            foreach (Action<UpdateCheckResult> h in handlers.GetInvocationList())
            {
                try { h(result); }
                catch (Exception ex) { Logger.WriteLine("UpdateNotifier callback error: " + ex.Message); }
            }
        }
    }

    public static class UpdateBackgroundCheck
    {
        /// <summary>
        /// Runs a check in the background if the 24h interval has elapsed and the user
        /// hasn't opted out. Never throws — failures are logged and ignored.
        /// </summary>
        public static async Task RunAsync()
        {
            try
            {
                if (!UpdateNotifier.IsDue()) return;

                var result = await UpdateService.CheckAsync();
                UpdateNotifier.MarkChecked();

                if (result.Status != UpdateStatus.UpdateAvailable) return;
                if (string.Equals(result.LatestVersion, UpdateNotifier.SkippedVersion, StringComparison.Ordinal))
                    return;

                UpdateNotifier.Publish(result);
                Logger.WriteLine($"Background update check: {result.LatestVersion} available");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Background update check error: " + ex.Message);
            }
        }
    }
}
