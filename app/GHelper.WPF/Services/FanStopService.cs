using System.Threading;
using GHelper.Mode;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Watchdog that drives the fans to a passive-cooling curve whenever the
    /// system is genuinely idle. Non-destructive: user-configured fan curves
    /// are never edited. When the system is hot, the watchdog restores the
    /// active performance mode's natural fan behavior.
    ///
    /// Safety design: the "idle" curve we apply is not 0% at every point. It's
    /// 0% up to 60°C then ramps to 100% by 95°C. If this service ever hangs or
    /// crashes while the idle curve is active, the hardware fans will still
    /// spin up automatically once temperatures rise — the laptop can never end
    /// up with fans pinned off under load.
    ///
    /// Hysteresis: fans stop when CPU AND GPU are both below 62°C; they resume
    /// when either crosses 72°C. The 10°C gap prevents the fans from flapping
    /// on/off near the threshold. Thresholds are tuned for realistic ROG idle
    /// floors — a ROG Silent-mode idle commonly sits in the high 50s, so a
    /// lower entry point would effectively never fire.
    /// </summary>
    public static class FanStopService
    {
        private const string EnabledKey = "fan_stop_on_idle";

        private const double IdleOnTempC = 62.0;
        private const double IdleOffTempC = 72.0;
        private const int CheckIntervalMs = 2500;

        private static System.Threading.Timer? _timer;
        private static bool _isIdleActive;
        private static readonly object _lock = new();

        /// <summary>Safety curve applied while in idle mode. See class summary for rationale.</summary>
        private static readonly byte[] IdleCurve =
        {
            // 8 temperature points (°C) — holds 0% through the exit threshold,
            // then ramps hard so the hardware protects itself if we hang.
            40, 50, 60, 72, 78, 84, 90, 95,
            // 8 PWM speed points (%)
            0,  0,  0,  0,  30, 60, 80, 100,
        };

        public static bool IsEnabled
        {
            get => AppConfig.Is(EnabledKey);
            set
            {
                AppConfig.Set(EnabledKey, value ? 1 : 0);
                if (value) Start();
                else Stop();
            }
        }

        /// <summary>Called once at app startup from AppHost.Initialize.</summary>
        public static void Initialize()
        {
            if (IsEnabled) Start();
        }

        public static void Shutdown()
        {
            Stop();
        }

        private static void Start()
        {
            lock (_lock)
            {
                if (_timer != null) return;
                _timer = new System.Threading.Timer(OnTick, null, CheckIntervalMs, CheckIntervalMs);
                Logger.WriteLine("FanStopService: started");
            }
        }

        private static void Stop()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }

            // If we're currently holding the idle curve, restore the user's
            // settings so disabling the feature doesn't leave them stuck.
            if (_isIdleActive)
            {
                RestoreCurve();
                _isIdleActive = false;
                Logger.WriteLine("FanStopService: stopped (restored natural curve on the way out)");
            }
            else
            {
                Logger.WriteLine("FanStopService: stopped");
            }
        }

        private static void OnTick(object? state)
        {
            try { Check(); }
            catch (Exception ex) { Logger.WriteLine("FanStopService tick error: " + ex.Message); }
        }

        private static void Check()
        {
            // HardwareControl's readings are the same ones the Monitor page uses.
            // They're only populated after the first sensor tick post-launch, so
            // we bail gracefully if they're still zero.
            float cpu = HardwareControl.cpuTemp ?? -1;
            float gpu = HardwareControl.gpuTemp ?? -1;

            if (cpu <= 0) return;  // no CPU reading yet — wait until next tick

            // In Eco / dGPU-off scenarios, gpuTemp is 0 or missing. Treat that as
            // "cold" for the purpose of the idle check — the GPU literally isn't
            // running, so it can't be a reason to keep fans on.
            bool gpuCool = gpu <= 0 || gpu < IdleOnTempC;
            bool gpuHot  = gpu > 0 && gpu >= IdleOffTempC;

            if (_isIdleActive)
            {
                bool cpuHot = cpu >= IdleOffTempC;
                if (cpuHot || gpuHot)
                {
                    RestoreCurve();
                    _isIdleActive = false;
                    Logger.WriteLine($"FanStopService: exit idle (CPU={cpu:F0}°C GPU={gpu:F0}°C)");
                }
            }
            else
            {
                if (cpu < IdleOnTempC && gpuCool)
                {
                    ApplyIdleCurve();
                    _isIdleActive = true;
                    Logger.WriteLine($"FanStopService: enter idle (CPU={cpu:F0}°C GPU={gpu:F0}°C)");
                }
            }
        }

        private static void ApplyIdleCurve()
        {
            try
            {
                Program.acpi?.SetFanCurve(AsusFan.CPU, IdleCurve);
                Program.acpi?.SetFanCurve(AsusFan.GPU, IdleCurve);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("FanStopService.ApplyIdleCurve error: " + ex.Message);
            }
        }

        private static void RestoreCurve()
        {
            try
            {
                // Full mode re-commit: includes the G14-2024 reset workaround that
                // reliably clears any SetFanCurve override we installed, then AutoFans
                // reapplies user-saved curves if auto_apply is on. A plain
                // DeviceSet(PerformanceMode, mode) is not enough on some laptops —
                // the custom curve persists through a same-mode rewrite.
                Program.modeControl?.SetPerformanceMode();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("FanStopService.RestoreCurve error: " + ex.Message);
            }
        }
    }
}
