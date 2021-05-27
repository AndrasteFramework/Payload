﻿using System;
using System.Drawing;
using Andraste.Shared.Lifecycle;
using SharpDX.Direct3D9;

namespace Andraste.Payload.D3D9
{
    /// <summary>
    /// Some games do not limit their FPS when running in Windowed Mode.
    /// This may be annoying when developing, but it may also be undesired
    /// for players to run at ridiculously high framerates, essentially
    /// maxing out the CPU Core of the Render Thread.
    /// 
    /// </summary>
    public class D3D9FrameLimiter : IManager
    {
        private readonly D3D9HookManager _d3d9;
        public bool Enabled { get; set; }
        public bool Loaded { get; private set; }

        // ReSharper disable once InconsistentNaming
        public float TargetFPS { get; set; } = 60f;

        private float MsPerFrame => 1000f / TargetFPS;
        
        /// <summary>
        /// The timestamp since the last Present()/PresentEx()/EndScene() call
        /// </summary>
        DateTime _lastFrame;

        public D3D9FrameLimiter(D3D9HookManager d3d9)
        {
            _d3d9 = d3d9;
        }

        // ReSharper disable once InconsistentNaming
        public D3D9FrameLimiter(D3D9HookManager d3d9, float targetFPS)
        {
            _d3d9 = d3d9;
            TargetFPS = targetFPS;
        }

        public void Load()
        {
            _lastFrame = DateTime.Now;
            Loaded = true;

            // TODO: Support different callbacks: Present, PresentEx and EndScene
            _d3d9.Present += D3d9OnPresent;
        }

        public void Unload()
        {
            _d3d9.Present -= D3d9OnPresent;
            Loaded = false;
        }

        private void D3d9OnPresent(Device deviceptr, ref Rectangle? sourcerect, ref Rectangle? destrect, IntPtr hdestwindowoverride, IntPtr pdirtyregion)
        {
            if (!Enabled)
            {
                return;
            }

            var ms = (DateTime.Now - _lastFrame).TotalMilliseconds;
            if (ms < MsPerFrame)
            {
                System.Threading.Thread.Sleep((int)(MsPerFrame - ms));
            }
            _lastFrame = DateTime.Now;
        }
    }
}