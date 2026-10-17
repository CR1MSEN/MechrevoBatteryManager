using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace MechrevoBatteryManager
{
    public sealed class EcDefaults
    {
        public int? UpperRaw { get; set; }
        public int? LowerRaw { get; set; }
        public int? ProfileRaw { get; set; }
        public string CapturedAtUtc { get; set; }
        public static string FilePath { get { return Path.Combine(BatteryConfig.DirectoryPath, "Default.json"); } }
        public static EcDefaults Load(string path)
        {
            var value = new JavaScriptSerializer().Deserialize<EcDefaults>(File.ReadAllText(path));
            if (value == null || !ValidByte(value.UpperRaw) || !ValidByte(value.LowerRaw) ||
                (value.ProfileRaw.HasValue && !ValidByte(value.ProfileRaw)))
                throw new InvalidDataException("Default.json is invalid; EC restore was cancelled.");
            // v1.5 backups stay untouched. The new profile snapshot is saved separately.
            if (!value.ProfileRaw.HasValue && File.Exists(path + ".profile.json"))
            {
                var profile = new JavaScriptSerializer().Deserialize<EcDefaults>(File.ReadAllText(path + ".profile.json"));
                if (profile == null || !ValidByte(profile.ProfileRaw)) throw new InvalidDataException("Profile backup is invalid.");
                value.ProfileRaw = profile.ProfileRaw;
            }
            return value;
        }
        private static bool ValidByte(int? value) { return value.HasValue && value.Value >= 0 && value.Value <= 255; }
    }
    public sealed class ThresholdResult
    {
        public byte BeforeUpper, BeforeLower, AfterUpper, AfterLower;
        public byte BeforeProfile, AfterProfile;
        public bool Changed;
    }

    public static class ThresholdManager
    {
        public const uint UpperAddress = 0x07B9;
        public const uint LowerAddress = 0x07D0;
        public const uint ProfileAddress = 0x07A6;
        public const byte ProfileMask = 0x30;
        // Some EC/GCU revisions return the upper limit with bit 7 set (0x80 + percent).
        // Keep raw bytes for backup/rollback, but expose and verify the 7-bit percentage.
        public const byte UpperValueMask = 0x7F;
        public const string DriverDirectory = @"C:\Program Files\OEM\BatteryManager";
        public const string DriverPath = DriverDirectory + "\\ACPIDriverDll.dll";
        private static readonly object Sync = new object();

        public static void EnsureDefaults(string dllPath, string path = null)
        {
            lock (Sync) using (var operation = new EcOperationLock())
            {
                path = path ?? EcDefaults.FilePath;
                if (File.Exists(path))
                {
                    var existing = EcDefaults.Load(path);
                    if (!existing.ProfileRaw.HasValue)
                    {
                        using (var ec = new EcClient(dllPath))
                            Publish(path + ".profile.json", new EcDefaults { ProfileRaw = ec.Read(ProfileAddress), CapturedAtUtc = DateTime.UtcNow.ToString("o") });
                        EcDefaults.Load(path);
                    }
                    return;
                }
                using (var ec = new EcClient(dllPath))
                    Publish(path, new EcDefaults { UpperRaw = ec.Read(UpperAddress), LowerRaw = ec.Read(LowerAddress), ProfileRaw = ec.Read(ProfileAddress), CapturedAtUtc = DateTime.UtcNow.ToString("o") });
                EcDefaults.Load(path);
            }
        }
        private static void Publish(string path, EcDefaults snapshot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(snapshot));
                try { File.Move(temp, path); }
                catch (IOException) { if (!File.Exists(path)) throw; }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static ThresholdResult Read(string dllPath)
        {
            lock (Sync) using (var operation = new EcOperationLock()) using (var ec = new EcClient(dllPath)) return ReadState(ec);
        }
        private static ThresholdResult ReadState(IEcAccessor ec)
        {
            byte upper = ec.Read(UpperAddress), lower = ec.Read(LowerAddress), profile = ec.Read(ProfileAddress);
            upper = NormalizeUpper(upper);
            return new ThresholdResult { BeforeUpper = upper, AfterUpper = upper, BeforeLower = lower, AfterLower = lower,
                BeforeProfile = (byte)((profile & ProfileMask) >> 4), AfterProfile = (byte)((profile & ProfileMask) >> 4) };
        }
        internal static ThresholdResult ReadStateForTests(IEcAccessor ec) { return ReadState(ec); }
        public static ThresholdResult Apply(BatteryConfig config, bool skipLower = false, string defaultsPath = null)
        {
            config.Validate();
            lock (Sync)
            {
                // Disabled settings perform no hardware access and need no backup.
                if (config.UpperLimit == 0) return new ThresholdResult();
                using (var operation = new EcOperationLock())
                {
                    EnsureDefaults(config.OemDllPath, defaultsPath);
                    using (var ec = new EcClient(config.OemDllPath)) return ApplyToEc(ec, config, true, Settle);
                }
            }
        }
        internal static ThresholdResult ApplyToEc(IEcAccessor ec, BatteryConfig config, bool skipLower, Action settle = null)
        {
            config.Validate();
            if (config.UpperLimit == 0) return new ThresholdResult();
            var changes = new List<Change>();
            int profile = config.UpperLimit <= 60 ? 2 : config.UpperLimit <= 80 ? 1 : 0;
            // Select the profile before writing the custom threshold: firmware may set a preset on mode change.
            changes.Add(new Change(ProfileAddress, (byte)(profile << 4), ProfileMask));
            changes.Add(new Change(UpperAddress, (byte)config.UpperLimit, UpperValueMask));

            return Execute(ec, changes, settle);
        }
        public static ThresholdResult Reset(string dllPath, bool skipLower = false)
        {
            lock (Sync) using (var operation = new EcOperationLock())
            {
                var defaults = EcDefaults.Load(EcDefaults.FilePath);
                using (var ec = new EcClient(dllPath)) return ResetToEc(ec, defaults, true, Settle);
            }
        }
        internal static ThresholdResult ResetToEc(IEcAccessor ec, EcDefaults defaults, bool skipLower, Action settle = null)
        {
            var changes = new List<Change>();
            // Restore only the charging bits; unrelated mode bits may have changed since the backup.
            if (defaults.ProfileRaw.HasValue) changes.Add(new Change(ProfileAddress, (byte)defaults.ProfileRaw.Value, ProfileMask));
            changes.Add(new Change(UpperAddress, (byte)defaults.UpperRaw.Value, UpperValueMask));

            return Execute(ec, changes, settle);
        }
        private static void Settle() { Thread.Sleep(150); }
        private static byte NormalizeUpper(byte raw) { return (byte)(raw & UpperValueMask); }
        private sealed class Change
        {
            internal readonly uint Address;
            internal readonly byte Value, Mask;
            internal byte Before;
            internal bool Attempted;
            internal Change(uint address, byte value, byte mask) { Address = address; Value = value; Mask = mask; }
        }
        private static void WriteVerified(IEcAccessor ec, uint address, byte value, byte mask)
        {
            var current = ec.Read(address);
            var target = (byte)((current & ~mask) | (value & mask));
            if (current != target) ec.Write(address, target);
            if ((ec.Read(address) & mask) != (target & mask))
                throw new InvalidOperationException(string.Format("EC 0x{0:X4} write verification failed.", address));
        }
        private static ThresholdResult Execute(IEcAccessor ec, List<Change> changes, Action settle)
        {
            var result = ReadState(ec);
            // Snapshot every target before any write, including raw bytes used for rollback.
            foreach (var change in changes) change.Before = ec.Read(change.Address);
            try
            {
                foreach (var change in changes)
                {
                    // Mark before calling the driver: a failed call may already have changed hardware.
                    change.Attempted = true;
                    WriteVerified(ec, change.Address, change.Value, change.Mask);
                    if (change.Address == ProfileAddress && settle != null) settle();
                }
                // Check immediately and after a short settling interval; never blindly repeat writes.
                for (int pass = 0; pass < 2; ++pass)
                {
                    if (pass != 0 && settle != null) settle();
                    foreach (var change in changes)
                        if ((ec.Read(change.Address) & change.Mask) != (change.Value & change.Mask))
                            throw new InvalidOperationException(string.Format("EC 0x{0:X4} final verification failed; firmware or the OEM console may have overwritten the setting.", change.Address));
                }
                var after = ReadState(ec);
                result.AfterUpper = after.AfterUpper; result.AfterLower = after.AfterLower; result.AfterProfile = after.AfterProfile;
                result.Changed = result.BeforeUpper != result.AfterUpper || result.BeforeLower != result.AfterLower || result.BeforeProfile != result.AfterProfile;
                return result;
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                // Profile restore can reset the preset threshold, so restore profile first, then upper. LowerAddress is never written.
                bool started = changes.Exists(c => c.Attempted);
                foreach (var change in changes)
                    if (started)
                        try
                        {
                            WriteVerified(ec, change.Address, change.Before, change.Mask);
                            if (change.Address == ProfileAddress && settle != null) settle();
                        }
                        catch (Exception rollback) { errors.Add(rollback); }
                if (started && settle != null)
                    try { settle(); } catch (Exception rollback) { errors.Add(rollback); }
                foreach (var change in changes)
                    if (started)
                        try
                        {
                            if ((ec.Read(change.Address) & change.Mask) != (change.Before & change.Mask))
                                throw new InvalidOperationException(string.Format("EC 0x{0:X4} rollback verification failed.", change.Address));
                        }
                        catch (Exception rollback) { errors.Add(rollback); }
                if (errors.Count > 1) throw new AggregateException("EC operation failed; rollback was incomplete.", errors);
                throw new InvalidOperationException("EC operation failed; previous values restored.", failure);
            }
        }
    }
}
