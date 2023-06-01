using System;
using System.Runtime.InteropServices;

namespace Andraste.Payload.Native
{
    public static class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern void DebugBreak();

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern void OutputDebugStringA(string str);
        public delegate void DelegateOutputDebugString(string str);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern void OutputDebugStringW(string str);
        
        [DllImport("kernel32.dll")]
        public static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateFileA(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        public delegate IntPtr Delegate_CreateFileA(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern bool CreateDirectoryA(string lpFileName, IntPtr lpSecurityAttributes);

        public delegate bool Delegate_CreateDirectoryA(string lpFileName, IntPtr lpSecurityAttribute);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        public delegate IntPtr DelegateFindFirstFileA(string lpFileName, IntPtr lpFindFileData);
    }
}
