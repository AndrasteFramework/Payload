using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Andraste.Payload.Hooking;
using Andraste.Shared.Lifecycle;
using NLog;
using SharpDX.Direct3D9;

namespace Andraste.Payload.D3D9
{
    public class D3D9HookManager : IManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected Hook<Direct3D9Device_EndSceneDelegate> Direct3DDevice_EndSceneHook;
        protected Hook<Direct3D9Device_ResetDelegate> Direct3DDevice_ResetHook;
        protected Hook<Direct3D9Device_PresentDelegate> Direct3DDevice_PresentHook;
        protected Hook<Direct3D9DeviceEx_PresentExDelegate> Direct3DDeviceEx_PresentExHook;

        protected List<Hook> Hooks = new List<Hook>();
        public List<IntPtr> Id3dDeviceFunctionAddresses = new List<IntPtr>();
        //List<IntPtr> id3dDeviceExFunctionAddresses = new List<IntPtr>();
        bool _supportsDirect3D9Ex;
        public Device Device;

        public event D3D9HookEvents.EndSceneDelegate EndScene;
        public event D3D9HookEvents.ResetDelegate Reset;
        public event D3D9HookEvents.PresentDelegate Present;
        public event D3D9HookEvents.PresentExDelegate PresentEx;

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value)
                {
                    Hooks.ForEach(h => h.Activate());
                }
                else
                {
                    Hooks.ForEach(h => h.Deactivate());
                }
            }
        }

        public bool Loaded => Hooks.Count > 0;

        public void Load()
        {
            #if NETFX
            // First we need to determine the function address for IDirect3DDevice9
            Id3dDeviceFunctionAddresses = new List<IntPtr>();
            //id3dDeviceExFunctionAddresses = new List<IntPtr>();
            logger.Debug("D3D9Hook: Before device creation");
            using (Direct3D d3d = new Direct3D())
            {
                using (var renderForm = new System.Windows.Forms.Form())
                {
                    using (var device = new Device(d3d, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing,
                        new PresentParameters { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle }))
                    {
                        logger.Debug("D3D9Hook: Device created");
                        Id3dDeviceFunctionAddresses.AddRange(Functions.GetVTblAddresses(device.NativePointer, Functions.D3D9_DEVICE_METHOD_COUNT));
                    }
                }
            }

            try
            {
                using (var d3dEx = new Direct3DEx())
                {
                    logger.Debug("D3D9Hook: Direct3DEx...");
                    using (var renderForm = new System.Windows.Forms.Form())
                    {
                        using (var deviceEx = new DeviceEx(d3dEx, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing,
                            new PresentParameters { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle },
                            new DisplayModeEx { Width = 800, Height = 600 }))
                        {
                            logger.Debug("D3D9Hook: DeviceEx created - PresentEx supported");
                            Id3dDeviceFunctionAddresses.AddRange(Functions.GetVTblAddresses(deviceEx.NativePointer, 
                                Functions.D3D9_DEVICE_METHOD_COUNT, Functions.D3D9Ex_DEVICE_METHOD_COUNT));
                            _supportsDirect3D9Ex = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                _supportsDirect3D9Ex = false;
            }

            // We want to hook each method of the IDirect3DDevice9 interface that we are interested in
            // 42 - EndScene (we will retrieve the back buffer here)
            Direct3DDevice_EndSceneHook = new Hook<Direct3D9Device_EndSceneDelegate>(
                Id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.EndScene],
                // On Windows 7 64-bit w/ 32-bit app and d3d9 dll version 6.1.7600.16385, the address is equiv to:
                // (IntPtr)(GetModuleHandle("d3d9").ToInt32() + 0x1ce09),
                // A 64-bit app would use 0xff18
                // Note: GetD3D9DeviceFunctionAddress will output these addresses to a log file
                EndSceneHook, this);

            unsafe
            {
                // If Direct3D9Ex is available - hook the PresentEx
                if (_supportsDirect3D9Ex)
                {
                    Direct3DDeviceEx_PresentExHook = new Hook<Direct3D9DeviceEx_PresentExDelegate>(
                        Id3dDeviceFunctionAddresses[(int)Direct3DDevice9ExFunctionOrdinals.PresentEx],
                        PresentExHook, this);
                }

                // Always hook Present also (device will only call Present or PresentEx not both)
                Direct3DDevice_PresentHook = new Hook<Direct3D9Device_PresentDelegate>(
                    Id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Present],
                    PresentHook, this);
            }

            // 16 - Reset (called on resolution change or windowed/fullscreen change - we will reset some things as well)
            Direct3DDevice_ResetHook = new Hook<Direct3D9Device_ResetDelegate>(
                Id3dDeviceFunctionAddresses[(int)Direct3DDevice9FunctionOrdinals.Reset],
                // On Windows 7 64-bit w/ 32-bit app and d3d9 dll version 6.1.7600.16385, the address is equiv to:
                //(IntPtr)(GetModuleHandle("d3d9").ToInt32() + 0x58dda),
                // A 64-bit app would use 0x3b3a0
                // Note: GetD3D9DeviceFunctionAddress will output these addresses to a log file
                ResetHook, this);

            Hooks.Add(Direct3DDevice_EndSceneHook);
            Hooks.Add(Direct3DDevice_PresentHook);
            if (_supportsDirect3D9Ex)
            {
                Hooks.Add(Direct3DDeviceEx_PresentExHook);
            }
            Hooks.Add(Direct3DDevice_ResetHook);
            #endif
        }

        public void Unload()
        {
            Hooks.ForEach(h => h.Dispose());
            Hooks.Clear();
        }

        private unsafe int PresentExHook(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect,
            IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags)
        {
            logger.Trace("PresentEx called");

            try
            {
                // More expensive copying than just passing pointers and expecting unsafe code!
                Rectangle? sourceRect = null;
                Rectangle? destRect = null;

                if (pSourceRect != null)
                {
                    sourceRect = *pSourceRect;
                }

                if (pDestRect != null)
                {
                    destRect = *pDestRect;
                }

                // https://codeblog.jonskeet.uk/2015/01/30/clean-event-handlers-invocation-with-c-6/
                Interlocked.CompareExchange(ref PresentEx, null, null)?.Invoke((Device)devicePtr,
                    ref sourceRect, ref destRect, hDestWindowOverride, pDirtyRegion, dwFlags);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Exception caught in PresentExHook Event Handler");
            }

            Device ??= (Device)devicePtr;

            return Direct3DDeviceEx_PresentExHook.Original(devicePtr, pSourceRect, pDestRect, 
                hDestWindowOverride, pDirtyRegion, dwFlags);
        }

        private unsafe int PresentHook(IntPtr devicePtr, Rectangle* pSourceRect, Rectangle* pDestRect,
            IntPtr hDestWindowOverride, IntPtr pDirtyRegion)
        {
            // Mumble uses StateBlocks. ((Device)devicePtr).BeginStateBlock();
            logger.Trace("Present called");

            try
            {
                // More expensive copying than just passing pointers and expecting unsafe code!
                Rectangle? sourceRect = null;
                Rectangle? destRect = null;

                if (pSourceRect != null)
                {
                    sourceRect = *pSourceRect;
                }

                if (pDestRect != null)
                {
                    destRect = *pDestRect;
                }

                // https://codeblog.jonskeet.uk/2015/01/30/clean-event-handlers-invocation-with-c-6/
                Interlocked.CompareExchange(ref Present, null, null)?.Invoke((Device)devicePtr,
                    ref sourceRect, ref destRect, hDestWindowOverride, pDirtyRegion);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Exception caught in PresentHook Event Handler");
            }

            Device ??= (Device) devicePtr;

            return Direct3DDevice_PresentHook.Original(devicePtr, pSourceRect, pDestRect,
                hDestWindowOverride, pDirtyRegion);
        }

        private int ResetHook(IntPtr devicePtr, ref PresentParameters presentParameters)
        {
            logger.Trace("Reset called");
            try
            {
                // https://codeblog.jonskeet.uk/2015/01/30/clean-event-handlers-invocation-with-c-6/
                Interlocked.CompareExchange(ref Reset, null, null)?.Invoke((Device)devicePtr, ref presentParameters);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Exception caught in ResetHook Event Handler");
            }

            Device ??= (Device)devicePtr;

            return Direct3DDevice_ResetHook.Original(devicePtr, ref presentParameters);
        }


        private int EndSceneHook(IntPtr devicePtr)
        {
            logger.Trace("EndScene called");
            try
            {
                // https://codeblog.jonskeet.uk/2015/01/30/clean-event-handlers-invocation-with-c-6/
                Interlocked.CompareExchange(ref EndScene, null, null)?.Invoke((Device)devicePtr);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Exception caught in EndScene Event Handler");
            }

            Device ??= (Device)devicePtr;

            return Direct3DDevice_EndSceneHook.Original(devicePtr);
        }
    }
}
