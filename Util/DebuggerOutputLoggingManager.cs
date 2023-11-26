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
        private Hook<Kernel32.DelegateOutputDebugStringW> _odsWHook;
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
            
            _odsWHook = new Hook<Kernel32.DelegateOutputDebugStringW>(
                LocalHook.GetProcAddress("kernel32.dll", "OutputDebugStringW"), str =>
                {
                    LoggingDelegate.Invoke(str);
                    if (!Swallow)
                    {
                        _odsWHook.Original(str);
                    }
                }, this);
            _odsWHook.Activate();
        }

        public bool Enabled
        {
            get => _odsAHook.IsActive;
            set
            {
                if (value)
                {
                    _odsAHook.Activate();
                    _odsWHook.Activate();
                }
                else
                {
                    _odsAHook.Deactivate();
                    _odsWHook.Deactivate();
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