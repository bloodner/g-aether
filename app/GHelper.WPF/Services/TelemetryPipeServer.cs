using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Windows.Forms;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Streams live telemetry as newline-delimited JSON over a named pipe.
    /// The Game Bar widget (and any other local consumer) connects as a client
    /// and receives one JSON line per sensor tick.
    ///
    /// Pipe: \\.\pipe\g-aether-telemetry
    /// ACL grants "ALL APPLICATION PACKAGES" read access so sandboxed
    /// AppContainer clients (the Game Bar widget) can connect.
    ///
    /// Hooked into MainViewModel's sensor tick via PushSnapshot(), so no
    /// duplicate hardware reads — we publish whatever ReadSensors() just wrote.
    /// </summary>
    public static class TelemetryPipeServer
    {
        public const string PipeName = "g-aether-telemetry";
        public const int SchemaVersion = 1;

        // Each connected client registers a handler on this event; PushSnapshot
        // invokes all handlers with the serialized JSON bytes.
        private static event Action<byte[]>? OnSnapshot;

        private static CancellationTokenSource? _cts;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        };

        public static void Initialize()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            Logger.WriteLine($"TelemetryPipeServer listening on \\\\.\\pipe\\{PipeName}");
        }

        public static void Shutdown()
        {
            _cts?.Cancel();
            _cts = null;
        }

        /// <summary>
        /// Publishes the current HardwareControl snapshot to every connected client.
        /// Safe to call on a background thread; no-op if nobody is connected.
        /// </summary>
        public static void PushSnapshot()
        {
            if (OnSnapshot == null) return;  // skip JSON work when nobody's listening

            try
            {
                byte[] payload = BuildSnapshotJson();
                OnSnapshot?.Invoke(payload);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("TelemetryPipeServer.PushSnapshot error: " + ex.Message);
            }
        }

        // ---- accept loop ----------------------------------------------------

        private static async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = CreateServerPipe();
                    await server.WaitForConnectionAsync(ct);

                    // Hand the connection to a worker and immediately loop to
                    // accept the next one. Multiple clients are supported.
                    NamedPipeServerStream handed = server;
                    server = null;  // ownership transferred
                    _ = Task.Run(() => HandleClientAsync(handed, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("TelemetryPipeServer accept error: " + ex.Message);
                    server?.Dispose();
                    await Task.Delay(1000, ct).ContinueWith(_ => { });
                }
            }
        }

        private static async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
        {
            var signal = new SemaphoreSlim(0);
            byte[]? pending = null;
            object gate = new();

            Action<byte[]> handler = bytes =>
            {
                lock (gate) { pending = bytes; }  // latest-value-wins; old frames get dropped
                try { signal.Release(); } catch { }
            };

            OnSnapshot += handler;

            try
            {
                // Fire an immediate snapshot so a fresh client doesn't have to
                // wait up to 2s for the first sensor tick.
                try { await server.WriteAsync(BuildSnapshotJson(), ct); await server.FlushAsync(ct); }
                catch { }

                while (server.IsConnected && !ct.IsCancellationRequested)
                {
                    await signal.WaitAsync(ct);

                    byte[]? toSend;
                    lock (gate) { toSend = pending; pending = null; }
                    if (toSend == null) continue;

                    try
                    {
                        await server.WriteAsync(toSend, ct);
                        await server.FlushAsync(ct);
                    }
                    catch (IOException) { break; }  // client disconnected
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.WriteLine("TelemetryPipeServer client error: " + ex.Message);
            }
            finally
            {
                OnSnapshot -= handler;
                server.Dispose();
                signal.Dispose();
            }
        }

        // ---- pipe creation with ACL --------------------------------------------

        private static NamedPipeServerStream CreateServerPipe()
        {
            var security = new PipeSecurity();

            // Current user: full control.
            var me = WindowsIdentity.GetCurrent().User!;
            security.AddAccessRule(new PipeAccessRule(
                me, PipeAccessRights.FullControl, AccessControlType.Allow));

            // ALL APPLICATION PACKAGES (S-1-15-2-1) — lets sandboxed / AppContainer
            // clients connect. Read-only access: data + sync handle (needed for
            // client-side waits). WriteAttributes isn't valid on pipes and trips an
            // ACL "access denied" at create time.
            var appPkgs = new SecurityIdentifier("S-1-15-2-1");
            security.AddAccessRule(new PipeAccessRule(
                appPkgs,
                PipeAccessRights.ReadData | PipeAccessRights.Synchronize,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.Out,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                inBufferSize: 0,
                outBufferSize: 65536,
                pipeSecurity: security);
        }

        // ---- JSON payload -------------------------------------------------------

        private static byte[] BuildSnapshotJson()
        {
            var ps = SystemInformation.PowerStatus;
            int? batteryPct = ps.BatteryLifePercent == 255 ? null : (int)(ps.BatteryLifePercent * 100);

            var snapshot = new TelemetrySnapshot(
                schema_version: SchemaVersion,
                timestamp: DateTimeOffset.UtcNow.ToString("o"),
                cpu_temp_c: NullIfNegative(HardwareControl.cpuTemp),
                dgpu_temp_c: NullIfNegative(HardwareControl.gpuTemp),
                dgpu_usage_pct: HardwareControl.gpuUse is null or < 0 ? null : HardwareControl.gpuUse,
                dgpu_power_w: HardwareControl.gpuPower is null or < 0 ? null : HardwareControl.gpuPower,
                cpu_fan_rpm: ParseFanRpm(HardwareControl.cpuFan),
                gpu_fan_rpm: ParseFanRpm(HardwareControl.gpuFan),
                battery_pct: batteryPct,
                battery_rate_w: HardwareControl.batteryRate);

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOpts);
            byte[] lineTerminated = new byte[json.Length + 1];
            json.AsSpan().CopyTo(lineTerminated);
            lineTerminated[^1] = (byte)'\n';
            return lineTerminated;
        }

        private static float? NullIfNegative(float? v) => v is null or < 0 ? null : v;

        private static int? ParseFanRpm(string? fanStr)
        {
            if (string.IsNullOrEmpty(fanStr)) return null;
            var digits = new string(fanStr.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int val) ? val : null;
        }

        // Record (not anonymous object) so field order + naming is explicit
        // and fully under our control as the schema evolves.
        private record TelemetrySnapshot(
            int schema_version,
            string timestamp,
            float? cpu_temp_c,
            float? dgpu_temp_c,
            int? dgpu_usage_pct,
            int? dgpu_power_w,
            int? cpu_fan_rpm,
            int? gpu_fan_rpm,
            int? battery_pct,
            decimal? battery_rate_w);
    }
}
