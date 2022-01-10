#if NETFX
using System;
using System.Threading;
using Andraste.Payload.D3D9;
using Andraste.Payload.Hooking;
using Andraste.Shared.Lifecycle;
using DearImguiSharp;
using NLog;
using SharpDX.Direct3D9;

namespace Andraste.Payload.ImGuiIntegration
{
    /// <summary>
    /// The ImGuiManager is the implementation of the (to date) major GUI
    /// Framework. <br />It does the Integration work with the hook and is based
    /// on a custom (.NET FX 4.8) build of DearImguiSharp.<br />
    /// <br />
    /// The Integration is as simple as subscribing to <see cref="Render"/>
    /// and calling the Methods in <see cref="ImGui"/>.<br />
    /// Changing IO settings, specifically <see cref="ImGuiIO.MouseDrawCursor"/>
    /// is also allowed from within Render. 
    /// </summary>
    ///
    /// <remarks>Calling Load/Unload does NOT clear the Event List, because
    /// the Manager may be re-loaded based on Graphics Backend Events</remarks>
    public class ImGuiManager : IManager
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IntPtr _hWnd;
        private readonly D3D9HookManager _d3d9;
        private readonly WndProcManager _wndProcManager;
        private ImGuiContext _ctx;
        private ImGuiIO _io;

        // Unfortunately, we need to store delegates to remove them properly.
        // If we would only use the methods, a new delegate would be created each time
        private WndProcManager.WndProcDelegate _del;
        private D3D9HookEvents.EndSceneDelegate _endScene;
        private D3D9HookEvents.ResetDelegate _reset;

        #nullable enable
        /// <summary>
        /// Implement this to render your custom ImGui Elements
        /// </summary>
        public delegate void ImGuiRenderDelegate(ImGuiManager sender);
        public event ImGuiRenderDelegate? Render;
        #nullable restore

        public ImGuiManager(D3D9HookManager d3d9, IntPtr hWnd, WndProcManager wndProcManager)
        {
            _d3d9 = d3d9;
            _hWnd = hWnd;
            _wndProcManager = wndProcManager;
        }

        public bool Enabled { get; set; }

        public bool Loaded => _ctx != null;

        public void Load()
        {
            _ctx = ImGui.CreateContext(null);
            _io = ImGui.GetIO();
            _io.IniFilename = "";
            ImGui.StyleColorsDark(null);
            ImGui.ImGuiImplWin32Init(_hWnd);
            unsafe
            {
                ImGui.ImGuiImplDX9Init((void*) (IntPtr) _d3d9.Device);
            }

            _del = WndProcHandler;
            _endScene = D3d9OnEndScene;
            _reset = D3d9OnReset;

            _wndProcManager.Callbacks.Add(_del);
            _d3d9.EndScene += _endScene;
            _d3d9.Reset += _reset;
        }

        public void Unload()
        {
            _d3d9.Reset -= _reset;
            _d3d9.EndScene -= _endScene;
            _wndProcManager.Callbacks.Remove(_del);

            ImGui.ImGuiImplDX9InvalidateDeviceObjects(); // Probably redundant
            ImGui.ImGuiImplDX9Shutdown();
            ImGui.ImGuiImplWin32Shutdown();
            ImGui.DestroyContext(_ctx);
            _ctx = null;
        }

        private IntPtr WndProcHandler(IntPtr wnd, uint msg, ref IntPtr wParam, ref IntPtr lParam, out bool consume,
            out bool callbacks)
        {
            consume = false;
            callbacks = false;

            // Mouse events
            if (msg >= 0x0201 && msg <= 0x020D || msg == 0x0020 || msg == 0x219)
            {
                consume = _io.WantCaptureMouse;
            }

            // Keyboard events
            if (msg == 0x0100 || msg == 0x0101 || msg == 0x0102 || msg == 0x0104 || msg == 0x0105 || msg == 0x0008 ||
                msg == 0x219)
            {
                consume |= _io.WantCaptureKeyboard;
            }

            // Note: Consume does NOT work when the game polls the input or uses Direct X
            unsafe
            {
                return ImGui.ImplWin32_WndProcHandler((void*) wnd, msg, wParam, lParam);
            }
        }

        private void D3d9OnEndScene(Device device)
        {
            if (!Enabled)
            {
                return;
            }

            // This can be overriden by Render() if desired.
            _io.MouseDrawCursor = _io.WantCaptureMouse;
            ImGui.ImGuiImplDX9NewFrame();
            ImGui.ImGuiImplWin32NewFrame();
            ImGui.NewFrame();

            // https://codeblog.jonskeet.uk/2015/01/30/clean-event-handlers-invocation-with-c-6/
            Interlocked.CompareExchange(ref Render, null, null)?.Invoke(this);

            ImGui.EndFrame();
            ImGui.Render();
            ImGui.ImGuiImplDX9RenderDrawData(ImGui.GetDrawData());
        }

        private void D3d9OnReset(Device device, ref PresentParameters parameters)
        {
            ImGui.ImGuiImplDX9InvalidateDeviceObjects();
            // @TODO: PostReset for CreateDeviceObjects, however NewFrame() will do so automatically anyway.
        }
    }
}
#endif
