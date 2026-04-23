using System.Runtime.InteropServices;

namespace GHelper.Gpu.NVidia;

/// <summary>
/// Minimal NVML (NVIDIA Management Library) PInvoke surface.
/// nvml.dll ships with every NVIDIA driver and lives on the system search path,
/// so no bundled binary is needed. We only touch init, device-by-index, and
/// power-usage; shutdown is implicit at process exit.
/// </summary>
internal static class NvmlInterop
{
    public const int NVML_SUCCESS = 0;

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    public static extern int Init();

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    public static extern int DeviceGetHandleByIndex(uint index, out nint device);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
    public static extern int DeviceGetPowerUsage(nint device, out uint powerMilliwatts);
}
