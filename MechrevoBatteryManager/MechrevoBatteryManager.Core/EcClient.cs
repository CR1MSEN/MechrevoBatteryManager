using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MechrevoBatteryManager
{
    public sealed class EcClient : IDisposable
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
            module = LoadLibrary(dllPath);
            if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to load OEM ACPIDriverDll.dll");
            read = LoadExport<ReadEcDelegate>("ReadEC");
            write = LoadExport<WriteEcDelegate>("WriteEC");
        }

        private T LoadExport<T>(string name) where T : class
        {
            var address = GetProcAddress(module, name);
            if (address == IntPtr.Zero) throw new MissingMethodException("OEM DLL export not found: " + name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }

        public byte Read(uint address) { return read(address); }
        public void Write(uint address, byte value) { write(address, value); }
        public void Dispose() { if (module != IntPtr.Zero) { FreeLibrary(module); module = IntPtr.Zero; } }
    }
}
