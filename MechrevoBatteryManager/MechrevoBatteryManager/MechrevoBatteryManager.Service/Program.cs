using System;
using System.ServiceProcess;

namespace MechrevoBatteryManager.ServiceHost
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (Environment.UserInteractive && args.Length > 0 && args[0] == "--run-once") { BatteryService.ApplyOnce(); return; }
            ServiceBase.Run(new BatteryService());
        }
    }
}
