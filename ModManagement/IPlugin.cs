using Andraste.Shared.Lifecycle;
using Andraste.Shared.ModManagement;

namespace Andraste.Payload.ModManagement
{
    public interface IPlugin : IManager
    {
        public EnabledMod ModInstance { get; }
        public EntryPoint EntryPoint { get;  }
        public void Bind(EnabledMod modInstance, EntryPoint entryPoint);
        
        /// <summary>
        /// This is called, whenever Andraste has emitted a generic event.
        /// For more details about existing events and the API design choice behind them,
        /// read the enum's documentation
        /// <see cref="EGenericEvent"/>
        /// </summary>
        /// <param name="genericEvent">The event that has happened</param>
        public void OnGenericEvent(EGenericEvent genericEvent);
    }
}