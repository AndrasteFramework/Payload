using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Andraste.Payload.Native;
#if NETFX
using Andraste.Payload.Util;
#endif
using EasyHook;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Andraste.Payload
{
    // TODO: Proper Shutdown detection using WM_ or something.
    public abstract class EntryPoint : IEntryPoint
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // TODO: Move into dedicated class
        public static string GameFolder;

        /// <summary>
        /// The folder where the resulting assembly dll is stored.
        /// This can be different from the Working Directory, which is the
        /// folder where the Launcher/Host Application resides.
        /// </summary>
        public static string ModFolder;

        public static string HostFolder =>
            throw new NotImplementedException("TODO: NLog can somehow determine the path of the Host Application.");

        public abstract string FrameworkName { get; }
        public abstract string Version { get; }

        public bool IsRunning { get; protected set; } = true;

        /// <summary>
        /// Determines when the application is ready.
        /// This is set, as soon as the Process has it's MainWindowHandle
        /// </summary>
        public bool IsReady => Process.GetCurrentProcess().MainWindowHandle != IntPtr.Zero;
        private bool _ready;

        protected EntryPoint(RemoteHooking.IContext context)
        {
            GameFolder = Directory.GetCurrentDirectory();
            ModFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public virtual void Run(RemoteHooking.IContext context)
        {
            if (ShouldSetupExceptionHandlers)
            {
                RegisterExceptionHandlers();
            }

            if (ShouldSetupLogging)
            {
                SetupLogging();
            }

            logger.Info($"Game Directory: {GameFolder}");
            logger.Info($"Mod Directory: {ModFolder}");
            //logger.Info($"Host Directory: {HostFolder}");
            // .net 4.7.1+ 
            logger.Info($".NET Plattform: {RuntimeInformation.FrameworkDescription}");

            logger.Info("Internal Initialization done, calling Pre-Wakeup");

            PreWakeup();
            logger.Info("Waking up the Application");
            RemoteHooking.WakeUpProcess();
            logger.Info("Calling Post-Wakeup");
            PostWakeup();

            while (IsRunning)
            {
                if (!_ready && IsReady)
                {
                    // A small wait, since some contexts might still not be ready at window creation time.
                    System.Threading.Thread.Sleep(100);
                    logger.Info("Calling ApplicationReady");
                    _ready = true;
                    ApplicationReady();
                }

                System.Threading.Thread.Sleep(100);
            }

            Shutdown();
        }

        protected virtual void Shutdown()
        {
            logger.Info("Shutting down and exiting CLR");
            UnregisterExceptionHandlers();
            Environment.Exit(1);
        }

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
            var cfg = new LoggingConfiguration();

            var fileStdout = new FileTarget("fileStdout")
            {
                FileName = "output.log", AutoFlush = true, DeleteOldFileOnStartup = true
            };

            // TODO: Proper \r\n before exception, but only if there is an exception...
            var fileErr = new FileTarget("fileErr")
            {
                FileName = "error.log", AutoFlush = false, DeleteOldFileOnStartup = true,
                // Default from https://github.com/NLog/NLog/wiki/File-target#layout-options
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${exception:format=ToString}"
            };

            cfg.AddRule(LogLevel.Info, LogLevel.Warn, fileStdout);
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
            System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            logger.Fatal(e.Exception, $"Got uncaught FirstChance Exception{Environment.NewLine}");
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
            logger.Fatal(e.ExceptionObject as Exception, $"Got uncaught Exception{Environment.NewLine}");
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
            logger.Info("Got ProcessExit: Shutting down!");
            IsRunning = false;
        }
        #endregion
    }
}
