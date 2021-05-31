using System;
using System.Runtime.InteropServices;

namespace Andraste.Payload.Native
{
    public static class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern void DebugBreak();
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr CreateFileA(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        public delegate IntPtr Delegate_CreateFileA(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    }
}
