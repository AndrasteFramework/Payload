using Andraste.Payload.Hooking;
using Andraste.Payload.Native;
using Andraste.Shared.Lifecycle;
using EasyHook;
using NLog;

namespace Andraste.Payload.Util
{
    public class DebuggerOutputLoggingManager : IManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Hook<Kernel32.DelegateOutputDebugString> _odsAHook;
        public Kernel32.DelegateOutputDebugString LoggingDelegate = DefaultLogger;
        public bool Swallow = false;
            
        public void Load()
        {
            _odsAHook = new Hook<Kernel32.DelegateOutputDebugString>(
                LocalHook.GetProcAddress("kernel32.dll", "OutputDebugStringA"), str =>
                {
                    LoggingDelegate.Invoke(str);
                    if (!Swallow)
                    {
                        _odsAHook.Original(str);
                    }
                }, this);
            _odsAHook.Activate();
        }

        public bool Enabled
        {
            get => _odsAHook.IsActive;
            set
            {
                if (value)
                {
                    _odsAHook.Activate();
                }
                else
                {
                    _odsAHook.Deactivate();
                }
            }
        }

        public bool Loaded => _odsAHook != null;
        public void Unload()
        {
            _odsAHook.Dispose();
            _odsAHook = null;
        }

        public static void DefaultLogger(string str)
        {
            Logger.Debug($"[DEBUGGER]: {str.TrimEnd()}");
        }
    }
}