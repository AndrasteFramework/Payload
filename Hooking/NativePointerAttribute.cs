using System;

namespace Andraste.Payload.Hooking
{
    /// <summary>
    /// An attribute to flag the required <see cref="Delegate"/> to hook
    /// or call the related function pointer.
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public class NativeMethodAttribute : Attribute
    {
        public Type DelegateType;

        public NativeMethodAttribute(Type delegateType)
        {
            DelegateType = delegateType;
        }
    }
}
