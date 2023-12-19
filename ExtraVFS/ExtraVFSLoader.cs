using Andraste.Payload.VFS;
using System;
using System.IO;
using System.Text.Json;
using NLog;

namespace Andraste.Payload.ExtraVFS
{
    public class ExtraVFSPairs
    {
        public ExtraVFSPair[] PathPairs { get; set; }
    }
    public class ExtraVFSPair
    {
        public string Source { get; set; }
        public string Dest { get; set; }
        public bool DestHasToExist { get; set; }
    }
    public class ExtraVFSLoader
    {
        private EntryPoint _entryPoint;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public ExtraVFSLoader(EntryPoint entryPoint)
        {
            _entryPoint = entryPoint;
        }

        public void LoadExtraVFSFromJson(string modFolder)
        {
            var vfs = _entryPoint.Container.GetManager<BasicFileRedirectingManager>();
            if (vfs != null)
            {
                var jsonFile = Path.Combine(modFolder, "extra_vfs.json");
                if (!File.Exists(jsonFile))
                {
                    var empty_template = new ExtraVFSPairs();
                    empty_template.PathPairs = new ExtraVFSPair[1];
                    empty_template.PathPairs[0] = new ExtraVFSPair();
                    empty_template.PathPairs[0].Source = "C:\\Some\\Savegame\\Path";
                    empty_template.PathPairs[0].Dest = "C:\\Some\\Savegame\\Path";
                    empty_template.PathPairs[0].DestHasToExist = false;
                    var empty_template_string = JsonSerializer.SerializeToUtf8Bytes(empty_template, new JsonSerializerOptions { WriteIndented = true});
                    try
                    {
                        File.WriteAllBytes(jsonFile, empty_template_string);
                    }
                    catch(Exception ex)
                    {
                        Logger.Warn("Couldn't create template extra_vfs.json, " + ex);
                    }
                }

                try
                {
                    var pairs = JsonSerializer.Deserialize<ExtraVFSPairs>(File.ReadAllText(jsonFile));
                    if (pairs == null)
                    {
                        Logger.Warn("Couldn't properly parse extra_vfs.json");
                        return;
                    }

                    if (pairs.PathPairs == null)
                    {
                        return;
                    }

                    foreach (var pair in pairs.PathPairs)
                    {
                        if (!Directory.Exists(pair.Dest) && pair.DestHasToExist)
                        {
                            try
                            {
                                Directory.CreateDirectory(pair.Dest);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("Destination " + pair.Dest + " does not exist and cannot be created, " + ex);
                                throw ex;
                            }
                        }
                        Logger.Trace("adding pair " + pair.Source + ", " + pair.Dest);
                        vfs.AddPrefixMapping(pair.Source.Replace('/', '\\'), pair.Dest.Replace('/', '\\'));
                    }
                }
                catch(Exception ex)
                {
                    Logger.Warn("ExtraVFS: Couldn't properly parse extra_vfs.json, " + ex);
                }
                return;
            }
            else
            {
                Logger.Info($"The Framework {_entryPoint.FrameworkName} has not enabled VFS Features");
            }
        }
    }
}
