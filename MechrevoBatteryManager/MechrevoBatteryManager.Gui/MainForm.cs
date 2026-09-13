using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using MechrevoBatteryManager;

namespace MechrevoBatteryManager.Gui
{
    public sealed class MainForm : Form
    {
        private const string ServiceInstallDirectory = @"C:\Program Files\OEM\BatteryManager";
        private const string Series40Console = @"C:\Program Files\OEM\机械革命电竞控制台\AiStoneService\MyControlCenter\GCUService.exe";
        private const string Series50Console = @"C:\Program Files\OEM\机械革命控制中心\AiStoneService\MyControlCenter\GCUService.exe";
        private readonly NumericUpDown upper = new NumericUpDown { Minimum = 0, Maximum = 100, Width = 90 };
        private readonly NumericUpDown lower = new NumericUpDown { Minimum = 0, Maximum = 99, Width = 90 };
        private readonly NumericUpDown delay = new NumericUpDown { Minimum = 0, Maximum = 300, Width = 90 };
        private readonly CheckBox enabled = new CheckBox { Text = "Apply once after Windows starts", AutoSize = true };
        private readonly TextBox dllPath = new TextBox { Width = 390, AutoSize = true };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(610, 0) };
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Anchor = AnchorStyles.Left, IntegralHeight = true };
        private Label upperLabel, lowerLabel, delayLabel, dllLabel;
        private int nextRow;
        private bool English { get { return language.SelectedIndex == 1; } }
        private bool IsSeries50 { get; set; }

        public MainForm()
        {
            Text = "MechrevoBatteryManager"; ClientSize = new Size(660, 350); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 48, 20, 20), ColumnCount = 2, RowCount = 7, AutoSize = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var dllPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Left }; dllPanel.Controls.Add(dllPath); dllPanel.Controls.Add(Button("重新检索", delegate { SearchDriver(); }));
            upperLabel = Add(panel, "充电截止阈值 (%)", upper); lowerLabel = Add(panel, "复充启动阈值 (%)", lower); delayLabel = Add(panel, "启动延迟 (秒)", delay); dllLabel = Add(panel, "OEM 驱动 DLL", dllPanel);
            language.Items.AddRange(new object[] { "中文 / Chinese", "English / 英文" }); language.SelectedIndex = 0; language.SelectedIndexChanged += delegate { ApplyLanguage(); };
            var languageBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 18, 0) }; languageBar.Controls.Add(language); languageBar.Controls.Add(new Label { Text = "语言 / Language", AutoSize = true, Padding = new Padding(0, 5, 6, 0) });
            panel.Controls.Add(enabled, 1, 4);
            var actions = new FlowLayoutPanel { AutoSize = true };
            actions.Controls.Add(Button("读取 EC", delegate { ReadEc(); })); actions.Controls.Add(Button("立即应用", delegate { ApplyNow(); })); actions.Controls.Add(Button("保存", delegate { Save(); })); actions.Controls.Add(Button("恢复默认", delegate { RestoreDefaults(); })); actions.Controls.Add(Button("安装服务", delegate { InstallService(); })); actions.Controls.Add(Button("卸载服务", delegate { RemoveService(); }));
            panel.Controls.Add(actions, 1, 5); panel.Controls.Add(status, 1, 6); Controls.Add(panel); Controls.Add(languageBar); ApplyLanguage(); LoadConfig(); IsSeries50 = dllPath.Text.IndexOf("机械革命控制中心", StringComparison.OrdinalIgnoreCase) >= 0; lower.Value = 0; lower.Enabled = false; if (!Current().DriverSearchCompleted) DetectInstalledConsole();
        }
        private Label Add(TableLayoutPanel p, string name, Control c) { var label = new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left }; p.Controls.Add(label, 0, nextRow); p.Controls.Add(c, 1, nextRow); nextRow++; return label; }
        private static Button Button(string text, EventHandler click) { var b = new Button { Text = text, AutoSize = true }; b.Click += click; return b; }
        private BatteryConfig Current() { return new BatteryConfig { UpperLimit = (int)upper.Value, LowerLimit = IsSeries50 ? 0 : (int)lower.Value, StartupDelaySeconds = (int)delay.Value, Enabled = enabled.Checked, OemDllPath = dllPath.Text }; }
        private void LoadConfig() { var c = BatteryConfig.Load(); upper.Value = c.UpperLimit; lower.Value = c.LowerLimit; delay.Value = c.StartupDelaySeconds; enabled.Checked = c.Enabled; dllPath.Text = c.OemDllPath; }
        private void DetectInstalledConsole()
        {
            var detectedPath = File.Exists(Series40Console) ? Series40Console : (File.Exists(Series50Console) ? Series50Console : null);
            if (detectedPath == null) { var candidates = Directory.Exists(@"C:\Program Files\OEM") ? Directory.GetFiles(@"C:\Program Files\OEM", "ACPIDriverDll.dll", SearchOption.AllDirectories) : new string[0]; if (candidates.Length > 0) detectedPath = candidates[0]; }
            var config = Current(); config.DriverSearchCompleted = true;
            if (detectedPath == null)
            {
                config.Save();
                status.Text = English ? "No 40 or 50 series console found; the current OEM DLL path was kept." : "未检测到40系或50系控制台，保留当前 OEM DLL 路径。";
                return;
            }

            var series = detectedPath == Series40Console ? "40" : (detectedPath == Series50Console ? "50" : "OEM");
            IsSeries50 = detectedPath == Series50Console;
            lower.Value = 0; lower.Enabled = false;
            var detectedDll = detectedPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? detectedPath : Path.Combine(Path.GetDirectoryName(detectedPath), "ACPIDriverDll.dll");
            if (!File.Exists(detectedDll))
            {
                status.Text = English ? string.Format("{0} series console detected, but ACPIDriverDll.dll was not found.", series) : string.Format("你当前安装的是{0}系控制台，但未找到 ACPIDriverDll.dll。", series);
                return;
            }

            dllPath.Text = detectedDll;
            config.OemDllPath = detectedDll;
            config.Save();
            status.Text = English ? string.Format("{0} series console detected; the DLL path was updated.", series) : string.Format("你当前安装的是{0}系控制台，已自动更新 DLL 路径。", series);
        }
        private void ApplyLanguage() { bool en = English; Text = en ? "MechrevoBatteryManager" : "机械革命电池管理器"; upperLabel.Text = en ? "Charge limit (%)" : "充电截止阈值 (%)"; lowerLabel.Text = en ? "Recharge at (%)" : "复充启动阈值 (%)"; delayLabel.Text = en ? "Startup delay (s)" : "启动延迟 (秒)"; dllLabel.Text = en ? "OEM driver DLL" : "OEM 驱动 DLL"; enabled.Text = en ? "Apply once after Windows starts" : "开机后应用一次"; }
        private void SearchDriver() { Guard(delegate { DetectInstalledConsole(); }); }
        private void Guard(Action action) { try { action(); } catch (Exception ex) { status.Text = (English ? "Error: " : "错误：") + ex.Message; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        private void ReadEc() { Guard(delegate { var r = ThresholdManager.Read(dllPath.Text); status.Text = English ? string.Format("Current charge limit: {0}%.", r.BeforeUpper) : string.Format("当前充电上限为：{0}%", r.BeforeUpper); }); }
        private void ApplyNow() { Guard(delegate { var c = Current(); c.Validate(); if (MessageBox.Show(this, English ? string.Format("Write upper {0}% and lower {1}% to EC?", c.UpperLimit, c.LowerLimit) : string.Format("将上限 {0}%、下限 {1}% 写入 EC？", c.UpperLimit, c.LowerLimit), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; var r = ThresholdManager.Apply(c, IsSeries50); status.Text = English ? string.Format("Verified: upper={0}, lower={1}.", r.AfterUpper, r.AfterLower) : string.Format("已验证：上限={0}，下限={1}。", r.AfterUpper, r.AfterLower); }); }
        private void Save() { Guard(delegate { Current().Save(); status.Text = English ? "Configuration saved." : "配置已保存。"; }); }
        private void RestoreDefaults() { Guard(delegate { var r = ThresholdManager.Reset(dllPath.Text, IsSeries50); upper.Value = 0; lower.Value = 0; Current().Save(); status.Text = English ? string.Format("Defaults restored in EC: upper={0}, lower={1}{2}.", r.AfterUpper, r.AfterLower, IsSeries50 ? " (not written on 50 series)" : "") : string.Format("已将 EC 恢复默认：上限={0}，下限={1}{2}。", r.AfterUpper, r.AfterLower, IsSeries50 ? "（50系未写入下限）" : ""); }); }
        private string ServiceSourceExe() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MechrevoBatteryManager.Service.payload"); }
        private string ServiceExe() { return Path.Combine(ServiceInstallDirectory, "MechrevoBatteryManager.Service.exe"); }
        private string CoreSourceDll() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MechrevoBatteryManager.Core.dll"); }
        private void InstallService()
        {
            Guard(delegate
            {
                Current().Save();
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\MechrevoBatteryManager")) key.SetValue("UserProfile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), RegistryValueKind.String);
                if (!File.Exists(ServiceSourceExe()) || !File.Exists(CoreSourceDll())) throw new FileNotFoundException("The service payload and Core DLL must be next to this application.");
                Directory.CreateDirectory(ServiceInstallDirectory);
                File.Copy(ServiceSourceExe(), ServiceExe(), true);
                File.Copy(CoreSourceDll(), Path.Combine(ServiceInstallDirectory, "MechrevoBatteryManager.Core.dll"), true);
                var uninstallSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall-MechrevoBatteryManager.cmd");
                if (File.Exists(uninstallSource)) File.Copy(uninstallSource, Path.Combine(ServiceInstallDirectory, "Uninstall-MechrevoBatteryManager.cmd"), true);
                RunSc("stop MechrevoBatteryManager", true);
                RunSc("create MechrevoBatteryManager binPath= \"" + ServiceExe() + "\" start= delayed-auto DisplayName= \"Mechrevo Battery Manager\"");
                RunSc("description MechrevoBatteryManager \"Applies configured battery thresholds once after startup\"");
                RunSc("start MechrevoBatteryManager");
                status.Text = English ? "Service installed at " + ServiceInstallDirectory : "服务已安装到 " + ServiceInstallDirectory;
            });
        }
        private void RemoveService() { Guard(delegate { RunSc("stop MechrevoBatteryManager", true); RunSc("delete MechrevoBatteryManager"); var root = ServiceInstallDirectory.Replace("'", "''"); Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -Command \"$root='" + root + "'; Start-Sleep -Seconds 2; Remove-Item -LiteralPath $root -Recurse -Force\"") { UseShellExecute = false, CreateNoWindow = true }); status.Text = English ? "Service removed." : "服务已卸载。"; }); }
        private static void RunSc(string args, bool ignoreFailure = false) { var p = Process.Start(new ProcessStartInfo("sc.exe", args) { UseShellExecute = false, CreateNoWindow = true }); p.WaitForExit(); if (!ignoreFailure && p.ExitCode != 0) throw new InvalidOperationException("sc.exe failed with exit code " + p.ExitCode); }
    }
}
