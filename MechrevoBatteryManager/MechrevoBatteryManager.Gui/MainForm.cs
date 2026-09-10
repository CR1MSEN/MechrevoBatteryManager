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
        private readonly NumericUpDown upper = new NumericUpDown { Minimum = 1, Maximum = 100, Width = 90 };
        private readonly NumericUpDown lower = new NumericUpDown { Minimum = 0, Maximum = 99, Width = 90 };
        private readonly NumericUpDown delay = new NumericUpDown { Minimum = 0, Maximum = 300, Width = 90 };
        private readonly CheckBox enabled = new CheckBox { Text = "Apply once after Windows starts", AutoSize = true };
        private readonly TextBox dllPath = new TextBox { Width = 500 };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(610, 0) };
        private int nextRow;

        public MainForm()
        {
            Text = "MechrevoBatteryManager"; ClientSize = new Size(660, 350); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 8, AutoSize = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Add(panel, "Charge limit (%)", upper); Add(panel, "Recharge at (%)", lower); Add(panel, "Startup delay (s)", delay); Add(panel, "OEM driver DLL", dllPath);
            panel.Controls.Add(enabled, 1, 4);
            var actions = new FlowLayoutPanel { AutoSize = true };
            actions.Controls.Add(Button("Read EC", delegate { ReadEc(); }));
            actions.Controls.Add(Button("Apply now", delegate { ApplyNow(); }));
            actions.Controls.Add(Button("Save", delegate { Save(); }));
            actions.Controls.Add(Button("Install service", delegate { InstallService(); }));
            actions.Controls.Add(Button("Remove service", delegate { RemoveService(); }));
            panel.Controls.Add(actions, 1, 5); panel.Controls.Add(status, 1, 6); Controls.Add(panel); LoadConfig();
        }
        private void Add(TableLayoutPanel p, string name, Control c) { p.Controls.Add(new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left }, 0, nextRow); p.Controls.Add(c, 1, nextRow); nextRow++; }
        private static Button Button(string text, EventHandler click) { var b = new Button { Text = text, AutoSize = true }; b.Click += click; return b; }
        private BatteryConfig Current() { return new BatteryConfig { UpperLimit = (int)upper.Value, LowerLimit = (int)lower.Value, StartupDelaySeconds = (int)delay.Value, Enabled = enabled.Checked, OemDllPath = dllPath.Text }; }
        private void LoadConfig() { var c = BatteryConfig.Load(); upper.Value = c.UpperLimit; lower.Value = c.LowerLimit; delay.Value = c.StartupDelaySeconds; enabled.Checked = c.Enabled; dllPath.Text = c.OemDllPath; }
        private void Guard(Action action) { try { action(); } catch (Exception ex) { status.Text = "Error: " + ex.Message; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        private void ReadEc() { Guard(delegate { var r = ThresholdManager.Read(dllPath.Text); status.Text = string.Format("EC upper={0}, lower={1}. No values were written.", r.BeforeUpper, r.BeforeLower); }); }
        private void ApplyNow() { Guard(delegate { var c = Current(); c.Validate(); if (MessageBox.Show(this, string.Format("Write upper {0}% and lower {1}% to EC?", c.UpperLimit, c.LowerLimit), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; var r = ThresholdManager.Apply(c); status.Text = string.Format("Verified: upper={0}, lower={1}.", r.AfterUpper, r.AfterLower); }); }
        private void Save() { Guard(delegate { Current().Save(); status.Text = "Configuration saved."; }); }
        private string ServiceSourceExe() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MechrevoBatteryManager.Service.exe"); }
        private string ServiceExe() { return Path.Combine(ServiceInstallDirectory, "MechrevoBatteryManager.Service.exe"); }
        private string CoreSourceDll() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MechrevoBatteryManager.Core.dll"); }
        private void InstallService()
        {
            Guard(delegate
            {
                Current().Save();
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\MechrevoBatteryManager")) key.SetValue("UserProfile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), RegistryValueKind.String);
                if (!File.Exists(ServiceSourceExe()) || !File.Exists(CoreSourceDll())) throw new FileNotFoundException("The service executable and Core DLL must be next to this application.");
                Directory.CreateDirectory(ServiceInstallDirectory);
                File.Copy(ServiceSourceExe(), ServiceExe(), true);
                File.Copy(CoreSourceDll(), Path.Combine(ServiceInstallDirectory, "MechrevoBatteryManager.Core.dll"), true);
                var uninstallSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall-MechrevoBatteryManager.cmd");
                if (File.Exists(uninstallSource)) File.Copy(uninstallSource, Path.Combine(ServiceInstallDirectory, "Uninstall-MechrevoBatteryManager.cmd"), true);
                RunSc("stop MechrevoBatteryManager", true);
                RunSc("create MechrevoBatteryManager binPath= \"" + ServiceExe() + "\" start= delayed-auto DisplayName= \"Mechrevo Battery Manager\"");
                RunSc("description MechrevoBatteryManager \"Applies configured battery thresholds once after startup\"");
                RunSc("start MechrevoBatteryManager");
                status.Text = "Service installed at " + ServiceInstallDirectory;
            });
        }
        private void RemoveService() { Guard(delegate { RunSc("stop MechrevoBatteryManager", true); RunSc("delete MechrevoBatteryManager"); status.Text = "Service removed."; }); }
        private static void RunSc(string args, bool ignoreFailure = false) { var p = Process.Start(new ProcessStartInfo("sc.exe", args) { UseShellExecute = false, CreateNoWindow = true }); p.WaitForExit(); if (!ignoreFailure && p.ExitCode != 0) throw new InvalidOperationException("sc.exe failed with exit code " + p.ExitCode); }
    }
}
