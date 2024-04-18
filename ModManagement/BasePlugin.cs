using Andraste.Shared.ModManagement;

namespace Andraste.Payload.ModManagement
{
    public abstract class BasePlugin : IPlugin
    {
        public EnabledMod ModInstance { get; private set; }
        public EntryPoint EntryPoint { get; private set; }

        public void Bind(EnabledMod modInstance, EntryPoint entryPoint)
        {
            ModInstance = modInstance;
            EntryPoint = entryPoint;
        }

        public bool Loaded { get; private set; }

        public void Load()
        {
            lock (this)
            {
                PluginLoad();
                Loaded = true;
            }
        }

        public void Unload()
        {
            lock (this)
            {
                PluginUnload();
                Loaded = false;
            }
        }
        
        public abstract bool Enabled { get; set; }
        protected abstract void PluginLoad();
        protected abstract void PluginUnload();

        /// <inheritdoc cref="IPlugin.OnGenericEvent"/>
        public virtual void OnGenericEvent(EGenericEvent genericEvent)
        {
            // Do nothing
        }
    }
}