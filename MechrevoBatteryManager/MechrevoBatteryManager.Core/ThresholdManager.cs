using System;

namespace MechrevoBatteryManager
{
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

        public static ThresholdResult Read(string dllPath)
        {
            using (var ec = new EcClient(dllPath))
            {
                return new ThresholdResult { BeforeUpper = EffectiveUpper(ec.Read(UpperAddress)), BeforeLower = ec.Read(LowerAddress), ChargeLimitGate = ec.Read(0x0742) };
            }
        }

        public static ThresholdResult Apply(BatteryConfig config, bool skipLower = false)
        {
            config.Validate();
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
            using (var ec = new EcClient(dllPath))
            {
                var result = new ThresholdResult { BeforeUpper = EffectiveUpper(ec.Read(UpperAddress)), BeforeLower = ec.Read(LowerAddress) };
                ec.Write(UpperAddress, 0);
                if (EffectiveUpper(ec.Read(UpperAddress)) != 0) throw new InvalidOperationException("Upper threshold reset verification failed.");
                if (!skipLower)
                {
                    ec.Write(LowerAddress, 0);
                    if (ec.Read(LowerAddress) != 0) throw new InvalidOperationException("Lower threshold reset verification failed.");
                }
                result.AfterUpper = 0; result.AfterLower = ec.Read(LowerAddress); result.Changed = result.BeforeUpper != 0 || (!skipLower && result.BeforeLower != 0);
                return result;
            }
        }
    }
}
