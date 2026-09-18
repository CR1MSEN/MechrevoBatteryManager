using System;
using System.IO;
using System.Web.Script.Serialization;

namespace MechrevoBatteryManager
{
    public sealed class EcDefaults
    {
        public int? UpperRaw { get; set; }
        public int? LowerRaw { get; set; }
        public string CapturedAtUtc { get; set; }
        public static string FilePath { get { return Path.Combine(BatteryConfig.DirectoryPath, "Default.json"); } }
        public static EcDefaults Load(string path)
        {
            var value = new JavaScriptSerializer().Deserialize<EcDefaults>(File.ReadAllText(path));
            if (value == null || !value.UpperRaw.HasValue || !value.LowerRaw.HasValue ||
                value.UpperRaw < 0 || value.UpperRaw > 255 || value.LowerRaw < 0 || value.LowerRaw > 255)
                throw new InvalidDataException("Default.json is invalid; EC restore was cancelled.");
            return value;
        }
    }
    public sealed class ThresholdResult
    {
        public byte BeforeUpper, BeforeLower, AfterUpper, AfterLower;
        public byte ChargeLimitGate;
        public bool Changed;
    }

    public static class ThresholdManager
    {
        public const uint UpperAddress = 0x07B9;
        public const uint LowerAddress = 0x07D0;
        private static byte EffectiveUpper(byte raw) { return (byte)(raw & 0x7F); }

        public static void EnsureDefaults(string dllPath, string path = null)
        {
            path = path ?? EcDefaults.FilePath;
            if (File.Exists(path)) { EcDefaults.Load(path); return; }
            EcDefaults snapshot;
            using (var ec = new EcClient(dllPath))
                snapshot = new EcDefaults { UpperRaw = ec.Read(UpperAddress), LowerRaw = ec.Read(LowerAddress), CapturedAtUtc = DateTime.UtcNow.ToString("o") };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Publish only a complete snapshot, and never replace an existing backup.
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(snapshot));
                try { File.Move(temp, path); }
                catch (IOException) { if (!File.Exists(path)) throw; EcDefaults.Load(path); }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static ThresholdResult Read(string dllPath)
        {
            using (var ec = new EcClient(dllPath))
            {
                return new ThresholdResult { BeforeUpper = EffectiveUpper(ec.Read(UpperAddress)), BeforeLower = ec.Read(LowerAddress), ChargeLimitGate = ec.Read(0x0742) };
            }
        }

        public static ThresholdResult Apply(BatteryConfig config, bool skipLower = false, string defaultsPath = null)
        {
            config.Validate();
            EnsureDefaults(config.OemDllPath, defaultsPath);
            skipLower = skipLower || (config.OemDllPath != null && config.OemDllPath.IndexOf("机械革命控制中心", StringComparison.OrdinalIgnoreCase) >= 0);
            using (var ec = new EcClient(config.OemDllPath))
            {
                var result = new ThresholdResult { BeforeUpper = EffectiveUpper(ec.Read(UpperAddress)), BeforeLower = ec.Read(LowerAddress) };
                if (config.UpperLimit == 0 && config.LowerLimit == 0) return result;
                if (result.BeforeUpper != config.UpperLimit)
                {
                    ec.Write(UpperAddress, (byte)config.UpperLimit);
                    if (EffectiveUpper(ec.Read(UpperAddress)) != config.UpperLimit) throw new InvalidOperationException("Upper threshold write verification failed.");
                    result.Changed = true;
                }
                try
                {
                    // A configured lower limit of 0 means "do not write" only when EC is already 0.
                    // If EC contains a previous non-zero limit, write 0 to clear it.
                    if (!skipLower && result.BeforeLower != 0 && result.BeforeLower != config.LowerLimit)
                    {
                        ec.Write(LowerAddress, (byte)config.LowerLimit);
                        if (ec.Read(LowerAddress) != config.LowerLimit) throw new InvalidOperationException("Lower threshold write verification failed.");
                        result.Changed = true;
                    }
                }
                catch
                {
                    if (result.BeforeUpper <= 100) ec.Write(UpperAddress, result.BeforeUpper);
                    throw;
                }
                result.AfterUpper = EffectiveUpper(ec.Read(UpperAddress));
                result.AfterLower = ec.Read(LowerAddress);
                return result;
            }
        }

        public static ThresholdResult Reset(string dllPath, bool skipLower = false)
        {
            var defaults = EcDefaults.Load(EcDefaults.FilePath);
            skipLower = skipLower || dllPath.IndexOf("机械革命控制中心", StringComparison.OrdinalIgnoreCase) >= 0;
            using (var ec = new EcClient(dllPath))
            {
                var result = new ThresholdResult { BeforeUpper = EffectiveUpper(ec.Read(UpperAddress)), BeforeLower = ec.Read(LowerAddress) };
                ec.Write(UpperAddress, (byte)defaults.UpperRaw.Value);
                if (EffectiveUpper(ec.Read(UpperAddress)) != EffectiveUpper((byte)defaults.UpperRaw.Value)) throw new InvalidOperationException("Upper threshold restore verification failed.");
                if (!skipLower)
                {
                    ec.Write(LowerAddress, (byte)defaults.LowerRaw.Value);
                    if (ec.Read(LowerAddress) != defaults.LowerRaw.Value) throw new InvalidOperationException("Lower threshold restore verification failed.");
                }
                result.AfterUpper = EffectiveUpper(ec.Read(UpperAddress)); result.AfterLower = ec.Read(LowerAddress);
                result.Changed = result.BeforeUpper != result.AfterUpper || result.BeforeLower != result.AfterLower;
                return result;
            }
        }
    }
}
