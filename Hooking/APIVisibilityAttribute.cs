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
    public class APIVisibilityAttribute : Attribute
    {
        public EVisibility Visibility;
        public string? Reasoning;

        public enum EVisibility
        {
            PublicAPI,
            SemiPublicAPI,
            InternalAPI
        }
    }
#nullable restore
}
