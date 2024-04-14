using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using Andraste.Payload.ModManagement;
using Andraste.Payload.Native;
using Andraste.Shared.Lifecycle;
using Andraste.Shared.ModManagement.Features;
using Andraste.Shared.ModManagement.Json.Features;
using Andraste.Shared.ModManagement.Json.Features.Plugin;
using EasyHook;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Andraste.Payload
{
    // TODO: Proper Shutdown detection using WM_ or something.
    public abstract class EntryPoint : IEntryPoint
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // TODO: Move into dedicated class
        public static string GameFolder;

        public static string GameExecutable;

        /// <summary>
        /// The folder where the resulting assembly dll is stored.
        /// This can be different from the Working Directory, which is the
        /// folder where the Launcher/Host Application resides.
        /// </summary>
        public static string FrameworkFolder;

        public static string ProfileFolder;

        public abstract string FrameworkName { get; }
        public abstract string Version { get; }

        public bool IsRunning { get; protected set; } = true;

        /// <summary>
        /// Determines when the application is ready.
        /// This is set, as soon as the Process has it's MainWindowHandle
        /// </summary>
        public bool IsReady => Process.GetCurrentProcess().MainWindowHandle != IntPtr.Zero;
        private bool _ready;
        
        protected readonly Dictionary<string, IFeatureParser> FeatureParser = new Dictionary<string, IFeatureParser>();
        public readonly ManagerContainer Container;
        private readonly ModLoader _modLoader;

        protected EntryPoint(RemoteHooking.IContext context, string profileFolder)
        {
            GameFolder = Directory.GetCurrentDirectory();
            GameExecutable = Process.GetCurrentProcess().MainModule!.FileName;
            FrameworkFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ProfileFolder = profileFolder;
            Container = new ManagerContainer();
            _modLoader = new ModLoader(this);
        }

        public virtual void Run(RemoteHooking.IContext context, string profileFolder)
        {
            if (ShouldSetupExceptionHandlers)
            {
                RegisterExceptionHandlers();
            }

            if (ShouldSetupLogging)
            {
                SetupLogging();
            }

            Thread.CurrentThread.Name = "Andraste Main-Thread";

            Logger.Info($"Game Executable: {GameExecutable}");
            Logger.Info($"Game Directory: {GameFolder}");
            Logger.Info($"Profile Directory: {ProfileFolder}");
            Logger.Info($"Framework Directory: {FrameworkFolder}");
            Logger.Info($".NET Plattform: {RuntimeInformation.FrameworkDescription}"); // .net 4.7.1+ 

            Logger.Trace("Loading Mods");
            LoadMods();

            Logger.Trace("Internal Initialization done, calling Pre-Wakeup");
            PreWakeup();
            
            Logger.Trace("Loading the Managers");
            Container.Load();

            Logger.Trace("Implementing Mods");
            ImplementMods();
            
            Logger.Trace("Waking up the Application");
            RemoteHooking.WakeUpProcess();
            Logger.Trace("Calling Post-Wakeup");
            PostWakeup();

            while (IsRunning)
            {
                if (!_ready && IsReady)
                {
                    // A small wait, since some contexts might still not be ready at window creation time.
                    Thread.Sleep(100);

                    Logger.Trace("Calling ApplicationReady");
                    _ready = true;
                    try
                    {
                        ApplicationReady();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Exception in ApplicationReady");
                    }
                }

                Thread.Sleep(100);
            }

            Shutdown();
        }

        protected virtual void Shutdown()
        {
            Logger.Info("Shutting down and exiting CLR");
            _modLoader?.UnloadPlugins();
            Container.Unload();
            UnregisterExceptionHandlers();
            LogManager.Flush();
            Environment.Exit(1);
        }

        #region Mods
        /// <summary>
        /// The mods have been parsed into <see cref="ModLoader.EnabledMods"/>, including their feature statements, and the
        /// framework had a chance in changing this in <see cref="PreWakeup"/>.
        /// Now the mods are "implemented", that means, the parsed features are used to dispatch hooks and others.
        /// </summary>
        protected virtual void ImplementMods()
        {
            _modLoader.ImplementVfs();
            _modLoader.ImplementPlugins();
            _modLoader.LoadPlugins();
        }

        /// <summary>
        /// Scans for a launcher-defined mods.json and examines it's contents.
        /// It will then proceed to load and register the mods, so they are available for the framework impl in
        /// PreWakeup, but they will be only activated after PreWakeup returns (but before actually waking up)
        /// </summary>
        protected virtual void LoadMods()
        {
            DiscoverMods();
            LoadFeatureParsers();
            ParseModFeatures();
        }

        protected virtual void ParseModFeatures()
        {
            foreach (var mods in _modLoader.EnabledMods)
            {
                Logger.Info($"Enabled Mod {mods.ModInformation.Slug}");
                var conf = mods.ModInformation.Configurations[mods.ModSetting.ActiveConfiguration];
                foreach (var feature in conf.Features.Keys)
                {
                    if (FeatureParser.TryGetValue(feature, out var parser))
                    {
                        var parsed = parser.Parse(conf.Features[feature]);
                        conf._parsedFeatures.Add(feature, parsed);
                    }
                    else
                    {
                        Logger.Warn($"Unknown Feature {feature}. Skipping");
                    }
                }
            }
        }

        protected virtual void DiscoverMods()
        {
            _modLoader.EnabledMods = _modLoader.DiscoverMods(ProfileFolder);
        }

        protected virtual void LoadFeatureParsers()
        {
            FeatureParser.Add("andraste.builtin.vfs", new VFSFeatureParser());
            FeatureParser.Add("andraste.builtin.plugin", new PluginFeatureParser());
        }
        #endregion
        
        #region Lifecycle
        /// <summary>
        /// This is called when Andraste has been loaded so far and the user
        /// framework is ready for action (registering hooks).
        /// This happens before the Application is launched, so regular
        /// method calls should not be called at this stage.
        /// Use <see cref="PostWakeup"/> instead.
        /// </summary>
        protected abstract void PreWakeup();

        /// <summary>
        /// This is called when Andraste has been loaded and the application
        /// has become active.
        /// This is the perfect time for a full initialization of non-critical
        /// hooks and all other elements, as this stage does NOT delay the
        /// application start-up (as opposed to <see cref="PreWakeup"/>).
        /// </summary>
        protected abstract void PostWakeup();

        /// <summary>
        /// This is called when the Application has been loaded and created
        /// it's Main Window.
        ///
        /// This happens after Pre/PostWakeup and is intended to initialize
        /// custom UI or accessing game functions (add your own, more specific
        /// checks, if possible).
        /// </summary>
        protected abstract void ApplicationReady();
        #endregion

        #region Logging
        protected bool ShouldSetupLogging { get; set; } = true;
        /// <summary>
        /// Sets a default NLog logging up. Stub this method if you plan on providing your own backend.
        /// Using a different logging framework is not supported, because Andraste will use NLog internally.
        /// </summary>
        protected virtual void SetupLogging()
        {
            // Workaround: For some reason, NLog is only flushing/deleting the file, once you log something to it,
            // so we're going to force-delete the files for now.
            var outputLog = Path.Combine(ProfileFolder, "output.log");
            var errorLog = Path.Combine(ProfileFolder, "error.log");
            
            if (File.Exists(outputLog))
            {
                File.Delete(outputLog);
            }
            
            if (File.Exists(errorLog))
            {
                File.Delete(errorLog);
            }
            
            var cfg = new LoggingConfiguration();
            var fileStdout = new FileTarget("fileStdout")
            {
                FileName = outputLog, AutoFlush = true, DeleteOldFileOnStartup = true,
                Layout = "${longdate}|${threadname}(${threadid})|${level:uppercase=true}|${logger}|${message}"
            };

            // TODO: Proper \r\n before exception, but only if there is an exception...
            var fileErr = new FileTarget("fileErr")
            {
                FileName = errorLog, AutoFlush = false, DeleteOldFileOnStartup = true,
                // Default from https://github.com/NLog/NLog/wiki/File-target#layout-options
                Layout = "${longdate}|${threadname}(${threadid})|${level:uppercase=true}|${logger}|${message}${exception:format=ToString}"
            };

            cfg.AddRule(LogLevel.Trace, LogLevel.Warn, fileStdout);
            cfg.AddRule(LogLevel.Error, LogLevel.Fatal, fileErr);
            LogManager.Configuration = cfg;
        }
        #endregion

        #region Exception Handlers
        /// <summary>
        /// Whether this should setup the exception handlers to display a
        /// message dialog for uncaught exceptions, that could terminate
        /// the game otherwise.
        ///
        /// Set to false before <see cref="Run"/> is called or use
        /// <see cref="RegisterExceptionHandlers"/> and
        /// <see cref="UnregisterExceptionHandlers"/>.
        /// </summary>
        protected bool ShouldSetupExceptionHandlers { get; set; } = true;

        protected void RegisterExceptionHandlers()
        {
            // TODO: refactor ProcessExit, so that it's always hooked and can properly do the destruction
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected void UnregisterExceptionHandlers()
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.FirstChanceException -= CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        }

        protected virtual void CurrentDomain_FirstChanceException(object sender,
            FirstChanceExceptionEventArgs e)
        {
            Logger.Fatal(e.Exception, $"Got uncaught FirstChance Exception{Environment.NewLine}");
            var text = "An Uncaught Exception has happened and the game will now crash!\n" +
                       $"This is definitely caused by Andraste / {FrameworkName}!\n\n" +
                       $"---------------------\n{e.Exception}";

            var mwh = Process.GetCurrentProcess().MainWindowHandle;
            const uint MB_OK = 0;
            const uint MB_ICONERROR = 0x00000010U;
            User32.MessageBox(mwh != IntPtr.Zero ? mwh : IntPtr.Zero, text, "Uncaught Exception", MB_OK | MB_ICONERROR);

            // TODO: It could still be handled?
            IsRunning = false;
            Shutdown(); // TODO: Is this the best way of doing it? UnhandledException Handler is not called afterwards...
            // @TODO: We should maybe just unload all hooks so the game can continue running
        }

        protected virtual void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Fatal(e.ExceptionObject as Exception, $"Got uncaught Exception{Environment.NewLine}");
            var text = "An Uncaught Exception has happened and the game will now crash!\n" +
                       $"This is definitely caused by Andraste / {FrameworkName}!\n\n" +
                       $"---------------------\n{e.ExceptionObject}";
            var mwh = Process.GetCurrentProcess().MainWindowHandle;
            const uint MB_OK = 0;
            const uint MB_ICONERROR = 0x00000010U;
            User32.MessageBox(mwh != IntPtr.Zero ? mwh : IntPtr.Zero, text, "Uncaught Exception", MB_OK | MB_ICONERROR);

            if (e.IsTerminating)
            {
                IsRunning = false;
                Shutdown(); // Run() won't be running anymore if it has caused the exception
            }
            // @TODO: We should maybe just unload all hooks so the game can continue running
        }

        protected virtual void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Logger.Info("Got ProcessExit: Shutting down!");
            IsRunning = false;
        }
        #endregion
    }
}
