namespace Andraste.Payload.ModManagement
{
    /// <summary>
    /// Generic events are a mechanism of andraste for lifecycle management in a non-api breaking way:
    /// If plugins are older than andraste, they just don't know the event yet,
    /// If they are newer than andraste, the related events just won't be thrown.
    ///
    /// Deriving frameworks CAN use this mechanism (provided they use a large enough base offset,
    /// that's why this enum is 32bit), but due to the higher specialization, consider using C# Events
    /// instead that are preferably bound and provided by managers. 
    /// </summary>
    public enum EGenericEvent: uint
    {
        /// <summary>
        /// This is called immediately before waking up the process (execute its entry point)
        /// </summary>
        EPreWakeup = 1,
        /// <summary>
        /// This is called right after the processes entry point has been executed
        /// </summary>
        EPostWakeup = 2,
        /// <summary>
        /// This is called as soon as the process has created its first window (typically meaning
        /// the rendering engine has been initialized and the application is loading).
        /// Note: Some games re-create their window in their boot process, that's why the rendering hooks
        /// in Andraste still wait a moment.
        /// </summary>
        EApplicationReady = 3,
    }
}