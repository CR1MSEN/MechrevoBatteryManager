using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MechrevoBatteryManager
{
    public interface IEcAccessor
    {
        byte Read(uint address);
        void Write(uint address, byte value);
    }
    public sealed class EcClient : IDisposable, IEcAccessor
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte ReadEcDelegate(uint address);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void WriteEcDelegate(uint address, uint value);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);

        private IntPtr module;
        private readonly ReadEcDelegate read;
        private readonly WriteEcDelegate write;

        public EcClient(string dllPath)
        {
            OemDriverInfo.Validate(dllPath);
            module = LoadLibrary(dllPath);
            if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to load OEM ACPIDriverDll.dll");
            try
            {
                read = LoadExport<ReadEcDelegate>("ReadEC");
                write = LoadExport<WriteEcDelegate>("WriteEC");
            }
            catch { Dispose(); throw; }
        }

        private T LoadExport<T>(string name) where T : class
        {
            var address = GetProcAddress(module, name);
            if (address == IntPtr.Zero) throw new MissingMethodException("OEM DLL export not found: " + name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }

        public byte Read(uint address) { ThrowIfDisposed(); return read(address); }
        public void Write(uint address, byte value)
        {
            ThrowIfDisposed();
            if (address != ThresholdManager.UpperAddress && address != ThresholdManager.ProfileAddress)
                throw new InvalidOperationException("EC write address is not supported: 0x" + address.ToString("X4"));
            write(address, value);
        }
        private void ThrowIfDisposed() { if (module == IntPtr.Zero) throw new ObjectDisposedException("EcClient"); }
        public void Dispose() { if (module != IntPtr.Zero) { FreeLibrary(module); module = IntPtr.Zero; } }
    }
}
