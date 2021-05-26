using System;
using System.Drawing;
using SharpDX.Direct3D9;

namespace Andraste.Payload.D3D9
{
    #nullable enable
    public class D3D9HookEvents
    {
        /// <summary>
        /// The IDirect3DDevice9.EndScene function definition
        /// </summary>
        /// <param name="device">The DirectX 9 Device</param>
        public delegate void EndSceneDelegate(Device device);

        /// <summary>
        /// The IDirect3DDevice9.Reset function definition
        /// </summary>
        /// <param name="device">The Direct X 9 Device</param>
        /// <param name="presentParameters"></param>
        public delegate void ResetDelegate(Device device, ref PresentParameters presentParameters);

        public delegate void PresentDelegate(Device devicePtr, ref Rectangle? sourceRect,
            ref Rectangle? destRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion);

        public delegate void PresentExDelegate(Device devicePtr, ref Rectangle? sourceRect,
            ref Rectangle? destRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags);

    }
    #nullable restore
}
