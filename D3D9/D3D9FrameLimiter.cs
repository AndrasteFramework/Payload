using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
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
        /// The time since the last Present()/PresentEx()/EndScene() call
        /// </summary>
        private readonly Stopwatch _stopwatch;

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);
        
        public D3D9FrameLimiter(D3D9HookManager d3d9)
        {
            _stopwatch = new Stopwatch();
            _d3d9 = d3d9;
        }

        // ReSharper disable once InconsistentNaming
        public D3D9FrameLimiter(D3D9HookManager d3d9, float targetFPS) : this(d3d9)
        {
            TargetFPS = targetFPS;
        }

        public void Load()
        {
            // TODO: Query min timer resolution which may not be 1
            timeBeginPeriod(1);
            _stopwatch.Start();
            // TODO: Support different callbacks: Present, PresentEx and EndScene [actually EndScene is probably not right]
            _d3d9.Present += D3d9OnPresent;
            Loaded = true;
        }

        public void Unload()
        {
            _d3d9.Present -= D3d9OnPresent;
            timeEndPeriod(1);
            Loaded = false;
        }

        private void D3d9OnPresent(Device deviceptr, ref Rectangle? sourcerect, ref Rectangle? destrect, IntPtr hdestwindowoverride, IntPtr pdirtyregion)
        {
            if (!Enabled)
            {
                return;
            }

            var sw = new SpinWait();
            while (_stopwatch.Elapsed.TotalMilliseconds < MsPerFrame)
            {
                sw.SpinOnce();
            }
            
            _stopwatch.Restart();
        }
    }
}
