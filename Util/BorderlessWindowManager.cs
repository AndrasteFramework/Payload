using System;
using System.Runtime.InteropServices;
using Andraste.Payload.Hooking;
using Andraste.Payload.Native;
using Andraste.Shared.Lifecycle;
using NLog;
using static Andraste.Payload.Util.Native;

namespace Andraste.Payload.Util
{
    #region P/Invokes
    // Those are NOT part of Andraste.Payload.Native, because they are to specific to be of general purpose use.
    internal static class Native
    {
        public static int GWL_STYLE = -16;
        public static int GWL_EXSTYLE = -20;

        public static uint WS_BORDER = 0x00800000;
        public static uint WS_DLGFRAME = 0x00400000;
        public static uint WS_CAPTION = WS_BORDER | WS_DLGFRAME;
        public static uint WS_THICKFRAME = 0x00040000;
        public static uint WS_SYSMENU = 0x00080000;
        public static uint WS_MAXIMIZEBOX = 0x00010000;
        public static uint WS_MINIMIZEBOX = 0x00020000;
        
        public static uint WS_EX_DLGMODALFRAME = 0x00000001;
        public static uint WS_EX_COMPOSITED = 0x02000000;
        public static uint WS_EX_WINDOWEDGE = 0x00000100;
        public static uint WS_EX_CLIENTEDGE = 0x00000200;
        public static uint WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE;
        public static uint WS_EX_LAYERED = 0x00080000;
        public static uint WS_EX_STATICEDGE = 0x00020000;
        public static uint WS_EX_TOOLWINDOW = 0x00000080;
        public static uint WS_EX_APPWINDOW = 0x00040000; // Forces the Taskbar.

        public static int WM_STYLECHANGING = 0x007C;

        [StructLayout(LayoutKind.Sequential)]
        public struct STYLESTRUCT
        {
            public uint styleOld;
            public uint styleNew;
        };

    }
    #endregion

    /// <summary>
    /// A simple implementation of a borderless mode enforcer.
    /// There are lots of other things possible (e.g. hiding menus etc),
    /// but since we primarily target games, this is not a concern
    /// </summary>
    public class BorderlessWindowManager : IManager
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IntPtr _windowHandle;
        private readonly WndProcManager _wndProcManager;
        private WndProcManager.WndProcDelegate _wndProcDel;
        private IntPtr _windowStyle, _windowStyleExtended;

        private bool _enabled;
        public bool Enabled { get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                if (value)
                {
                    MakeBorderless();
                }
                else
                {
                    Restore();
                }

                _enabled = value;
            }
        }
        
        public bool Loaded { get; private set; }

        /// <summary>
        /// Creates a new Borderless Window Manager.<br />
        /// </summary>
        /// <param name="windowHandle">The Handle to the Window</param>
        /// <param name="wndProcManager">Activate a persistence mode, by passing
        /// the <see cref="WndProcManager"/>, which will re-apply styles when
        /// the application tries to change them.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="windowHandle"/> is null</exception>
        public BorderlessWindowManager(IntPtr windowHandle, WndProcManager wndProcManager)
        {
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(windowHandle));
            }

            _windowHandle = windowHandle;
            _wndProcManager = wndProcManager;
        }

        /// <inheritdoc cref="BorderlessWindowManager(IntPtr, WndProcManager)"/>
        public BorderlessWindowManager(IntPtr windowHandle) : this(windowHandle, null)
        {
        }

        public void Load()
        {
            if (Loaded)
            {
                return;
            }

            // Store the method reference in the delegate, so we can remove it later again
            _wndProcDel = WndProc;
            _wndProcManager?.Callbacks.Add(_wndProcDel);
            Loaded = true;
        }

        public void Unload()
        {
            if (!Loaded)
            {
                return;
            }

            _wndProcManager?.Callbacks.Remove(_wndProcDel);
            Loaded = false;
        }

        /// <summary>
        /// This will make the Window appear Borderless while saving the original style.
        /// This allows for <see cref="Restore"/> to make it Windowed again.<br/>
        /// Note that <see cref="Enabled"/> already calls
        /// <see cref="MakeBorderless"/> and <see cref="Restore"/>.<br />
        /// Won't do anything if the window already is borderless
        /// </summary>
        public void MakeBorderless()
        {
            var oldWS = User32.GetWindowLongPtr(_windowHandle, GWL_STYLE);
            var oldWSExtended = User32.GetWindowLongPtr(_windowHandle, GWL_EXSTYLE);

            if (oldWS == BorderlessStyle(oldWS) && oldWSExtended == BorderlessStyleExtended(oldWSExtended))
            {
                // Prevent overwriting _windowStyle/Extended with borderless values
                // When this code is reached, the window already is borderless.
                return;
            }

            // Store the original styles
            _windowStyle = oldWS;
            _windowStyleExtended = oldWSExtended;

            // Remove the styles with a negative bitmask
            var styleNew = BorderlessStyle(_windowStyle);
            var styleNewExtended = BorderlessStyleExtended(_windowStyleExtended);

            User32.SetWindowLongPtr(_windowHandle, GWL_STYLE, styleNew);
            User32.SetWindowLongPtr(_windowHandle, GWL_EXSTYLE, styleNewExtended);
        }

        /// <summary>
        /// Restore the Window Styles into a Windowed mode.<br />
        /// Note that <see cref="Enabled"/> already calls
        /// <see cref="MakeBorderless"/> and <see cref="Restore"/>.<br />
        /// </summary>
        public void Restore()
        {
            User32.SetWindowLongPtr(_windowHandle, GWL_STYLE, _windowStyle);
            User32.SetWindowLongPtr(_windowHandle, GWL_EXSTYLE, _windowStyleExtended);
        }

        private static uint BorderlessStyle(uint ws)
        {
            return ws &
                   ~(
                       WS_CAPTION
                       | WS_THICKFRAME
                       | WS_SYSMENU
                       | WS_MINIMIZEBOX
                       | WS_MAXIMIZEBOX
                   );
        }

        private static IntPtr BorderlessStyle(IntPtr ptr)
        {
            return new IntPtr(BorderlessStyle((uint) ptr.ToInt64()));
        }

        private static uint BorderlessStyleExtended(uint ws)
        {
            return ws &
                   ~(
                       WS_EX_DLGMODALFRAME
                       | WS_EX_COMPOSITED
                       | WS_EX_OVERLAPPEDWINDOW
                       | WS_EX_LAYERED
                       | WS_EX_STATICEDGE
                       | WS_EX_TOOLWINDOW
                       | WS_EX_APPWINDOW
                   );
        }

        private static IntPtr BorderlessStyleExtended(IntPtr ptr)
        {
            return new IntPtr(BorderlessStyleExtended((uint)ptr.ToInt64()));
        }

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, ref IntPtr wParam, ref IntPtr lParam, out bool consume, out bool skipCallbacks)
        {
            skipCallbacks = false;
            consume = false;

            if (Enabled && hWnd == _windowHandle && uMsg == WM_STYLECHANGING)
            {
                var ss = Marshal.PtrToStructure<STYLESTRUCT>(lParam);
                if (wParam.ToInt32() == GWL_STYLE)
                {
                    if (BorderlessStyle(ss.styleNew) != ss.styleNew)
                    {
                        _logger.Info("Got a Style Change attempt. Re-Applying Borderless Modes");
                        ss.styleNew = BorderlessStyle(ss.styleNew);
                        Marshal.StructureToPtr(ss, lParam, false);
                        // Don't give the game a chance to override this.
                        consume = true;
                    }
                }
                else if (wParam.ToInt32() == GWL_EXSTYLE)
                {
                    if (BorderlessStyleExtended(ss.styleNew) != ss.styleNew)
                    {
                        _logger.Info("Got a StyleExtended Change attempt. Re-Applying Borderless Modes");
                        ss.styleNew = BorderlessStyleExtended(ss.styleNew);
                        Marshal.StructureToPtr(ss, lParam, false);
                        // Don't give the game a chance to override this.
                        consume = true; 
                    }
                }
            }

            return IntPtr.Zero;
        }
    }
}
