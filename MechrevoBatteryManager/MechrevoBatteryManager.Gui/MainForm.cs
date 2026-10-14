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
        private readonly NumericUpDown upper = new NumericUpDown { Minimum = 0, Maximum = 100, Width = 90 };
        private readonly NumericUpDown lower = new NumericUpDown { Minimum = 0, Maximum = 99, Width = 90 };
        private int startupDelaySeconds = 10;
        private bool driverSearchCompleted;
        private bool updatingLanguage;
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
        private readonly bool initializeHardware;

        public MainForm() : this(true) { }
        internal MainForm(bool initializeHardware)
        {
            this.initializeHardware = initializeHardware;
            Text = "MechrevoBatteryManager"; ClientSize = new Size(790, 450); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 48, 20, 20), ColumnCount = 2, RowCount = 7, AutoSize = true };
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
            var actions = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(600, 0) };
            actions.Controls.Add(Button("读取 EC", "Read EC", delegate { ReadEc(); })); actions.Controls.Add(Button("立即应用", "Apply now", delegate { ApplyNow(); })); actions.Controls.Add(Button("保存", "Save", delegate { Save(); })); actions.Controls.Add(Button("恢复默认", "Restore defaults", delegate { RestoreDefaults(); })); actions.Controls.Add(Button("安装服务", "Install service", delegate { InstallService(); })); actions.Controls.Add(Button("卸载服务", "Uninstall service", delegate { RemoveService(); }));
            actions.Controls.Add(Button("驱动诊断", "Diagnostics", delegate { ShowDiagnostics(); }));
            panel.Controls.Add(actions, 1, 5); panel.Controls.Add(status, 1, 6); Controls.Add(panel); Controls.Add(languageBar); ApplyLanguage();
            lower.Value = 0; lower.Enabled = false;
            if (initializeHardware) Guard(delegate {
                EnsureBundledDriver();
                LoadConfig(); IsSeries50 = dllPath.Text.IndexOf("机械革命控制中心", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!driverSearchCompleted) DetectInstalledConsole();
            });
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (initializeHardware) Guard(delegate { ThresholdManager.EnsureDefaults(dllPath.Text); });
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
        private BatteryConfig Current() { return new BatteryConfig { UpperLimit = (int)upper.Value, LowerLimit = 0, StartupDelaySeconds = startupDelaySeconds, Enabled = enabled.Checked, OemDllPath = dllPath.Text, DriverSearchCompleted = driverSearchCompleted }; }
        private void LoadConfig() { var c = BatteryConfig.Load(); upper.Value = c.UpperLimit; lower.Value = c.LowerLimit; startupDelaySeconds = c.StartupDelaySeconds; enabled.Checked = c.Enabled; dllPath.Text = string.IsNullOrWhiteSpace(c.OemDllPath) ? ThresholdManager.DriverPath : c.OemDllPath; driverSearchCompleted = c.DriverSearchCompleted; }
        private void DetectInstalledConsole()
        {
            string detectedDll = File.Exists(ThresholdManager.DriverPath) ? ThresholdManager.DriverPath : null;
            if (detectedDll != null)
                try { OemDriverInfo.Validate(detectedDll); }
                catch { detectedDll = null; }
            driverSearchCompleted = true;
            var config = Current();
            if (detectedDll == null)
            {
                config.Save();
                SetStatus(() => English ? "No compatible x64 OEM DLL with ReadEC/WriteEC was found. Use Diagnostics to check the configured path." : "未找到带 ReadEC/WriteEC 导出的兼容 x64 OEM DLL，请通过“驱动诊断”检查当前路径。");
                return;
            }
            dllPath.Text = detectedDll;
            config.OemDllPath = detectedDll;
            config.Save();
            SetStatus(() => English ? "Bundled OEM driver installed in " + ThresholdManager.DriverDirectory : "已将内置 OEM 驱动安装到 " + ThresholdManager.DriverDirectory);
        }
        private string BundledDriverPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "driver", "ACPIDriverDll.dll"); }
        private void EnsureBundledDriver()
        {
            if (File.Exists(ThresholdManager.DriverPath)) return;
            var source = BundledDriverPath();
            if (!File.Exists(source)) return;
            Directory.CreateDirectory(ThresholdManager.DriverDirectory);
            File.Copy(source, ThresholdManager.DriverPath, false);
            OemDriverInfo.Validate(ThresholdManager.DriverPath);
        }
        private void ApplyLanguage() { if (updatingLanguage) return; updatingLanguage = true; try { bool en = English; Text = en ? "MechrevoBatteryManager" : "机械革命电池管理器"; upperLabel.Text = en ? "Charge limit (%)" : "充电截止阈值 (%)"; lowerLabel.Text = en ? "Lower (not written)" : "下限（不写入）"; dllLabel.Text = en ? "OEM driver DLL" : "OEM 驱动 DLL"; enabled.Text = en ? "Apply once after Windows starts" : "开机后应用一次";
            foreach (var update in updateButtonLanguages) update(); if (statusMessage != null) status.Text = statusMessage(); } finally { updatingLanguage = false; } }
        private void ShowDiagnostics()
        {
            Guard(delegate {
                using (var dialog = new Form { Text = English ? "OEM diagnostics" : "官方驱动诊断", Font = Font,
                    ClientSize = new Size(760, 430), StartPosition = FormStartPosition.CenterParent })
                {
                    var details = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                        WordWrap = false, Dock = DockStyle.Fill, Text = OemDriverInfo.Describe(dllPath.Text, English) };
                    dialog.Controls.Add(details);
                    dialog.ShowDialog(this);
                }
            });
        }
        private void SearchDriver() { Guard(delegate { DetectInstalledConsole(); }); }
        private void Guard(Action action) { try { action(); } catch (Exception ex) { var localized = ex as StatusException; SetStatus(() => (English ? "Error: " : "错误：") + (localized == null ? ex.Message : localized.Render())); MessageBox.Show(this, status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        private sealed class StatusException : Exception
        {
            public readonly Func<string> Render;
            public StatusException(Func<string> render) : base(render()) { Render = render; }
        }
        private void ReadEc() { Guard(delegate { var r = ThresholdManager.Read(dllPath.Text); SetStatus(() => English ? string.Format("EC upper={0}, charging profile={1} (0=capacity, 1=balanced, 2=stationary).", r.BeforeUpper, r.BeforeProfile) : string.Format("EC 上限={0}，充电档位={1}（0=长效，1=平衡，2=工作站）。", r.BeforeUpper, r.BeforeProfile)); }); }
        private void ApplyNow() { Guard(delegate { var c = Current(); c.Validate(); if (MessageBox.Show(this, English ? string.Format("Apply upper {0}% (0 disables application)? Charging profile follows the threshold automatically; lower is never written.", c.UpperLimit) : string.Format("应用上限 {0}%（0 为停用）？充电档位自动联动，下限不写入。", c.UpperLimit), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; var r = ThresholdManager.Apply(c, IsSeries50); if (c.UpperLimit == 0) SetStatus(() => English ? "Application disabled; EC was not accessed. Use Restore defaults to restore hardware." : "已停用应用，本次未访问 EC；如需恢复硬件状态，请使用“恢复默认”。");
                else SetStatus(() => English ? string.Format("Verified EC upper={0}, profile={1}; lower not written. This does not verify actual charging behavior.", r.AfterUpper, r.AfterProfile) : string.Format("已验证 EC 上限={0}，档位={1}；下限未写入。实际充电停止仍需观察。", r.AfterUpper, r.AfterProfile)); }); }
        private void Save() { Guard(delegate { Current().Save(); SetStatus(() => English ? "Configuration saved." : "配置已保存。"); }); }
        private void RestoreDefaults()
        {
            Guard(delegate {
                var r = ThresholdManager.Reset(dllPath.Text, IsSeries50);
                enabled.Checked = false;
                upper.Value = 0; lower.Value = 0; Current().Save();
                var skippedLower = true;
                SetStatus(() => English
                    ? string.Format("Restored from Default.json: upper={0}, lower={1}{2}. Startup application disabled.", r.AfterUpper, r.AfterLower, skippedLower ? " (not written)" : "")
                    : string.Format("已从 Default.json 恢复：上限={0}，下限={1}{2}。已关闭开机应用。", r.AfterUpper, r.AfterLower, skippedLower ? "（下限未写入）" : ""));
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
                var input = new NumericUpDown { Minimum = 0, Maximum = 300, Value = startupDelaySeconds, Width = 100, Location = new Point(20, 48) };
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
                EnsureBundledDriver();
                dllPath.Text = ThresholdManager.DriverPath;
                Current().Save();
                ThresholdManager.EnsureDefaults(dllPath.Text);
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\MechrevoBatteryManager")) key.SetValue("UserProfile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), RegistryValueKind.String);
                if (!File.Exists(ServiceSourceExe()) || !File.Exists(CoreSourceDll())) throw new FileNotFoundException("The service payload and Core DLL must be next to this application.");
                var query = RunSc("query MechrevoBatteryManager", true);
                if (query != 0 && query != 1060) throw new InvalidOperationException("Unable to query service: " + query);
                if (query == 0)
                {
                    using (var controller = new System.ServiceProcess.ServiceController("MechrevoBatteryManager"))
                    {
                        if (controller.Status != System.ServiceProcess.ServiceControllerStatus.Stopped)
                        {
                            if (controller.Status != System.ServiceProcess.ServiceControllerStatus.StopPending) controller.Stop();
                            controller.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                    }
                }
                Directory.CreateDirectory(ServiceInstallDirectory);
                File.Copy(ServiceSourceExe(), ServiceExe(), true);
                File.Copy(CoreSourceDll(), Path.Combine(ServiceInstallDirectory, "MechrevoBatteryManager.Core.dll"), true);
                var bundledDriver = BundledDriverPath();
                if (!File.Exists(bundledDriver)) throw new FileNotFoundException("Bundled ACPIDriverDll.dll is missing.", bundledDriver);
                File.Copy(bundledDriver, ThresholdManager.DriverPath, true);
                var uninstallSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall-MechrevoBatteryManager.cmd");
                if (File.Exists(uninstallSource)) File.Copy(uninstallSource, Path.Combine(ServiceInstallDirectory, "Uninstall-MechrevoBatteryManager.cmd"), true);
                RunSc((query == 1060 ? "create" : "config") + " MechrevoBatteryManager binPath= \"" + ServiceExe() + "\" start= delayed-auto DisplayName= \"Mechrevo Battery Manager\"");
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
