using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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
        private readonly Logger _logger = LogManager.GetCurrentClassLogger(); 
        // TODO: What about "OpenFile" for older applications? What about CreateFileW?
        private Hook<Kernel32.Delegate_CreateFileA> _createFileHook;
        private Hook<Kernel32.DelegateFindFirstFileA> _findFirstFileHook;
        private Hook<Kernel32.Delegate_CreateDirectoryA> _createDirectoryHook;
        private Hook<Kernel32.Delegate_DeleteFileA> _deleteFileHook;
        private Hook<Kernel32.Delegate_RemoveDirectoryA> _removeDirectoryHook;
        private Hook<Shell32.Delegate_SHFileOperationA> _shFileOperationHook;
        private readonly ConcurrentDictionary<string, string> _fileMap = new ConcurrentDictionary<string, string>();
        private readonly List<Hook> _hooks = new List<Hook>();
        private readonly SortedList<string, string> _prefixMap = new SortedList<string, string>();
        private readonly Mutex _prefixMapEnumerationLock = new Mutex();

        public bool Enabled
        {
            get => _hooks.All(hook => hook.IsActive);
            set
            {
                if (value)
                {
                    _hooks.ForEach(hook => hook.Activate());
                }
                else
                {
                    _hooks.ForEach(hook => hook.Deactivate());
                }
            }
        }

        public bool Loaded => _hooks.Count > 0;

        public void Load()
        {
            // TODO: be more clever about the name: Forward/Backward Slashes, "\\?\", Streams, etc. Maybe collect real world use-cases first?
            _createFileHook = new Hook<Kernel32.Delegate_CreateFileA>(
                LocalHook.GetProcAddress("kernel32.dll", "CreateFileA"),
                (name, access, mode, attributes, disposition, andAttributes, file) =>
                {
                    name = ApplyPrefixMapping(SanitizePath(name));
                    var queryFile = name;
                    // Debug Logging
                    // _logger.Trace($"CreateFileA {name} ({queryFile}) => {_fileMap.ContainsKey(queryFile)}");
                    //if (_fileMap.ContainsKey(queryFile)) _logger.Trace($"{queryFile} redirected to {_fileMap[queryFile]}");
                    //if (!_fileMap.ContainsKey(queryFile)) _logger.Trace($"{queryFile} could not be redirected");
                    var fileName = _fileMap.ContainsKey(queryFile) ? _fileMap[queryFile] : name;

                    return _createFileHook.Original(fileName, access, mode,attributes, disposition, andAttributes, file);
                },
                this);
            _hooks.Add(_createFileHook);
            
            _findFirstFileHook = new Hook<Kernel32.DelegateFindFirstFileA>(
                LocalHook.GetProcAddress("kernel32.dll", "FindFirstFileA"),
                (name, data) =>
                {
                    name = ApplyPrefixMapping(SanitizePath(name));
                    if (name.Contains("*") || name.Contains("?"))
                    {
                        // Wildcards are not supported yet (we'd need to fake all search results and manage the handle)
                        return _findFirstFileHook.Original(name, data);
                    }
                    
                    // Games like Test Drive Unlimited (2006) are abusing FindFirstFile with an explicit file name to 
                    // get all file attributes, such as the  file size.
                    var queryFile = name;
                    var fileName = _fileMap.ContainsKey(queryFile) ? _fileMap[queryFile] : name;

                    return _findFirstFileHook.Original(fileName, data);
                }, this);
            _hooks.Add(_findFirstFileHook);

            _createDirectoryHook = new Hook<Kernel32.Delegate_CreateDirectoryA>(
                LocalHook.GetProcAddress("kernel32.dll", "CreateDirectoryA"),
                (name, attributes) =>
                {
                    //_logger.Trace("CreateDirectoryA hook with " + name);

                    // TDU uses CreateDirectoryA to:
                    // check if playersave/playersave2 directories exist
                    // check if it's data directory in ProgramData exists
                    var fileName = ApplyPrefixMapping(SanitizePath(name));

                    return _createDirectoryHook.Original(fileName, attributes);
                }, this);
            _hooks.Add(_createDirectoryHook);

            _deleteFileHook = new Hook<Kernel32.Delegate_DeleteFileA>(
                LocalHook.GetProcAddress("kernel32.dll", "DeleteFileA"),
                (name) =>
                {
                    _logger.Trace("DeleteFileA with " + name);
                    name = ApplyPrefixMapping(SanitizePath(name));
                    var queryFile = name;
                    var fileName = _fileMap.ContainsKey(queryFile) ? _fileMap[queryFile] : name;
                    return _deleteFileHook.Original(fileName);
                }, this);
            _hooks.Add(_deleteFileHook);

            _removeDirectoryHook = new Hook<Kernel32.Delegate_RemoveDirectoryA>(
                LocalHook.GetProcAddress("kernel32.dll", "RemoveDirectoryA"),
                (name) =>
                {
                    _logger.Trace("RemoveDirectoryA with " + name);
                    var fileName = ApplyPrefixMapping(SanitizePath(name));
                    return _removeDirectoryHook.Original(fileName);
                }, this);
            _hooks.Add(_removeDirectoryHook);

            // hmm it seems that shell32.dll can remove files directly without using the above functions
            _shFileOperationHook = new Hook<Shell32.Delegate_SHFileOperationA>(
                LocalHook.GetProcAddress("shell32.dll", "SHFileOperationA"),
                (lpFileOp) =>
                {
                    uint op = Extract32BitUintFromAddress(lpFileOp + 4);
                    IntPtr from_ptr = Marshal.ReadIntPtr(lpFileOp + 8);
                    IntPtr to_ptr = Marshal.ReadIntPtr(lpFileOp + 12);
                    _logger.Trace("SHFileOperationA with op " + op);
                    // TODO implement more ops maybe, if other games need them down the road
                    // see https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-shfileopstructa
                    // tdu uses this to recursively remove profile
                    if (op == 3)
                    {
                        string from = Marshal.PtrToStringAnsi(from_ptr);
                        from = ApplyPrefixMapping(SanitizePath(from));
                        if (Directory.Exists(from))
                        {
                            _logger.Trace("SHFileOperationA recursively removing " + from);
                            Directory.Delete(from, true);
                        }
                        else
                        {
                            from = _fileMap.ContainsKey(from) ? _fileMap[from] : from;
                            if (File.Exists(from))
                            {
                                _logger.Trace("SHFileOperationA removing single file " + from);
                                File.Delete(from);
                            }
                            else
                            {
                                _logger.Trace("SHFileOperationA cowardly not removing supposingly non-existing file " + from);
                            }
                        }
                        return 0;
                    }
                    return _shFileOperationHook.Original(lpFileOp);
                }, this);
            _hooks.Add(_shFileOperationHook);
        }

        private uint Extract32BitUintFromAddress(IntPtr address)
        {
            var buf = new byte[4];
            Marshal.Copy(address, buf, 0, 4);
            return BitConverter.ToUInt32(buf, 0);
        }

        private string SanitizePath(string fileName)
        {
            // Maybe we could instead do something like Path.Resolve on fileName, so it's always an absolute path first, to get rid of relative path-isms.
            string result;

            if (fileName.ToLowerInvariant().StartsWith(EntryPoint.GameFolder.ToLowerInvariant()))
            {
                result = fileName.ToLowerInvariant().Substring(EntryPoint.GameFolder.ToLowerInvariant().Length);
                // Depending on if the GameFolder has a trailing slash:
                if (result.StartsWith("\\"))
                {
                    result = result.Substring(1);
                }
            }
            else
            {
                result = fileName.ToLowerInvariant();
            }

            // Replace forward slashes
            result = result.Replace('/', '\\');
            return result;
        }
        private string ApplyPrefixMapping(string sourcePath)
        {
            //_logger.Trace("processing " + sourcePath);
            _prefixMapEnumerationLock.WaitOne(-1);
            foreach (var entry in _prefixMap.Reverse())
            {
                if (sourcePath.ToLower().StartsWith(entry.Key))
                {
                    string ret = entry.Value + sourcePath.Substring(entry.Key.Length);
                    //_logger.Trace("redirecting " + sourcePath + " to " + ret);
                    _prefixMapEnumerationLock.ReleaseMutex();
                    return ret;
                }
            }
            _prefixMapEnumerationLock.ReleaseMutex();
            return sourcePath;
        }

        public void Unload()
        {
            _hooks.ForEach(hook => hook.Dispose());
            _hooks.Clear();
        }

        [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.ModFrameworkInternalAPI)]
        public void ClearMappings()
        {
            _fileMap.Clear();
            _prefixMap.Clear();
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

        /// <summary>
        /// Adds a custom prefix redirect.<br />
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
        public void AddPrefixMapping(string sourcePath, string destPath)
        {
            _prefixMap.Add(sourcePath.ToLower(), destPath);
            /*
            _logger.Trace("printing current _prefixMap");
            foreach(var entry in _prefixMap.Reverse())
            {
                _logger.Trace(entry.Key + ", " + entry.Value);
            }
            */
        }

        #nullable enable
        /// <summary>
        /// Allows other file reading utilities inside Andraste to support VFS redirects by querying them.
        /// </summary>
        /// <param name="sourcePath">The game path</param>
        /// <returns>The redirected mod path or null.</returns>
        [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.ModFrameworkInternalAPI)]
        public string? QueryMapping(string sourcePath)
        {
            var queryFile = SanitizePath(sourcePath);
            return _fileMap.ContainsKey(queryFile) ? _fileMap[queryFile] : null;
        }
        #nullable restore
    }
}
