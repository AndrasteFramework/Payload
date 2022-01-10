using System;
using System.Runtime.InteropServices;
using System.Threading;
using Andraste.Payload.Native;

namespace Andraste.Payload.Util
{
    /// <summary>
    /// A Utility Class to help debugging and writing against the framework.
    /// </summary>
    public static class DebugUtil
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PureCallHandlerDelegate();
        [DllImport("msvcr90.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_set_purecall_handler")]
        private static extern IntPtr _set_purecall_handler90([MarshalAs(UnmanagedType.FunctionPtr)] PureCallHandlerDelegate handler);

        /// <summary>
        /// Install a purecall handler for an <c>msvcr90.dll</c> application.
        /// When a call of a pure virtual function happens, the default
        /// behavior is only showing a small message box and then quitting.
        ///
        /// This Handler will emit a native breakpoint, so you can inspect
        /// the callstack of that purecall. One can also put a
        /// ".net breakpoint" on <see cref="PureCallHandler"/>
        /// </summary>
        public static void InstallPurecallHandler90()
        {
            _set_purecall_handler90(PureCallHandler);
        }

        private static void PureCallHandler()
        {
            // Place a Breakpoint here for .net Debugging
            Kernel32.DebugBreak(); // Causes a native breakpoint
        }

        /// <summary>
        /// Wait / block the current thread until a debugger is attached
        /// </summary>
        public static void DebugWait()
        {
            while (!Kernel32.IsDebuggerPresent())
            {
                Thread.Sleep(1000);
            }
        }
    }
}
