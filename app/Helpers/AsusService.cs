using System.Diagnostics;

namespace GHelper.Helpers
{
    public class ServiceInfo
    {
        public string ProcessName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";
        public bool IsRunning { get; set; }
        public bool IsArmoryCrate { get; init; }
    }

    public static class AsusService
    {

        static List<string> services = new() {
                "ArmouryCrateControlInterface",
                "ArmouryCrateProArtService",
                "AsHidService",
                "ASUSOptimization",
                "AsusAppService",
                "ASUSLinkNear",
                "ASUSLinkRemote",
                "ASUSSoftwareManager",
                "ASUSLiveUpdateAgent",
                "ASUSSwitch",
                "ASUSSystemAnalysis",
                "ASUSSystemDiagnosis",
                "AsusCertService"
        };

        //"AsusPTPService",

        static List<string> processesAC = new() {
                "ArmouryCrateSE.Service",
                "ArmouryCrate.Service",
                "LightingService",
        };

        static List<string> servicesAC = new() {
                "ArmouryCrateSEService",
                "ArmouryCrateService",
                "LightingService",
        };

        public static bool IsAsusOptimizationRunning()
        {
            return Process.GetProcessesByName("AsusOptimization").Length > 0;
        }

        public static bool IsArmouryRunning()
        {
            var acService = Process.GetProcessesByName("ArmouryCrate.Service").Length > 0;
            var lightingService = Process.GetProcessesByName("LightingService").Length > 0;   
            Logger.WriteLine($"AC Service: {acService}, Lighting Service: {lightingService}");
            return acService || lightingService;
        }

        public static void RunArmouryUninstaller()
        {
            Process.Start(new ProcessStartInfo("https://dlcdnets.asus.com/pub/ASUS/mb/14Utilities/Armoury_Crate_Uninstall_Tool.zip") { UseShellExecute = true });
        }

        public static bool IsOSDRunning()
        {
            return Process.GetProcessesByName("AsusOSD").Length > 0;
        }


        static List<ServiceInfo> serviceDefinitions = new()
        {
            new() { ProcessName = "ArmouryCrateControlInterface", DisplayName = "Armoury Crate Control", Description = "Hardware control interface" },
            new() { ProcessName = "ArmouryCrateProArtService", DisplayName = "ProArt Service", Description = "ProArt display profiles" },
            new() { ProcessName = "AsHidService", DisplayName = "HID Service", Description = "Keyboard & input handling" },
            new() { ProcessName = "ASUSOptimization", DisplayName = "ASUS Optimization", Description = "System optimization agent" },
            new() { ProcessName = "AsusAppService", DisplayName = "App Service", Description = "ASUS application manager" },
            new() { ProcessName = "ASUSLinkNear", DisplayName = "Link Near", Description = "Nearby device sharing" },
            new() { ProcessName = "ASUSLinkRemote", DisplayName = "Link Remote", Description = "Remote device control" },
            new() { ProcessName = "ASUSSoftwareManager", DisplayName = "Software Manager", Description = "Driver & software updates" },
            new() { ProcessName = "ASUSLiveUpdateAgent", DisplayName = "Live Update", Description = "Background update agent" },
            new() { ProcessName = "ASUSSwitch", DisplayName = "ASUS Switch", Description = "Multi-device switching" },
            new() { ProcessName = "ASUSSystemAnalysis", DisplayName = "System Analysis", Description = "Performance diagnostics" },
            new() { ProcessName = "ASUSSystemDiagnosis", DisplayName = "System Diagnosis", Description = "Hardware diagnostics" },
            new() { ProcessName = "AsusCertService", DisplayName = "Certificate Service", Description = "ASUS certificate manager" },
        };

        static List<ServiceInfo> acServiceDefinitions = new()
        {
            new() { ProcessName = "ArmouryCrateSE.Service", DisplayName = "Armoury Crate SE", Description = "Armoury Crate special edition", IsArmoryCrate = true },
            new() { ProcessName = "ArmouryCrate.Service", DisplayName = "Armoury Crate", Description = "Main Armoury Crate service", IsArmoryCrate = true },
            new() { ProcessName = "LightingService", DisplayName = "Lighting Service", Description = "Aura RGB lighting control", IsArmoryCrate = true },
        };

        public static List<ServiceInfo> GetServiceDetails()
        {
            var results = new List<ServiceInfo>();
            foreach (var def in serviceDefinitions)
            {
                results.Add(new ServiceInfo
                {
                    ProcessName = def.ProcessName,
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    IsRunning = Process.GetProcessesByName(def.ProcessName).Length > 0,
                    IsArmoryCrate = false,
                });
            }
            if (AppConfig.IsStopAC())
            {
                foreach (var def in acServiceDefinitions)
                {
                    results.Add(new ServiceInfo
                    {
                        ProcessName = def.ProcessName,
                        DisplayName = def.DisplayName,
                        Description = def.Description,
                        IsRunning = Process.GetProcessesByName(def.ProcessName).Length > 0,
                        IsArmoryCrate = true,
                    });
                }
            }
            return results;
        }

        public static int GetRunningCount()
        {
            int count = 0;
            foreach (string service in services)
            {
                if (Process.GetProcessesByName(service).Count() > 0) count++;
            }

            if (AppConfig.IsStopAC())
                foreach (string service in processesAC)
                {
                    if (Process.GetProcessesByName(service).Count() > 0)
                    {
                        count++;
                        Logger.WriteLine(service);
                    }
                }

            return count;
        }


        public static void StopAsusServices()
        {
            foreach (string service in services)
            {
                ProcessHelper.StopDisableService(service);
            }

            if (AppConfig.IsStopAC())
            {
                foreach (string service in servicesAC)
                {
                    ProcessHelper.StopDisableService(service, "Manual");
                }
                Thread.Sleep(1000);
            }

        }

        public static void StartAsusServices()
        {
            foreach (string service in services)
            {
                ProcessHelper.StartEnableService(service);
            }

            if (AppConfig.IsStopAC())
            {
                foreach (string service in servicesAC)
                {
                    ProcessHelper.StartEnableService(service);
                }
                Thread.Sleep(1000);
            }

        }

    }

}