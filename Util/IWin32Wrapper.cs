#if NETFX
using System;
using System.Windows.Forms;

namespace Andraste.Payload.Util
{
    /// <summary>
    /// This class allows to create a dummy <see cref="IWin32Window"/> from
    /// a simple <see cref="IntPtr"/>.<br />
    /// This is most prominently used to show MessageBoxes on top of foreign
    /// applications from which we only have the hWnd (aka MainWindowHandle).
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class IWin32Wrapper : IWin32Window
    {
        public IWin32Wrapper(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; set; }
    }
}
#endif
