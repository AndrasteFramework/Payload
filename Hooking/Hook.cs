using EasyHook;
using System;
using System.Runtime.InteropServices;

namespace Andraste.Payload.Hooking
{
    // Thanks to remcoros for the initial version of the following helper classes and spazzarama for this version taken from Direct3DHook
    // This has been adopted to more recent C# features by Andraste

    /// <summary>
    /// Extends <see cref="Hook"/> with support for accessing the Original method from within a hook delegate
    /// </summary>
    /// <typeparam name="T">A delegate type</typeparam>
    public class Hook<T> : Hook where T : Delegate
    {
        /// <summary>
        /// When called from within the <see cref="Hook.NewFunc"/> delegate this will call the original function at <see cref="Hook.FuncToHook"/>.
        /// </summary>
        public T Original { get; }

        /// <summary>
        /// Creates a new hook at <paramref name="funcToHook"/> redirecting to <paramref name="newFunc"/>. The hook starts inactive so a call to <see cref="Hook.Activate"/> is required to enable the hook.
        /// </summary>
        /// <param name="funcToHook">A pointer to the location to insert the hook</param>
        /// <param name="newFunc">The delegate to call from the hooked location</param>
        /// <param name="owner">The object to assign as the "callback" object within the <see cref="EasyHook.LocalHook"/> instance.</param>
        public Hook(IntPtr funcToHook, T newFunc, object owner)
            : base(funcToHook, newFunc, owner)
        {
            Original = (T)Marshal.GetDelegateForFunctionPointer(funcToHook, typeof(T));
        }

        /// <inheritdoc cref="Hook{T}(IntPtr, T, object)"/>
        public Hook(int funcToHook, T newFunc, object owner) : this(new IntPtr(funcToHook), newFunc, owner)
        {
        }
    }

    /// <summary>
    /// Wraps the <see cref="EasyHook.LocalHook"/> class with a simplified active/inactive state
    /// </summary>
    public class Hook : IDisposable
    {
        /// <summary>
        /// The hooked function location
        /// </summary>
        public IntPtr FuncToHook { get; }

        /// <summary>
        /// The replacement delegate
        /// </summary>
        public Delegate NewFunc { get; }

        /// <summary>
        /// The callback object passed to LocalHook constructor
        /// </summary>
        public object Owner { get; }

        /// <summary>
        /// The <see cref="EasyHook.LocalHook"/> instance
        /// </summary>
        public LocalHook LocalHook { get; private set; }

        /// <summary>
        /// Indicates whether the hook is currently active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Creates a new hook at <paramref name="funcToHook"/> redirecting to <paramref name="newFunc"/>. The hook starts inactive so a call to <see cref="Activate"/> is required to enable the hook.
        /// </summary>
        /// <param name="funcToHook">A pointer to the location to insert the hook</param>
        /// <param name="newFunc">The delegate to call from the hooked location</param>
        /// <param name="owner">The object to assign as the "callback" object within the <see cref="EasyHook.LocalHook"/> instance.</param>
        public Hook(IntPtr funcToHook, Delegate newFunc, object owner)
        {
            this.FuncToHook = funcToHook;
            this.NewFunc = newFunc;
            this.Owner = owner;

            CreateHook();
        }

        ~Hook()
        {
            Dispose(false);
        }

        protected void CreateHook()
        {
            if (LocalHook != null) return;

            this.LocalHook = LocalHook.Create(FuncToHook, NewFunc, Owner);
        }

        protected void UnHook()
        {
            if (this.IsActive)
                Deactivate();

            if (this.LocalHook != null)
            {
                this.LocalHook.Dispose();
                this.LocalHook = null;
            }
        }

        /// <summary>
        /// Activates the hook for every thread except the calling thread!
        /// </summary>
        public void Activate()
        {
            if (this.LocalHook == null)
                CreateHook();

            if (this.IsActive) return;

            this.IsActive = true;
            this.LocalHook.ThreadACL.SetExclusiveACL(new[] { 0 });
        }

        /// <summary>
        /// Deactivates the hook for the current thread
        /// </summary>
        public void Deactivate()
        {
            if (!this.IsActive) return;

            this.IsActive = false;
            this.LocalHook.ThreadACL.SetInclusiveACL(new[] { 0 });
        }


        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposeManagedObjects)
        {
            // Only clean up managed objects if disposing (i.e. not called from destructor)
            if (disposeManagedObjects)
            {
                UnHook();
            }
        }
    }
}