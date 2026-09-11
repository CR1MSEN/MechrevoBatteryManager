using System;
using System.IO;
using System.Web.Script.Serialization;

namespace MechrevoBatteryManager
{
    public sealed class BatteryConfig
    {
        public bool Enabled { get; set; }
        public int UpperLimit { get; set; }
        public int LowerLimit { get; set; }
        public int StartupDelaySeconds { get; set; }
        public string OemDllPath { get; set; }
        public bool DriverSearchCompleted { get; set; }

        public BatteryConfig()
        {
            UpperLimit = 80;
            LowerLimit = 60;
            StartupDelaySeconds = 30;
            OemDllPath = @"C:\Program Files\OEM\机械革命电竞控制台\AiStoneService\MyControlCenter\ACPIDriverDll.dll";
        }

        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MechrevoBatteryManager"); } }
        public static string FilePath { get { return Path.Combine(DirectoryPath, "config.json"); } }
        private static string LegacyFilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MechrevoBatteryManager", "config.json"); } }
        public static BatteryConfig Load()
        {
            return LoadFromDirectory(DirectoryPath);
        }
        public static BatteryConfig LoadFromDirectory(string directory)
        {
            var path = Path.Combine(directory, "config.json");
            if (!File.Exists(path) && directory == DirectoryPath) path = LegacyFilePath;
            if (!File.Exists(path)) return new BatteryConfig();
            return new JavaScriptSerializer().Deserialize<BatteryConfig>(File.ReadAllText(path)) ?? new BatteryConfig();
        }
        public void Save()
        {
            Validate(); Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this));
        }
        public void Validate()
        {
            if (LowerLimit < 0 || UpperLimit > 100 || LowerLimit >= UpperLimit) throw new InvalidOperationException("Required: 0 <= lower < upper <= 100.");
            if (StartupDelaySeconds < 0 || StartupDelaySeconds > 300) throw new InvalidOperationException("Startup delay must be between 0 and 300 seconds.");
            if (string.IsNullOrWhiteSpace(OemDllPath)) throw new InvalidOperationException("OEM DLL path is required.");
        }
    }
}
