// Block Load() and Unload() until the WndProc has caused a successful Subclass (Un)Registration
#define BLOCK_LOAD

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Andraste.Payload.Native;
using Andraste.Shared.Lifecycle;
using NLog;

namespace Andraste.Payload.Hooking
{
    /// <summary>
    /// This class allows consumers to hook/manipulate the WndProc in a safe
    /// manner without needing to manually care about hooking it and the
    /// trouble involved with it (i.e. proper cleanup).<br />
    ///
    /// The order of insertion determines the order of execution.
    /// </summary>
    public class WndProcManager : IManager
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public IntPtr WindowHandle { get; }
        public bool Enabled { get; set; }
        public bool Loaded { get; private set; }

        public List<WndProcDelegate> Callbacks { get; }

        private IntPtr _hhk;
        private bool _subclassed;
        private ComCtl32.SubclassProc _del;

        /// <summary>
        /// This delegate is invoked each time a message is passed to the target application.<br />
        /// The first 4 parameters match the parameters from the WinAPI documentation for
        /// <c>WndProcs</c> or the relevant <c>WM_</c> messages.<br />
        ///
        /// With the way this delegate works we cannot have a custom control flow, so the
        /// two out boolean values control how the actual message is processed.
        /// </summary>
        /// <param name="hWnd">The handle of the hooked Window, see WinAPI</param>
        /// <param name="uMsg">The Window Message Id, see WinAPI</param>
        /// <param name="wParam">The wParam, see WinAPI</param>
        /// <param name="lParam">The lParam, see WinAPI</param>
        /// <param name="consume">Whether this delegate should <i>consume</i>
        /// the message.<br />
        /// This means it will NOT be passed to neither other
        /// hooking applications, other delegates of this manager nor the
        /// actual window/target application.<br />
        /// This can intercept keystrokes, but others (e.g. <c>WM_SETTEXT</c>)
        /// must not be intercepted, because the message never makes it's way
        /// to the actual window.</param>
        /// <param name="skipCallbacks">Whether this delegate should <i>consume</i>
        /// the message only for the other callbacks/delegates of this manager.<br />
        /// This is comparable to <paramref name="consume"/>, but only affects 
        /// other delegates.</param>
        /// <returns>The return value. Only relevant for <paramref name="consume"/> = <c>true</c></returns>
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, ref IntPtr wParam, ref IntPtr lParam,
            out bool consume, out bool skipCallbacks);

        public WndProcManager(IntPtr windowHandle)
        {
            WindowHandle = windowHandle;
            Callbacks = new List<WndProcDelegate>();
        }
        private void HookWndProc()
        {
            _logger.Trace("Hooking the WndProc");
            var tid = User32.GetWindowThreadProcessId(WindowHandle, IntPtr.Zero);
            var ret = _hhk = User32.SetWindowsHookEx(User32.HookType.WH_CALLWNDPROC, WndProc, IntPtr.Zero, tid);

            if (ret == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            #if BLOCK_LOAD
                _logger.Trace("Blocking until the Hook has been removed");
                while (_hhk != IntPtr.Zero)
                {
                    Thread.Sleep(10);
                }
                _logger.Trace("Blocking done");
            #endif
        }

        public void Load()
        {
            if (Loaded)
            {
                return;
            }

            /* In order to hook the WndProc, there are three possibilities:
               A) Use SetWindowLongPtr() to replace the WndProc. This is the 
                  legacy way and leads to problems when having multiple hooks.
               B) Use SetWindowSubclass, which is probably the fastest because it's 
                  thread local. The following quote remains a mystery though:
                  https://docs.microsoft.com/en-us/windows/win32/winmsg/about-hooks
                  "Subclassing the window does not work for messages set between processes."
                  I could confirm, that SetWindowSubclass indeed is triggered for 
                  remote SendMessage calls. It seems to be related about how Subclassing
                  itself doesn't work across processes, which is where C comes into play.
               C) Use SetWindowsHookEx, which is designed for external applications
                  hooking by providing an automated DLL Injection, so it may not be 
                  as fast as B. Furthermore it is not capable of editing message content.
                  Since B can only be called from the Message / Main Thread, we need C first.
             */

            HookWndProc();
            Loaded = true;
        }

        public void Unload()
        {
            if (!Loaded)
            {
                return;
            }

            HookWndProc();
            Loaded = false;
        }

        private IntPtr WndProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // TODO: What happens if there are multiple Messages in the queue, so that the Subclass is added and remove simultaneously
            // Check the spec if unhooking flushes the queue or prevent this proc from being called.
            if (nCode == ComCtl32.HC_ACTION)
            {
                if (!_subclassed)
                {
                    _logger.Trace("Subclassing the Window");
                    // Now we are on the Message Thread (tid), so we can register a Subclass
                    _del = PfnSubclass;
                    if (!ComCtl32.SetWindowSubclass(WindowHandle, _del, IntPtr.Zero, IntPtr.Zero))
                    {
                        throw new InvalidOperationException("Error when calling SetWindowSubclass");
                    }

                    _subclassed = true;
                } else if (_subclassed)
                {
                    _logger.Trace("Removing the Window Subclass");
                    if (!ComCtl32.RemoveWindowSubclass(WindowHandle, _del, IntPtr.Zero))
                    {
                        throw new InvalidOperationException("Error when calling RemoveWindowSubclass");
                    }

                    _subclassed = false;
                }

                _logger.Trace("Removing the Window Hook");
                if (!User32.UnhookWindowsHookEx(_hhk))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                _logger.Trace("Removed the Window Hook");
                _hhk = IntPtr.Zero;
            }

            return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr PfnSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefdata)
        {
            var _wParam = wParam;
            var _lParam = lParam;

            if (Enabled)
            {
                foreach (var del in Callbacks)
                {
                    var res = del(hWnd, uMsg, ref _wParam, ref _lParam, out var consume, out var skipCallbacks);

                    if (consume)
                    {
                        return res;
                    }

                    if (skipCallbacks)
                    {
                        break;
                    }
                }
            }

            return ComCtl32.DefSubclassProc(hWnd, uMsg, _wParam, _lParam);
        }
    }
}
