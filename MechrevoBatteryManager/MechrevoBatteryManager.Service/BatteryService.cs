using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;
using MechrevoBatteryManager;

namespace MechrevoBatteryManager.ServiceHost
{
    public sealed class BatteryService : ServiceBase
    {
        public const string Name = "MechrevoBatteryManager";
        public const string InstallDirectory = @"C:\Program Files\OEM\BatteryManager";
        private Thread worker;
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        public BatteryService() { ServiceName = Name; CanStop = true; AutoLog = false; }
        protected override void OnStart(string[] args) { worker = new Thread(Run) { IsBackground = true }; worker.Start(); }
        protected override void OnStop() { stop.Set(); if (worker != null) worker.Join(TimeSpan.FromSeconds(10)); }
        private void Run()
        {
            try
            {
                var cfg = LoadServiceConfig();
                if (!cfg.Enabled) { Log("Disabled; no EC write performed."); return; }
                if (stop.WaitOne(TimeSpan.FromSeconds(cfg.StartupDelaySeconds))) return;
                ApplyOnce();
            }
            finally
            {
                new Thread(new ThreadStart(delegate { try { Log("Service stopping automatically."); Stop(); } catch { } })) { IsBackground = true }.Start();
            }
        }
        internal static void ApplyOnce()
        {
            try
            {
                var cfg = LoadServiceConfig();
                if (!cfg.Enabled) { Log("Disabled; no EC write performed."); return; }
                var r = ThresholdManager.Apply(cfg);
                Log(string.Format("Applied upper={0}, lower={1}; before={2}/{3}, after={4}/{5}, changed={6}", cfg.UpperLimit, cfg.LowerLimit, r.BeforeUpper, r.BeforeLower, r.AfterUpper, r.AfterLower, r.Changed));
            }
            catch (Exception ex) { Log("ERROR: " + ex); }
        }
        private static BatteryConfig LoadServiceConfig()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"Software\MechrevoBatteryManager"))
            {
                var profile = key == null ? null : key.GetValue("UserProfile") as string;
                return string.IsNullOrWhiteSpace(profile) ? BatteryConfig.Load() : BatteryConfig.LoadFromDirectory(profile);
            }
        }
        private static void Log(string message)
        {
            var logDirectory = Path.Combine(InstallDirectory, "log");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(Path.Combine(logDirectory, "service.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
        }
    }
}
