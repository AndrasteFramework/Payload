using Andraste.Payload;
using Andraste.Shared.Lifecycle;

namespace Andraste.Shared.ModManagement
{
    public interface IPlugin : IManager
    {
        public EnabledMod ModInstance { get; }
        public EntryPoint EntryPoint { get;  }
        public void Bind(EnabledMod modInstance, EntryPoint entryPoint);
    }
}