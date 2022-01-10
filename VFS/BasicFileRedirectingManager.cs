using System.Collections.Concurrent;
using Andraste.Payload.Hooking;
using Andraste.Payload.Native;
using Andraste.Shared.Lifecycle;
using EasyHook;
using NLog;

namespace Andraste.Payload.VFS
{
    /// <summary>
    /// The Basic File Redirecting Manager is the most basic element of Andraste's
    /// Virtual File System (VFS): It allows modding frameworks to redirect/detour
    /// FileOpen() calls from the game in order to point to a different file.<br />
    /// That way, when the game tries to open foo.txt, it will open bar.txt in a
    /// completely different folder (typically the one from a mod).
    /// 
    /// </summary>
    [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.ModFrameworkInternalAPI)]
    public class BasicFileRedirectingManager : IManager
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger(); 
        // TODO: What about "OpenFile" for older applications? What about CreateFileW?
        private Hook<Kernel32.Delegate_CreateFileA> _createFileHook;
        private readonly ConcurrentDictionary<string, string> _fileMap;

        public BasicFileRedirectingManager()
        {
            _fileMap = new ConcurrentDictionary<string, string>();
        }

        public bool Enabled
        {
            get => _createFileHook.IsActive;
            set
            {
                if (value)
                {
                    _createFileHook.Activate();
                }
                else
                {
                    _createFileHook.Deactivate();
                }
            }
        }

        public bool Loaded => _createFileHook != null;

        public void Load()
        {
            // TODO: be more clever about the name: Forward/Backward Slashes, "\\?\", Streams, etc. Maybe collect real world use-cases first?
            _createFileHook = new Hook<Kernel32.Delegate_CreateFileA>(
                LocalHook.GetProcAddress("kernel32.dll", "CreateFileA"),
                (name, access, mode, attributes, disposition, andAttributes, file) =>
                {
                    //LogManager.GetCurrentClassLogger().Warn($"CreateFileA {name} => {_fileMap.ContainsKey(name.ToLower())}");
                    return _createFileHook.Original(_fileMap.ContainsKey(name.ToLower()) ? _fileMap[name.ToLower()] : name, access, mode,
                        attributes, disposition, andAttributes, file
                    );
                },
                this);
        }

        public void Unload()
        {
            _createFileHook.Dispose();
            _createFileHook = null;
        }

        [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.ModFrameworkInternalAPI)]
        public void ClearMappings()
        {
            _fileMap.Clear();
        }

        /// <summary>
        /// Adds a mapping to the file redirects.<br />
        /// Note that currently, this functionality is currently limited by the
        /// use of the correct path separators (e.g. forward vs. backward slashes)<br />
        /// All paths are treated as case invariant (windows)/lowercase and need to
        /// match the target application (e.g. relative versus absolute path).<br />
        /// <br />
        /// This method should NOT be called by Mods, only by the modding framework.<br />
        /// This is because conflicts cannot be handled and would overwrite each-other.<br />
        /// Instead the Framework should handle this gracefully and use a priority value
        /// or ask the user via the Host Application on a per-file basis.
        /// </summary>
        /// <param name="sourcePath">The path the target application searches for</param>
        /// <param name="destPath">The path of the file that should be redirected to</param>
        [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.ModFrameworkInternalAPI)]
        public void AddMapping(string sourcePath, string destPath)
        {
            _fileMap[sourcePath.ToLower()] = destPath;
        }
    }
}
