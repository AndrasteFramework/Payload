using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Andraste.Payload.VFS;
using Andraste.Shared.ModManagement;
using Andraste.Shared.ModManagement.DependencyResolution;
using Andraste.Shared.ModManagement.Json;
using Andraste.Shared.ModManagement.Json.Features.Plugin;
using Andraste.Shared.ModManagement.Json.Features.Vfs;
using NLog;
using Semver;

namespace Andraste.Payload.ModManagement
{
    public class ModLoader
    {
        private readonly EntryPoint _entryPoint;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public List<EnabledMod> EnabledMods = new List<EnabledMod>();
        public readonly Dictionary<EnabledMod, IPlugin> Plugins = new Dictionary<EnabledMod, IPlugin>();
        
        public ModLoader(EntryPoint entryPoint)
        {
            _entryPoint = entryPoint;
        }

        public virtual void ImplementVfs()
        {
            var vfs = _entryPoint.Container.GetManager<BasicFileRedirectingManager>();
            if (vfs != null)
            {
                foreach (var mod in EnabledMods.Where(x =>
                             x.ActiveConfiguration._parsedFeatures.ContainsKey("andraste.builtin.vfs")))
                {
                    BuiltinVfsFeature feature = mod.ActiveConfiguration._parsedFeatures["andraste.builtin.vfs"];
                    foreach (var directory in feature.Directories)
                    {
                        var basePath = Path.Combine(mod.ModSetting.ModPath, directory);
                        Logger.Info($"Enumerating {basePath}");
                        foreach (var file in Directory.EnumerateFiles(basePath, "*",
                                     SearchOption.AllDirectories))
                        {
                            var relativePath = file.Substring(basePath.Length);
                            Logger.Trace($"Registering {relativePath} as VFS for {mod.ModInformation.Slug}");
                            vfs.AddMapping(relativePath, file.Replace('/', '\\'));
                        }
                    }

                    foreach (var fileEntry in feature.Files)
                    {
                        var absolutePath = Path.Combine(mod.ModSetting.ModPath, fileEntry.Value);
                        
                        if (fileEntry.Value != "INTENTIONAL")
                        {
                            if (!File.Exists(absolutePath))
                            {
                                Logger.Warn($"{mod.ModInformation.Slug} is trying to redirect \"{fileEntry.Key}\" to non-existent \"{fileEntry.Value}\". " +
                                            "If this is intentional, redirect it to \"INTENTIONAL\"");
                            }
                        }
                        Logger.Trace($"Registering {fileEntry.Key} as VFS for {mod.ModInformation.Slug}");
                        vfs.AddMapping(fileEntry.Key, absolutePath);
                    }
                }
            }
            else
            {
                Logger.Info($"The Framework {_entryPoint.FrameworkName} has not enabled VFS Features");
            }
        }
        
        public virtual List<EnabledMod> DiscoverMods(string modFolder)
        {
            var enabledMods = new List<EnabledMod>();
            var jsonFile = Path.Combine(modFolder, "mods.json");
            if (!File.Exists(jsonFile))
            {
                return enabledMods;
            }

            try
            {
                var settings = JsonSerializer.Deserialize<ModSettings>(File.ReadAllText(jsonFile));
                if (settings == null)
                {
                    Logger.Warn("Couldn't properly parse mods.json");
                    return enabledMods;
                }

                foreach (var mod in settings.EnabledMods ?? Array.Empty<ModSetting>())
                {
                    Logger.Info($"Discovered Mod at {mod.ModPath}");
                    var modPath = Path.Combine(mod.ModPath, "mod.json");
                    if (!File.Exists(modPath))
                    {
                        Logger.Warn($"Missing file {modPath}, even though mod has been enabled by the Launcher!");
                        continue;
                    }

                    var modInfo = ModInformationParser.ParseString(File.ReadAllText(modPath));
                    if (!ModInformationParser.Validate(modInfo))
                    {
                        Logger.Warn($"Invalid Mod Information in {modPath}. Validation failed");
                        continue;
                    }

                    Logger.Info($"Discovered Mod is {modInfo.Name} [{modInfo.Slug}] Version {modInfo.DisplayVersion}!");
                    enabledMods.Add(new EnabledMod(modInfo, mod));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Exception caught when trying to load mods");
            }

            return enabledMods;
        }

        public void ImplementPlugins()
        {
            foreach (var mod in EnabledMods.Where(x =>
                         x.ActiveConfiguration._parsedFeatures.ContainsKey("andraste.builtin.plugin")))
            {
                BuiltinPluginFeature feature = mod.ActiveConfiguration._parsedFeatures["andraste.builtin.plugin"];
                var dll = Path.Combine(mod.ModSetting.ModPath, feature.AssemblyFilePath);

                if (!File.Exists(dll))
                {
                    Logger.Warn($"Cannot load the plugin of {mod.ModInformation.Slug}, because {dll} does not exist");
                    continue;
                }

                try
                {
                    var assembly = Assembly.LoadFile(dll);
                    var pluginType = assembly.GetType(feature.PluginClassName, false, true);
                    if (pluginType == null)
                    {
                        Logger.Warn($"Cannot load the plugin of {mod.ModInformation.Slug}, " +
                                    $"because the type {feature.PluginClassName} does not exist " +
                                    "(or another error has happened)");
                        continue;
                    }

                    if (!typeof(IPlugin).IsAssignableFrom(pluginType))
                    {
                        Logger.Warn($"Cannot load the plugin of {mod.ModInformation.Slug}, " +
                                    $"because the type {feature.PluginClassName} is no IPlugin");
                        continue;
                    }

                    var plugin = (IPlugin)Activator.CreateInstance(pluginType);
                    plugin.Bind(mod, _entryPoint);
                    Plugins.Add(mod, plugin);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Exception when trying to load the plugin of {mod.ModInformation.Slug}");
                }
            }
        }

        public void LoadPlugins()
        {
            foreach (var plugin in Plugins.Values)
            {
                plugin.Load();
            }

            foreach (var plugin in Plugins.Values)
            {
                plugin.Enabled = true;
            }
        }

        public void UnloadPlugins()
        {
            foreach (var plugin in Plugins.Values.Where(plugin => plugin.Enabled))
            {
                plugin.Enabled = false;
            }

            foreach (var plugin in Plugins.Values.Where(plugin => plugin.Loaded))
            {
                plugin.Unload();
            }
        }

        public void EmitGenericEvent(EGenericEvent genericEvent)
        {
            foreach (var plugin in Plugins.Values.Where(plugin => plugin.Enabled && plugin.Loaded))
            {
                try
                {
                    plugin.OnGenericEvent(genericEvent);
                }
                catch (Exception exception)
                {
                    Logger.Warn(exception, "Exception during generic event handling of a plugin");
                }
            }
        }

        public void ValidateDependencies(Dictionary<string, SemVersion> builtInDependencies)
        {
            var availableMods = EnabledMods.ToDictionary(mod => mod.ModInformation.Slug, mod => mod.ModInformation.SemanticVersion);
            foreach (var dependency in builtInDependencies)
            {
                availableMods.Add(dependency.Key, dependency.Value);
            }
            
            var requirements = EnabledMods.SelectMany(mod =>
            {
                if (mod.ModInformation.Dependencies == null)
                {
                    return Enumerable.Empty<IDependencyVersionRequirement>();
                }
                
                return mod.ModInformation.Dependencies.Select(dependency =>
                    DependencyResolver.ParseDependency(mod.ModInformation.Slug, dependency));
            }).ToList();
            
            // Some old (pre andraste 0.3.0) mods don't have a SemanticVersion
            var results = DependencyResolver.ValidateRequirementsSimple(availableMods, requirements);
            if (results.Count == 0)
            {
                return;
            }
            
            Logger.Warn("Found {} dependency constraint violation(s)", results.Count);
            EnabledMods.RemoveAll(mod => results.Any(result => result.SourceSlug == mod.ModInformation.Slug));
            
            foreach (var violation in results)
            {
                Logger.Warn("Disabled {}, because the dependency on {} couldn't be solved. Type: {}, Message: {}", 
                    violation.SourceSlug, violation.TargetSlug, violation.ViolationType, violation.Message ?? "");
            }
        }
    }
}