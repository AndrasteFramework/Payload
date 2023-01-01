using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Andraste.Payload.Hooking;
using Andraste.Payload.Native;
using Andraste.Shared.Lifecycle;
using EasyHook;
using NLog;

namespace Andraste.Payload.Net
{
    [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.ModFrameworkInternalAPI, 
        Reasoning = "Intended use are frameworks to redirect to multiplayer servers")]
    public class DNSRedirectionManager: IManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, string> _prefixRedirects = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _suffixRedirects = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _exactMatchRedirects = new Dictionary<string, string>();
        #nullable enable
        private readonly Dictionary<Regex, Func<string, string>> _regexRedirects =
            new Dictionary<Regex, Func<string, string>>();
        #nullable restore

        public bool Enabled
        {
            get => _hook.IsActive;
            set
            {
                if (value)
                {
                    _hook.Activate();
                }
                else
                {
                    _hook.Deactivate();
                }
            }
        }
        public bool Loaded => _hook != null;
        private Hook<WS2_32.Delegate_gethostbyname> _hook;

        private IntPtr HookFn(string name)
        {
            #nullable enable
            Logger.Trace($"Resolving {name}");
            if (_exactMatchRedirects.ContainsKey(name))
            {
                var target = _exactMatchRedirects[name];
                Logger.Trace($"Exact-Match Redirecting to {target}");
                return _hook.Original(target);
            }

            var regex = _regexRedirects.Keys.FirstOrDefault(x => x.IsMatch(name));
            if (regex != null)
            {
                var target = _regexRedirects[regex].Invoke(name);
                Logger.Trace($"Regex-Match Redirecting to {target}");
                return _hook.Original(target);
            }

            string? redirect = _suffixRedirects.Keys.FirstOrDefault(x => name.EndsWith(x, StringComparison.InvariantCultureIgnoreCase));
            if (redirect != null)
            {
                var target = _suffixRedirects[redirect];
                Logger.Trace($"Suffix-Match Redirecting to {target}");
                return _hook.Original(target);
            }
            
            redirect = _prefixRedirects.Keys.FirstOrDefault(x => name.EndsWith(x, StringComparison.InvariantCultureIgnoreCase));
            if (redirect != null)
            {
                var target = _prefixRedirects[redirect];
                Logger.Trace($"Prefix-Match Redirecting to {target}");
                return _hook.Original(target);
            }
            
            return _hook.Original(name);
            #nullable restore
        }

        public void Load()
        {
            _hook = new Hook<WS2_32.Delegate_gethostbyname>(LocalHook.GetProcAddress("ws2_32.dll", "gethostbyname"), HookFn, this);
        }

        public void Unload()
        {
            _hook.Dispose();
            _hook = null;
        }
        
        public void AddExactRedirect(string source, string target)
        {
            _exactMatchRedirects.Add(source, target);
        }

        public void AddRegexRedirect(Regex source, Func<string, string> targetFunc)
        {
            _regexRedirects.Add(source, targetFunc);
        }

        public void AddSuffixRedirect(string source, string target)
        {
            _suffixRedirects.Add(source, target);
        }

        public void AddPrefixRedirect(string source, string target)
        {
            _prefixRedirects.Add(source, target);
        }
    }
}