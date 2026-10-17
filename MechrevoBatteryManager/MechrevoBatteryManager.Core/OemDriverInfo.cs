using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MechrevoBatteryManager
{
    public static class OemDriverInfo
    {
        public static void Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new InvalidOperationException("OEM DLL must have an absolute path.");
            var exports = ReadExports(path);
            if (!exports.Contains("ReadEC") || !exports.Contains("WriteEC"))
                throw new InvalidDataException("OEM DLL must export ReadEC and WriteEC.");
        }
        // Read PE metadata only. Never load the DLL or run its initialization code here.
        internal static HashSet<string> ReadExports(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > 64 * 1024 * 1024) throw new InvalidDataException("OEM DLL is too large.");
            var bytes = File.ReadAllBytes(path);
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Not a PE file.");
                    reader.BaseStream.Position = 0x3C;
                    int pe = reader.ReadInt32();
                    reader.BaseStream.Position = pe;
                    if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664)
                        throw new InvalidDataException("An x64 OEM DLL is required.");
                    int count = reader.ReadUInt16();
                    reader.BaseStream.Position = pe + 20;
                    int optionalSize = reader.ReadUInt16();
                    ushort characteristics = reader.ReadUInt16();
                    if ((characteristics & 0x2000) == 0) throw new InvalidDataException("A DLL is required.");
                    int optional = pe + 24;
                    if (reader.ReadUInt16() != 0x20B || optionalSize < 120 || count < 1 || count > 96)
                        throw new InvalidDataException("Invalid x64 PE headers.");
                    reader.BaseStream.Position = optional + 108;
                    if (reader.ReadUInt32() < 1) throw new InvalidDataException("No export directory.");
                    uint exportRva = reader.ReadUInt32();
                    var sections = new List<uint[]>();
                    for (int i = 0; i < count; ++i)
                    {
                        reader.BaseStream.Position = optional + optionalSize + i * 40 + 8;
                        reader.ReadUInt32();
                        uint va = reader.ReadUInt32(), rawSize = reader.ReadUInt32(), rawOffset = reader.ReadUInt32();
                        sections.Add(new uint[] { va, rawSize, rawOffset });
                    }
                    Func<uint, long> offset = delegate(uint address)
                    {
                        foreach (var section in sections)
                            if (address >= section[0] && (ulong)address - section[0] < section[1])
                            {
                                long value = (long)section[2] + address - section[0];
                                if (value >= 0 && value < bytes.Length) return value;
                            }
                        throw new InvalidDataException("Invalid PE export address.");
                    };
                    reader.BaseStream.Position = offset(exportRva) + 24;
                    uint namesCount = reader.ReadUInt32();
                    reader.ReadUInt32();
                    uint namesRva = reader.ReadUInt32();
                    if (namesCount > 100000) throw new InvalidDataException("Invalid export count.");
                    long namesOffset = offset(namesRva);
                    var exports = new HashSet<string>(StringComparer.Ordinal);
                    for (uint i = 0; i < namesCount; ++i)
                    {
                        reader.BaseStream.Position = namesOffset + 4L * i;
                        uint nameRva = reader.ReadUInt32();
                        reader.BaseStream.Position = offset(nameRva);
                        var name = new StringBuilder();
                        byte value;
                        while ((value = reader.ReadByte()) != 0)
                        {
                            if (name.Length >= 256) throw new InvalidDataException("Invalid export name.");
                            name.Append((char)value);
                        }
                        exports.Add(name.ToString());
                    }
                    return exports;
                }
            }
            catch (EndOfStreamException ex) { throw new InvalidDataException("Truncated OEM DLL.", ex); }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid OEM DLL.", ex); }
        }
        public static string Describe(string dllPath, bool english)
        {
            var text = new StringBuilder();
            text.AppendLine(english ? "OEM driver diagnostics (files only; no EC access)" : "官方驱动诊断（只读取文件，不访问 EC）");
            text.AppendLine("DLL: " + dllPath);
            try
            {
                Validate(dllPath);
                text.AppendLine((english ? "DLL version: " : "DLL 版本：") + FileVersionInfo.GetVersionInfo(dllPath).FileVersion);
                text.AppendLine("x64 / ReadEC / WriteEC: OK");
            }
            catch (Exception ex) { text.AppendLine((english ? "Driver validation failed: " : "驱动校验失败：") + ex.Message); }
            try
            {
                string service = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dllPath)), "GCUService.exe");
                text.AppendLine("GCUService: " + service);
                text.AppendLine(File.Exists(service) ? "GCU version: " + FileVersionInfo.GetVersionInfo(service).FileVersion : (english ? "GCUService was not found next to the DLL." : "DLL 同目录未找到 GCUService。"));
            }
            catch (Exception ex) { text.AppendLine("GCUService: " + ex.Message); }
            text.AppendLine();
            text.AppendLine(english ? "Writes: upper 0x07B9; charging profile 0x07A6 bits[5:4]. Lower 0x07D0 is never written." : "写入范围：上限 0x07B9、档位 0x07A6 bits[5:4]。下限 0x07D0 永不写入。");
            text.AppendLine(english ? "Automatic profile: 1–60% stationary; 61–80% balanced; 81–100% capacity. Custom ranges are this app's policy." : "自动联动：1–60% 工作站；61–80% 平衡；81–100% 长效。自定义区间是本软件的策略。");
            text.AppendLine();
            text.AppendLine(english ? "Local GCU 1.0.2.70 static inspection found SetBatteryChargingLimit_Up(Int32) and SetPowerLedStatus(UInt32). Their implementations are protected. This finding does not verify other installed versions." : "本机 GCU 1.0.2.70 静态检查发现 SetBatteryChargingLimit_Up(Int32) 和 SetPowerLedStatus(UInt32)，但实现已被保护；该结论不代表其他版本已验证。");
            text.AppendLine(english ? "Charging-light controls are not included in v2.1." : "v2.1 不包含充电灯控制功能。");
            text.AppendLine(english ? "NVRAM battery fields and BatteryProtection/Control exist in the inspected GCU, but their write protocol is unknown and is not used here." : "已发现 NVRAM 电池字段和 BatteryProtection/Control，但写入协议未确认，本程序不调用这些路径。");
            return text.ToString();
        }
    }
}
