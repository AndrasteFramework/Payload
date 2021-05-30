using System;

namespace Andraste.Payload.Hooking
{
    /// <summary>
    /// This interface will be implemented by the underlying modding framework
    /// to supply memory addresses (<see cref="IntPtr"/>) that can be used to
    /// Read/Write Values, Call Methods or Hook Methods.<br />
    ///<br />
    /// The Mod Framework will subclass this interface with another interface,
    /// typically containing IntPtr Properties, that denote the available offsets.<br />
    /// There will then be multiple implementations of that interface and depending
    /// on the game version/circumstance, the correct implementation should be chosen.<br />
    /// It is also perfectly valid to have multiple sub-interfaces, e.g. <c>MethodOffsets</c>
    /// or <c>AudioOffsets</c>.<br />
    ///<br />
    /// While it's framework implementation specific, we recommend that all members of
    /// the implementing class are IntPtr Properties, <br />that either perform a signature scan
    /// in general or at least validate their hardcoded pointers against signatures. <br />
    /// They should then throw an <see cref="InvalidOperationException"/>, if the signature
    /// cannot be found.<br />
    /// If the specific version of the target process did not yet / anymore have a specific
    /// value, then throw an <see cref="NotImplementedException"/>.<br />
    /// Doing so prevents the target application from randomly crashing (e.g. compared to
    /// returning <see cref="IntPtr.Zero"/>)
    /// <br />
    /// This interface and the other associated classes/interfaces
    /// may be moved into a different namespace (<c>Payload.Memory</c>) at some point
    /// </summary>
    public interface IMemoryOffsets
    {
        /// <summary>
        /// Whether this implementation is applicable for the current process.
        /// Use this to thoroughly check for signatures/pointers, that have
        /// changed between versions. <br />
        /// Failure to implement this check correctly may lead to the wrong
        /// implementation being chosen, simply crashing the application later.
        /// </summary>
        /// <returns>Whether this instance is applicable for the current target process</returns>
        public bool IsApplicable();
    }
}
