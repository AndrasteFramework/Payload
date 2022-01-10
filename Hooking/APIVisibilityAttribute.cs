using System;

namespace Andraste.Payload.Hooking
{
#nullable enable
    /// <summary>
    /// An attribute to document how the tagged structure is intended to be used
    /// and how likely API breakages are.<br />
    /// It is primarily targeted at <see cref="IMemoryOffsets"/> props.
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public class ApiVisibilityAttribute : Attribute
    {
        public EVisibility Visibility;
        public string? Reasoning;

        public enum EVisibility
        {
            PublicAPI,
            SemiPublicAPI,
            /// <summary>
            /// Only to be consumed by the Mod Framework extending Andraste,
            /// not by the individual mods.
            /// </summary>
            ModFrameworkInternalAPI,
            InternalAPI
        }
    }
#nullable restore
}
