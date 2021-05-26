#if !NETSTANDARD
using System;
using Andraste.Payload.D3D9;
using Andraste.Shared.Lifecycle;
using DearImguiSharp;

namespace Andraste.Payload.ImGuiIntegration
{
    public class ImGuiManager : IManager
    {
        private readonly D3D9HookManager _d3d9;
        private readonly IntPtr _hWnd;
        private ImGuiContext _ctx;

        public ImGuiManager(D3D9HookManager d3d9, IntPtr hWnd)
        {
            _d3d9 = d3d9;
            _hWnd = hWnd;
        }

        public bool Enabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool Loaded => _ctx != null;

        public void Load()
        {
            _ctx = ImGui.CreateContext(null);
            var io = ImGui.GetIO();
            ImGui.StyleColorsDark(null);
            ImGui.ImGuiImplWin32Init(_hWnd);

            unsafe
            {
                ImGui.ImGuiImplDX9Init((void*)(IntPtr)_d3d9.Device);
            }

            _d3d9.EndScene += device =>
            {
                //if (ImGui.IsKeyPressed((int)ImGuiKey.Space))
                if (true || io.KeysDown[0x79]) // F10
                {
                    ImGui.ImGuiImplDX9NewFrame();
                    ImGui.ImGuiImplWin32NewFrame();
                    ImGui.NewFrame();
                    //ImGui.ShowDemoWindow();
                    bool open = true;

                    ImGui.ShowAboutWindow(ref open);
                    ImGui.EndFrame();

                    ImGui.Render();
                    ImGui.ImGuiImplDX9RenderDrawData(ImGui.GetDrawData());
                }
            };
        }

        public void Unload()
        {
            ImGui.ImGuiImplDX9Shutdown();
            ImGui.ImGuiImplWin32Shutdown();
            ImGui.DestroyContext(_ctx);
            _ctx = null;
        }
    }
}
#endif
