using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text;
using MechrevoBatteryManager;

class FakeEc : IEcAccessor
{
    public readonly Dictionary<uint, byte> Values = new Dictionary<uint, byte> {
        { ThresholdManager.ProfileAddress, 0xC5 }, { ThresholdManager.UpperAddress, 100 }, { ThresholdManager.LowerAddress, 19 }
    };
    public int Writes;
    public uint FailAddress;
    public bool FailOnce, PresetSideEffect, IgnoreWrites, RollbackFails;
    public byte Read(uint address) { return Values[address]; }
    public void Write(uint address, byte value)
    {
        if (address != ThresholdManager.UpperAddress && address != ThresholdManager.ProfileAddress)
            throw new Exception("Unexpected write to 0x" + address.ToString("X4"));
        Writes++;
        if (!IgnoreWrites) Values[address] = value;
        if (address == ThresholdManager.ProfileAddress && PresetSideEffect) Values[ThresholdManager.UpperAddress] = 60;
        if (FailOnce && address == FailAddress) { FailOnce = false; throw new Exception("Driver failed after mutation"); }
        if (RollbackFails && Writes > 1) throw new Exception("Rollback driver failure");
    }
}
class Tests
{
    static int passed;
    static void Check(bool success, string message) { if (!success) throw new Exception(message); }
    static BatteryConfig Config(int upper) { return new BatteryConfig { UpperLimit = upper, LowerLimit = 0, OemDllPath = "fake.dll" }; }
    static void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    static void Fails(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected failure"); }
    static byte[] DriverFixture()
    {
        var data = new byte[0x600];
        using (var writer = new BinaryWriter(new MemoryStream(data)))
        {
            Action<int, uint> put = delegate(int offset, uint value) { writer.BaseStream.Position = offset; writer.Write(value); };
            put(0, 0x5A4D); put(0x3C, 0x80); put(0x80, 0x4550); put(0x84, 0x00018664);
            put(0x94, 0x200000F0); put(0x98, 0x20B); put(0x98 + 108, 16); put(0x98 + 112, 0x1000);
            put(0x188 + 8, 0x400); put(0x188 + 12, 0x1000); put(0x188 + 16, 0x400); put(0x188 + 20, 0x200);
            put(0x200 + 24, 2); put(0x200 + 28, 0x1048); put(0x200 + 32, 0x1040);
            put(0x240, 0x1060); put(0x244, 0x1070);
            writer.BaseStream.Position = 0x260; writer.Write(Encoding.ASCII.GetBytes("ReadEC\0"));
            writer.BaseStream.Position = 0x270; writer.Write(Encoding.ASCII.GetBytes("WriteEC\0"));
        }
        return data;
    }
    static void InvalidDriver(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Expected invalid driver rejection");
    }
    static void Main()
    {
        foreach (int limit in new [] {1, 59, 60, 61, 75, 79, 80, 81, 99, 100})
        {
            int captured = limit;
            Run("official preset " + limit, delegate {
                var ec = new FakeEc(); var r = ThresholdManager.ApplyToEc(ec, Config(captured), false);
                int expected = captured <= 60 ? 2 : captured <= 80 ? 1 : 0;
                Check(r.AfterProfile == expected && r.AfterUpper == captured, "wrong preset");
                Check((ec.Values[ThresholdManager.ProfileAddress] & ~0x30) == 0xC5, "unrelated bits modified");
                Check(ec.Values[ThresholdManager.LowerAddress] == 19, "lower modified");
            });
        }
        Run("idempotent and lower never cleared", delegate {
            var ec = new FakeEc(); ThresholdManager.ApplyToEc(ec, Config(80), false); ec.Writes = 0;
            var r = ThresholdManager.ApplyToEc(ec, Config(80), true); Check(ec.Writes == 0 && !r.Changed, "redundant write");
        });
        Run("upper bit 7 is tolerated and preserved", delegate {
            var ec = new FakeEc();
            ec.Values[ThresholdManager.UpperAddress] = 0xE4; // 0x80 + 100
            var before = ThresholdManager.ReadStateForTests(ec);
            Check(before.BeforeUpper == 100, "high-bit upper was not normalized");
            var result = ThresholdManager.ApplyToEc(ec, Config(80), false);
            Check(result.BeforeUpper == 100 && result.AfterUpper == 80, "normalized result is wrong");
            Check(ec.Values[ThresholdManager.UpperAddress] == 0xD0, "upper high bit was not preserved");
            Check(ec.Values[ThresholdManager.LowerAddress] == 19, "lower modified");
        });
        Run("disabled accesses no EC", delegate { ThresholdManager.ApplyToEc(null, Config(0), false); });
        Run("lower input rejected", delegate { var c = Config(80); c.LowerLimit = 20; Fails(c.Validate); });
        foreach (uint address in new [] { ThresholdManager.ProfileAddress, ThresholdManager.UpperAddress })
        {
            uint captured = address;
            Run("rollback after mutation " + address.ToString("X4"), delegate {
                var ec = new FakeEc { FailOnce = true, FailAddress = captured, PresetSideEffect = true };
                Fails(delegate { ThresholdManager.ApplyToEc(ec, Config(80), false); });
                Check(ec.Values[ThresholdManager.ProfileAddress] == 0xC5 && ec.Values[ThresholdManager.UpperAddress] == 100, "rollback incomplete");
            });
        }
        Run("readback mismatch detected", delegate {
            var ec = new FakeEc { IgnoreWrites = true }; Fails(delegate { ThresholdManager.ApplyToEc(ec, Config(80), false); });
        });
        Run("rollback failure surfaced", delegate {
            var ec = new FakeEc { FailOnce = true, FailAddress = ThresholdManager.UpperAddress, RollbackFails = true };
            bool caught = false; try { ThresholdManager.ApplyToEc(ec, Config(80), false); } catch (AggregateException) { caught = true; }
            Check(caught, "rollback failure hidden");
        });
        Run("restore preserves unrelated profile bits and never writes lower", delegate {
            var ec = new FakeEc { PresetSideEffect = true };
            ThresholdManager.ResetToEc(ec, new EcDefaults { ProfileRaw = 0x20, UpperRaw = 95, LowerRaw = 0 }, false);
            Check(ec.Values[ThresholdManager.ProfileAddress] == 0xE5 && ec.Values[ThresholdManager.UpperAddress] == 95 && ec.Values[ThresholdManager.LowerAddress] == 19, "restore incorrect");
        });
        Run("v1.5 config and backup compatibility", delegate {
            string dir = Path.Combine(Path.GetTempPath(), "mbm-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                File.WriteAllText(Path.Combine(dir, "config.json"), "{\"UpperLimit\":80,\"LowerLimit\":40,\"ChargingProfilePolicy\":1,\"OemDllPath\":\"fake.dll\"}");
                var c = BatteryConfig.LoadFromDirectory(dir); c.Validate(); Check(c.LowerLimit == 0, "lower not migrated");
                var ec = new FakeEc(); var result = ThresholdManager.ApplyToEc(ec, c, false);
                Check(result.AfterProfile == 1, "legacy policy disabled automatic linking");
                string path = Path.Combine(dir, "Default.json"); string original = "{\"UpperRaw\":100,\"LowerRaw\":40}"; File.WriteAllText(path, original);
                Check(!EcDefaults.Load(path).ProfileRaw.HasValue, "old backup rejected");
                File.WriteAllText(path + ".profile.json", "{\"ProfileRaw\":32}");
                Check(EcDefaults.Load(path).ProfileRaw == 32 && File.ReadAllText(path) == original, "sidecar incorrect");
            } finally { Directory.Delete(dir, true); }
        });
        Run("late firmware override detected and rolled back", delegate {
            var ec = new FakeEc(); int waits = 0;
            Fails(delegate { ThresholdManager.ApplyToEc(ec, Config(75), false, delegate {
                waits++; if (waits == 2) ec.Values[ThresholdManager.UpperAddress] = 80;
            }); });
            Check(ec.Values[ThresholdManager.UpperAddress] == 100 && ec.Values[ThresholdManager.ProfileAddress] == 0xC5, "late override not restored");
        });
        Run("wait for profile preset before custom upper", delegate {
            var ec = new FakeEc(); int waits = 0;
            var r = ThresholdManager.ApplyToEc(ec, Config(75), false, delegate {
                waits++; if (waits == 1) ec.Values[ThresholdManager.UpperAddress] = 80;
            });
            Check(r.AfterUpper == 75 && r.AfterProfile == 1 && waits == 2, "custom upper lost to delayed preset");
        });
        Run("operation mutex rejects concurrent transaction and releases", delegate {
            string name = "mbm-test-lock-" + Guid.NewGuid().ToString("N");
            Exception failure = null; bool blocked = false;
            using (var held = new EcOperationLock(1000, name))
            {
                var thread = new Thread(delegate() {
                    try { using (var other = new EcOperationLock(50, name)) { } }
                    catch (InvalidOperationException) { blocked = true; }
                    catch (Exception ex) { failure = ex; }
                });
                thread.Start(); thread.Join();
            }
            Check(failure == null && blocked, "concurrent operation allowed");
            using (var released = new EcOperationLock(1000, name)) { }
        });
        Run("PE validation checks architecture, exports and malformed input without loading", delegate {
            string path = Path.Combine(Path.GetTempPath(), "mbm-driver-" + Guid.NewGuid().ToString("N") + ".dll");
            try
            {
                byte[] data = DriverFixture(); File.WriteAllBytes(path, data); OemDriverInfo.Validate(path);
                data[0x84] = 0x4C; data[0x85] = 0x01; File.WriteAllBytes(path, data); InvalidDriver(delegate { OemDriverInfo.Validate(path); });
                data = DriverFixture(); data[0x270] = (byte)'X'; File.WriteAllBytes(path, data); InvalidDriver(delegate { OemDriverInfo.Validate(path); });
                File.WriteAllBytes(path, new byte[] { 0x4D, 0x5A }); InvalidDriver(delegate { OemDriverInfo.Validate(path); });
                var report = OemDriverInfo.Describe(path, false); Check(report.Contains("校验失败") && report.Contains("SetPowerLedStatus"), "diagnostic hides failure or findings");
            }
            finally { File.Delete(path); }
        });
        Console.WriteLine("Passed " + passed + " tests. No real hardware was accessed.");
    }
}
