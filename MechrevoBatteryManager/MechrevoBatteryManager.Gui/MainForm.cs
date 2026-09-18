using System;
using System.Collections.Generic;
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
        private int startupDelaySeconds = 10;
        private readonly CheckBox enabled = new CheckBox { Text = "Apply once after Windows starts", AutoSize = true };
        private readonly TextBox dllPath = new TextBox { Width = 390, AutoSize = true };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(610, 0) };
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Anchor = AnchorStyles.Left, IntegralHeight = true };
        private Label upperLabel, lowerLabel, dllLabel;
        private int nextRow;
        private readonly List<Action> updateButtonLanguages = new List<Action>();
        private Func<string> statusMessage;
        private void SetStatus(Func<string> message) { statusMessage = message; status.Text = message(); }
        private bool English { get { return language.SelectedIndex == 1; } }
        private bool IsSeries50 { get; set; }

        public MainForm()
        {
            Text = "MechrevoBatteryManager"; ClientSize = new Size(660, 350); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 48, 20, 20), ColumnCount = 2, RowCount = 6, AutoSize = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var dllPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Left }; dllPanel.Controls.Add(dllPath); dllPanel.Controls.Add(Button("重新检索", "Rescan", delegate { SearchDriver(); }));
            upperLabel = Add(panel, "充电截止阈值 (%)", upper); lowerLabel = Add(panel, "复充启动阈值 (%)", lower); dllLabel = Add(panel, "OEM 驱动 DLL", dllPanel);
            language.Items.AddRange(new object[] { "中文 / Chinese", "English / 英文" }); language.SelectedIndex = 0; language.SelectedIndexChanged += delegate { ApplyLanguage(); };
            var languageBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 18, 0) }; languageBar.Controls.Add(language); languageBar.Controls.Add(new Label { Text = "语言 / Language", AutoSize = true, Padding = new Padding(0, 5, 6, 0) });
            panel.Controls.Add(enabled, 1, 3);
            enabled.Click += delegate {
                if (enabled.Checked) MessageBox.Show(this,
                    English ? "For applying saved settings at startup, we recommend using Install service. The service is designed to stop after one attempt and release its memory, so you do not need to keep this app open."
                    : "如需开机自动应用已保存的配置，建议使用“安装服务”。服务会在尝试应用配置后自动停止并释放内存，无需一直保持软件开启。",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            var actions = new FlowLayoutPanel { AutoSize = true };
            actions.Controls.Add(Button("读取 EC", "Read EC", delegate { ReadEc(); })); actions.Controls.Add(Button("立即应用", "Apply now", delegate { ApplyNow(); })); actions.Controls.Add(Button("保存", "Save", delegate { Save(); })); actions.Controls.Add(Button("恢复默认", "Restore defaults", delegate { RestoreDefaults(); })); actions.Controls.Add(Button("安装服务", "Install service", delegate { InstallService(); })); actions.Controls.Add(Button("卸载服务", "Uninstall service", delegate { RemoveService(); }));
            panel.Controls.Add(actions, 1, 4); panel.Controls.Add(status, 1, 5); Controls.Add(panel); Controls.Add(languageBar); ApplyLanguage(); LoadConfig(); IsSeries50 = dllPath.Text.IndexOf("机械革命控制中心", StringComparison.OrdinalIgnoreCase) >= 0; lower.Value = 0; lower.Enabled = false; if (!Current().DriverSearchCompleted) DetectInstalledConsole();
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Guard(delegate { ThresholdManager.EnsureDefaults(dllPath.Text); });
        }
        private Label Add(TableLayoutPanel p, string name, Control c) { var label = new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left }; p.Controls.Add(label, 0, nextRow); p.Controls.Add(c, 1, nextRow); nextRow++; return label; }
        private Button Button(string chinese, string english, EventHandler click)
        {
            var button = new Button { AutoSize = true };
            Action update = () => button.Text = English ? english : chinese;
            updateButtonLanguages.Add(update);
            update();
            button.Click += click;
            return button;
        }
        private BatteryConfig Current() { return new BatteryConfig { UpperLimit = (int)upper.Value, LowerLimit = IsSeries50 ? 0 : (int)lower.Value, StartupDelaySeconds = startupDelaySeconds, Enabled = enabled.Checked, OemDllPath = dllPath.Text }; }
        private void LoadConfig() { var c = BatteryConfig.Load(); upper.Value = c.UpperLimit; lower.Value = c.LowerLimit; startupDelaySeconds = c.StartupDelaySeconds; enabled.Checked = c.Enabled; dllPath.Text = c.OemDllPath; }
        private void DetectInstalledConsole()
        {
            var detectedPath = File.Exists(Series40Console) ? Series40Console : (File.Exists(Series50Console) ? Series50Console : null);
            if (detectedPath == null) { var candidates = Directory.Exists(@"C:\Program Files\OEM") ? Directory.GetFiles(@"C:\Program Files\OEM", "ACPIDriverDll.dll", SearchOption.AllDirectories) : new string[0]; if (candidates.Length > 0) detectedPath = candidates[0]; }
            var config = Current(); config.DriverSearchCompleted = true;
            if (detectedPath == null)
            {
                config.Save();
                SetStatus(() => English ? "No 40 or 50 series console found; the current OEM DLL path was kept." : "未检测到40系或50系控制台，保留当前 OEM DLL 路径。");
                return;
            }

            var series = detectedPath == Series40Console ? "40" : (detectedPath == Series50Console ? "50" : "OEM");
            IsSeries50 = detectedPath == Series50Console;
            lower.Value = 0; lower.Enabled = false;
            var detectedDll = detectedPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? detectedPath : Path.Combine(Path.GetDirectoryName(detectedPath), "ACPIDriverDll.dll");
            if (!File.Exists(detectedDll))
            {
                SetStatus(() => English ? string.Format("{0} series console detected, but ACPIDriverDll.dll was not found.", series) : string.Format("你当前安装的是{0}系控制台，但未找到 ACPIDriverDll.dll。", series));
                return;
            }

            dllPath.Text = detectedDll;
            config.OemDllPath = detectedDll;
            config.Save();
            SetStatus(() => English ? string.Format("{0} series console detected; the DLL path was updated.", series) : string.Format("你当前安装的是{0}系控制台，已自动更新 DLL 路径。", series));
        }
        private void ApplyLanguage() { bool en = English; Text = en ? "MechrevoBatteryManager" : "机械革命电池管理器"; upperLabel.Text = en ? "Charge limit (%)" : "充电截止阈值 (%)"; lowerLabel.Text = en ? "Recharge at (%)" : "复充启动阈值 (%)"; dllLabel.Text = en ? "OEM driver DLL" : "OEM 驱动 DLL"; enabled.Text = en ? "Apply once after Windows starts" : "开机后应用一次"; foreach (var update in updateButtonLanguages) update(); if (statusMessage != null) status.Text = statusMessage(); }
        private void SearchDriver() { Guard(delegate { DetectInstalledConsole(); }); }
        private void Guard(Action action) { try { action(); } catch (Exception ex) { var localized = ex as StatusException; SetStatus(() => (English ? "Error: " : "错误：") + (localized == null ? ex.Message : localized.Render())); MessageBox.Show(this, status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        private sealed class StatusException : Exception
        {
            public readonly Func<string> Render;
            public StatusException(Func<string> render) : base(render()) { Render = render; }
        }
        private void ReadEc() { Guard(delegate { var r = ThresholdManager.Read(dllPath.Text); SetStatus(() => English ? string.Format("Current charge limit: {0}%.", r.BeforeUpper) : string.Format("当前充电上限为：{0}%", r.BeforeUpper)); }); }
        private void ApplyNow() { Guard(delegate { var c = Current(); c.Validate(); if (MessageBox.Show(this, English ? string.Format("Write upper {0}% and lower {1}% to EC?", c.UpperLimit, c.LowerLimit) : string.Format("将上限 {0}%、下限 {1}% 写入 EC？", c.UpperLimit, c.LowerLimit), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; var r = ThresholdManager.Apply(c, IsSeries50); SetStatus(() => English ? string.Format("Verified: upper={0}, lower={1}.", r.AfterUpper, r.AfterLower) : string.Format("已验证：上限={0}，下限={1}。", r.AfterUpper, r.AfterLower)); }); }
        private void Save() { Guard(delegate { Current().Save(); SetStatus(() => English ? "Configuration saved." : "配置已保存。"); }); }
        private void RestoreDefaults()
        {
            Guard(delegate {
                var r = ThresholdManager.Reset(dllPath.Text, IsSeries50);
                enabled.Checked = false;
                upper.Value = 0; lower.Value = 0; Current().Save();
                var skippedLower = IsSeries50;
                SetStatus(() => English
                    ? string.Format("Restored from Default.json: upper={0}, lower={1}{2}. Startup application disabled.", r.AfterUpper, r.AfterLower, skippedLower ? " (not written on 50 series)" : "")
                    : string.Format("已从 Default.json 恢复：上限={0}，下限={1}{2}。已关闭开机应用。", r.AfterUpper, r.AfterLower, skippedLower ? "（50系未写入下限）" : ""));
            });
        }
        private string ServiceSourceExe() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MechrevoBatteryManager.Service.payload"); }
        private string ServiceExe() { return Path.Combine(ServiceInstallDirectory, "MechrevoBatteryManager.Service.exe"); }
        private string CoreSourceDll() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MechrevoBatteryManager.Core.dll"); }
        private bool ConfigureServiceDelay()
        {
            using (var dialog = new Form { Text = English ? "Install service" : "安装服务", Font = Font,
                ClientSize = new Size(440, 190), FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false })
            {
                var label = new Label { Text = English ? "Service auto-start delay (seconds):" : "服务自启动延迟时间（秒）：",
                    AutoSize = true, Location = new Point(20, 20) };
                var input = new NumericUpDown { Minimum = 0, Maximum = 300, Value = 10, Width = 100, Location = new Point(20, 48) };
                var hint = new Label { Text = English
                    ? "Wait this long after the service starts before applying settings. Default: 10 seconds; range: 0–300. Windows may delay the service start separately."
                    : "服务启动后，等待此时间再应用配置。默认 10 秒，可设置 0–300 秒；Windows 本身还可能延后启动服务。",
                    AutoSize = true, MaximumSize = new Size(400, 0), Location = new Point(20, 82) };
                var confirm = new Button { Text = English ? "Install" : "确认安装", DialogResult = DialogResult.OK, Size = new Size(100, 30), Location = new Point(200, 145) };
                var cancel = new Button { Text = English ? "Cancel" : "取消", DialogResult = DialogResult.Cancel, Size = new Size(100, 30), Location = new Point(310, 145) };
                dialog.Controls.AddRange(new Control[] { label, input, hint, confirm, cancel });
                dialog.AcceptButton = confirm; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                startupDelaySeconds = (int)input.Value;
                return true;
            }
        }
        private void InstallService()
        {
            Guard(delegate
            {
                if (!ConfigureServiceDelay()) return;
                Current().Save();
                ThresholdManager.EnsureDefaults(dllPath.Text);
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
                SetStatus(() => English ? "Service installed at " + ServiceInstallDirectory : "服务已安装到 " + ServiceInstallDirectory);
            });
        }
        private void ShowServiceMissing()
        {
            SetStatus(() => English ? "Service does not exist; no uninstall is needed." : "服务不存在，无需卸载。");
            MessageBox.Show(this, status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void RemoveService()
        {
            Guard(delegate {
                var queryResult = RunSc("query MechrevoBatteryManager", true);
                if (queryResult == 1060) { ShowServiceMissing(); return; }
                if (queryResult != 0) throw new StatusException(() => English
                    ? "Unable to check service status. Error code: " + queryResult
                    : "无法检查服务状态，错误码：" + queryResult);
                var stopResult = RunSc("stop MechrevoBatteryManager", true);
                if (stopResult == 1060) { ShowServiceMissing(); return; }
                // 1062 means the service is already stopped.
                if (stopResult != 0 && stopResult != 1062) throw new StatusException(() => English
                    ? "Unable to stop service. Error code: " + stopResult
                    : "无法停止服务，错误码：" + stopResult);
                var deleteResult = RunSc("delete MechrevoBatteryManager", true);
                if (deleteResult == 1060) { ShowServiceMissing(); return; }
                if (deleteResult != 0) throw new StatusException(() => English
                    ? "Unable to uninstall service. Error code: " + deleteResult
                    : "无法卸载服务，错误码：" + deleteResult);
                var root = ServiceInstallDirectory.Replace("'", "''");
                Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -Command \"$root='" + root + "'; Start-Sleep -Seconds 2; Remove-Item -LiteralPath $root -Recurse -Force\"") { UseShellExecute = false, CreateNoWindow = true });
                SetStatus(() => English ? "Service removed." : "服务已卸载。");
            });
        }
        private static int RunSc(string args, bool ignoreFailure = false)
        {
            using (var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"), args) { UseShellExecute = false, CreateNoWindow = true }))
            {
                p.WaitForExit();
                if (!ignoreFailure && p.ExitCode != 0) throw new InvalidOperationException("sc.exe failed with exit code " + p.ExitCode);
                return p.ExitCode;
            }
        }
    }
}
