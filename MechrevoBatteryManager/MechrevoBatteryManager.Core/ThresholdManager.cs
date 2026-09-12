using System;

namespace MechrevoBatteryManager
{
    public sealed class ThresholdResult
    {
        public byte BeforeUpper, BeforeLower, AfterUpper, AfterLower;
        public byte ChargeLimitGate;
        public byte State07C3, State0770, StoredLimit;
        public bool LimitEnabled;
        public bool Changed;
    }

    public static class ThresholdManager
    {
        public const uint UpperAddress = 0x07B9;
        public const uint LowerAddress = 0x07D0;

        public static ThresholdResult Read(string dllPath)
        {
            using (var ec = new EcClient(dllPath))
            {
                var c3 = ec.Read(0x07C3); var w70 = ec.Read(0x0770); var stored = (byte)(ec.Read(0x087F) & 0x7F);
                var stateEnabled = c3 == 4 || c3 == 5 || w70 == 4 || w70 == 5;
                return new ThresholdResult { BeforeUpper = ec.Read(UpperAddress), BeforeLower = ec.Read(LowerAddress), ChargeLimitGate = ec.Read(0x0742), State07C3 = c3, State0770 = w70, StoredLimit = stored, LimitEnabled = stateEnabled || (stored >= 1 && stored <= 100) };
            }
        }

        public static ThresholdResult Apply(BatteryConfig config)
        {
            config.Validate();
            using (var ec = new EcClient(config.OemDllPath))
            {
                var result = new ThresholdResult { BeforeUpper = ec.Read(UpperAddress), BeforeLower = ec.Read(LowerAddress) };
                if (result.BeforeUpper == 0 && result.BeforeLower == 0) return result;
                if (config.UpperLimit == 0 && config.LowerLimit == 0) return result;
                if (result.BeforeUpper != config.UpperLimit)
                {
                    ec.Write(UpperAddress, (byte)config.UpperLimit);
                    if (ec.Read(UpperAddress) != config.UpperLimit) throw new InvalidOperationException("Upper threshold write verification failed.");
                    result.Changed = true;
                }
                try
                {
                    if (result.BeforeLower != config.LowerLimit)
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
                result.AfterUpper = ec.Read(UpperAddress);
                result.AfterLower = ec.Read(LowerAddress);
                return result;
            }
        }
    }
}
