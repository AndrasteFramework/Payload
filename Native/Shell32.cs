using System;
using System.Runtime.InteropServices;

namespace Andraste.Payload.Native
{
    public static class Shell32
    {
        [DllImport("shell32.dll", CharSet = CharSet.Ansi)]
        public static extern int SHFileOperationA(IntPtr lpFileOp);
        public delegate int Delegate_SHFileOperationA(IntPtr lpFileOp);
    }
}
