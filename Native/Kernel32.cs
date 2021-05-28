using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Andraste.Payload.Native
{
    public static class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern void DebugBreak();
    }
}
