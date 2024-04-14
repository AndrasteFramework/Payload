using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;

namespace Andraste.Payload.D3D9
{
    // Idea: fill the known-bad list with all shaders that have been happening before the crash. Then gradually
    // always no-op half of them and expect the game to crash. When it does, note the half that hasn't been no-opped.
    // Restart the game.
    public class DriverCrashDetector
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Random Rng = new Random();
        
        private readonly HashSet<string> _pixelShaders = new HashSet<string>();
        private readonly HashSet<string> _vertexShaders = new HashSet<string>();

        // We should only ever debug one type of shaders at the same time!
        private readonly bool _debugPixelShaders;
        
        // We keep track of the shaders that have been part of the crash, but not been disabled, this is the set to minimize for
        private HashSet<string> _knownBadPixelShaders = new HashSet<string>();
        private HashSet<string> _knownBadVertexShaders = new HashSet<string>();
        
        // we keep track of known "good" shaders (i.e. those that have been disabled, but the game still crashing.
        // Note that this only works if only one shader is broken), so that the UI and others get more visible over time
        // because otherwise we'd converge to disabling every shader besides the faulting and then you can't really
        // control the game. We need to be careful on what to label as good, though.
        private HashSet<string> _knownGoodPixelShaders = new HashSet<string>();
        private HashSet<string> _knownGoodVertexShaders = new HashSet<string>();
        private HashSet<string> _dropped = new HashSet<string>();

        public DriverCrashDetector(float aggressivity, bool debugPixelShaders = true)
        {
            _debugPixelShaders = debugPixelShaders;
            if (File.Exists("known-bad-ps.lst"))
            {
                foreach (var hash in File.ReadAllLines("known-bad-ps.lst"))
                {
                    _knownBadPixelShaders.Add(hash);
                }
            }

            if (File.Exists("known-good-ps.lst"))
            {
                foreach (var hash in File.ReadAllLines("known-good-ps.lst"))
                {
                    _knownGoodPixelShaders.Add(hash);
                }
            }
            
            if (File.Exists("known-bad-vs.lst"))
            {
                foreach (var hash in File.ReadAllLines("known-bad-vs.lst"))
                {
                    _knownBadVertexShaders.Add(hash);
                }
            }

            if (File.Exists("known-good-vs.lst"))
            {
                foreach (var hash in File.ReadAllLines("known-good-vs.lst"))
                {
                    _knownGoodVertexShaders.Add(hash);
                }
            }

            // I don't know how this could happen, but it would reduce the aggressivity significantly
            _knownBadPixelShaders.RemoveWhere(hash => _knownGoodPixelShaders.Contains(hash));
            _knownBadVertexShaders.RemoveWhere(hash => _knownGoodVertexShaders.Contains(hash));

            if (_debugPixelShaders)
            {
                if (_knownBadPixelShaders.Count == 1)
                {
                    Logger.Error($"ONLY REMAINING: {_knownBadPixelShaders.First()}");
                }
                else if (_knownBadPixelShaders.Count > 1)
                {
                    var count = (int)Math.Floor(_knownBadPixelShaders.Count * aggressivity);
                    Logger.Error($"Dropping a random {aggressivity * 100} % ({count} shaders)");
                    var tmpList = _knownBadPixelShaders
                        .OrderBy(a => Rng.Next()) // random
                        .ToList();

                    // Dump the dropped ones for diagnostic reasons
                    _dropped = tmpList.Take(count).OrderBy(s => s).ToHashSet();
                    File.WriteAllLines("dropped-ps.lst", _dropped);

                    _knownBadPixelShaders = tmpList
                        .Skip(count) // drop aggressivity % -> make them render again
                        .ToHashSet();
                    Logger.Error($"{_knownBadPixelShaders.Count} shaders remaining");

                    // Only those in knownBad will be skipped by the renderer (replaced with the default shader). So the hope
                    // is that we will still crash again. If we don't, any of the non-dropped shaders have been part of the crash
                    // This suboptimal implementation will just require restarting then, randomness will probably drop the shader
                    // in the next iteration. This is basically because we can't automatically determine a known "good" state, only
                    // a bad state. So in the good state, the user needs to relaunch.
                }
            }
            else
            {
                if (_knownBadVertexShaders.Count == 1)
                {
                    Logger.Error($"ONLY REMAINING: {_knownBadVertexShaders.First()}");
                }
                else if (_knownBadVertexShaders.Count > 1)
                {
                    var count = (int)Math.Floor(_knownBadVertexShaders.Count * aggressivity);
                    Logger.Error($"Dropping a random {aggressivity * 100} % ({count} shaders)");
                    var tmpList = _knownBadVertexShaders
                        .OrderBy(a => Rng.Next()) // random
                        .ToList();

                    // Dump the dropped ones for diagnostic reasons
                    _dropped = tmpList.Take(count).OrderBy(s => s).ToHashSet();
                    File.WriteAllLines("dropped-vs.lst", _dropped);

                    _knownBadVertexShaders = tmpList
                        .Skip(count) // drop aggressivity % -> make them render again
                        .ToHashSet();
                    Logger.Error($"{_knownBadVertexShaders.Count} shaders remaining");
                }
            }
        }

        public bool OnCreateVertexShader(string shaderHash)
        {
            if (_debugPixelShaders)
            {
                return false;
            }
            
            _vertexShaders.Add(shaderHash);
            
            // return value: shall disable shader?
            if (_knownGoodVertexShaders.Contains(shaderHash))
            {
                return false;
            }

            if (_knownBadVertexShaders.Contains(shaderHash))
            {
                return true;
            }

            return false; // Shader that has been recently dropped out of the bad shaders (or first run)
        }

        public bool OnCreatePixelShader(string shaderHash)
        {
            if (!_debugPixelShaders)
            {
                return false;
            }
            
            _pixelShaders.Add(shaderHash);
            
            // return value: shall disable shader?
            if (_knownGoodPixelShaders.Contains(shaderHash))
            {
                return false;
            }

            if (_knownBadPixelShaders.Contains(shaderHash))
            {
                return true;
            }

            return false; // Shader that has been recently dropped out of the bad shaders (or first run)
        }

        public void HandleCrash()
        {
            // Special case: shader that was previously in known-bad hasn't been seen this time, so it's automatically removed from that list.
            // Special case: shader that wasn't on the known-bad: It will be added to known-bad despite most likely not being part of the problem,
            // _but_ it doesn't hurt to have investigated every shader at least once, especially for when multiple shaders
            // could be part of the problem. There, only fixing one shader doesn't help us at all.
            // Special case: multiple shaders are part of the problem, that means the game crashes with a small knownBad
            // and thus, fills it again with loads of shaders.
            
            // 1. Shaders that have been disabled, can't be the culprit, only the ones that had been dropped, so add them to knownGood:
            if (_debugPixelShaders)
            {
                foreach (var shaderHash in _knownBadPixelShaders)
                {
                    _knownGoodPixelShaders.Add(shaderHash);
                }
            }
            else
            {
                foreach (var shaderHash in _knownBadVertexShaders)
                {
                    _knownGoodVertexShaders.Add(shaderHash);
                }
            }

            // 2. Thus, every shader that isn't good (i.e. had been disabled once) is potentially bad.
            List<string> badShaders;
            if (_debugPixelShaders)
            {
                badShaders = _pixelShaders
                    .Where(shader => !_knownGoodPixelShaders.Contains(shader))
                    .ToList();

                if (badShaders.Any(shader => _knownGoodPixelShaders.Contains(shader)))
                {
                    Logger.Error("Bad Shaders contains a known good shader!");
                }

                // if (!badShaders.All(shader => _dropped.Contains(shader)))
                // {
                //     Logger.Error("New Shaders"); 
                // }
            }
            else
            {
                badShaders = _vertexShaders
                    .Where(shader => !_knownGoodVertexShaders.Contains(shader))
                    .ToList();

                if (badShaders.Any(shader => _knownGoodVertexShaders.Contains(shader)))
                {
                    Logger.Error("Bad Shaders contains a known good shader!");
                }
            }

            // 3. Dump the new good shaders. This is the one responsible for shrinking the bad list, actually!
            if (_debugPixelShaders)
            {
                File.WriteAllLines("known-good-ps.lst", _knownGoodPixelShaders.OrderBy(s => s));
            }
            else
            {
                File.WriteAllLines("known-good-vs.lst", _knownGoodVertexShaders.OrderBy(s => s));
            }

            // 4. Dump the new bad shaders.
            if (_debugPixelShaders)
            {
                File.WriteAllLines("known-bad-ps.lst", badShaders.OrderBy(s => s));
            }
            else
            {
                File.WriteAllLines("known-bad-vs.lst", badShaders.OrderBy(s => s));
            }

            Logger.Error($"State dumped {badShaders.Count} shaders, exiting");
            Environment.Exit(1337);
        }
    }
}