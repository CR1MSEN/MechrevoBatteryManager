using System;
using System.Threading;

namespace MechrevoBatteryManager
{
    // Coordinate our elevated GUI and startup service across Windows sessions.
    // This does not lock the OEM console or the firmware.
    internal sealed class EcOperationLock : IDisposable
    {
        private readonly Mutex mutex;
        private bool acquired;
        internal EcOperationLock(int timeoutMilliseconds = 10000, string name = @"Global\MechrevoBatteryManager.EcOperation")
        {
            mutex = new Mutex(false, name);
            try
            {
                try { acquired = mutex.WaitOne(timeoutMilliseconds); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new InvalidOperationException("Another Battery Manager EC operation is in progress. Try again later.");
            }
            catch { mutex.Dispose(); throw; }
        }
        public void Dispose()
        {
            if (acquired) { acquired = false; mutex.ReleaseMutex(); }
            mutex.Dispose();
        }
    }
}
