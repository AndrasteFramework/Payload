using System;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace Andraste.Payload.Hooking
{
    /// <summary>
    /// Utility class for calling methods and also providing calling convention
    /// translate code caves for method calls and hooks for non standard calling
    /// conventions (e.g. due to link-time optimization).<br />
    ///
    /// The name of this class is subject to change.
    /// </summary>
    public class Method32
    {
        /// <summary>
        /// Retrieve a delegate to call a native function at a given address
        /// </summary>
        /// <typeparam name="T">The function prototype (delegate)</typeparam>
        /// <param name="address">The address where the function resides</param>
        /// <returns>The callable delegate</returns>
        public static T FromPointer<T>(uint address) where T : Delegate
        {
            return Marshal.GetDelegateForFunctionPointer<T>(new IntPtr(address));
        }

        // Important note for these: stdcall "allows" that EAX, ECX and EDX can be used
        // within the function (i.e. polluted). Preferably use EAX, because that is
        // where the function will write it's result into

        /// <summary>
        /// Create assembly to convert a thiscall to a stdcall and jump to
        /// a given address.
        ///
        /// <b>Note:</b> thiscall is natively supported by P/Invoke, so there isn't much sense in this.
        /// </summary>
        /// <param name="target">The address of the stdcall target</param>
        /// <param name="rip">the instruction pointer (memory address where this will reside)</param>
        /// <returns>a memory stream containing the instructions</returns>
        public static MemoryStream FromThiscallToStdcallStatic(IntPtr target, IntPtr rip)
        {
            var a = new Assembler(32);
            // ret is on top of the stack, that needs to remain, but ecx needs to be in between.
            a.xchg(__[esp], ecx); // Now this is on top of the stack
            a.push(ecx); // Push what used to be ret
            a.jmp((ulong) target.ToInt32());

            var ms = new MemoryStream();
            a.Assemble(new StreamCodeWriter(ms), (ulong)rip.ToInt32());
            return ms;
        }

        /// <summary>
        /// Create assembly to convert a thiscall to a stdcall and jump to
        /// a dynamic address, which is expected to be the first (non this) parameter.
        ///
        /// <b>Note:</b> thiscall is natively supported by P/Invoke, so there isn't much sense in this.
        /// </summary>
        /// <param name="rip">the instruction pointer (memory address where this will reside)</param>
        /// <returns>a memory stream containing the instructions</returns>
        public static MemoryStream FromThiscallToStdcallDynamic(IntPtr rip)
        {
            // stack on entrance: ret, addr, bar, baz -> ret, this, bar, baz
            var a = new Assembler(32);
            a.xchg(__[esp + 4], ecx);
            a.jmp(ecx);

            var ms = new MemoryStream();
            a.Assemble(new StreamCodeWriter(ms), (ulong)rip.ToInt32());
            return ms;
        }

        /// <summary>
        /// Create assembly to convert a stdcall to a thiscall and jump to
        /// a given address.<br />
        /// <c>this</c> is expected to be the first parameter of the stdcall
        ///
        /// <b>Note:</b> thiscall is natively supported by P/Invoke, so there isn't much sense in this.
        /// </summary>
        /// <param name="target">The address of the thiscall target</param>
        /// <param name="rip">the instruction pointer (memory address where this will reside)</param>
        /// <returns>a memory stream containing the instructions</returns>
        public static MemoryStream FromStdcallToThiscallStatic(IntPtr target, IntPtr rip)
        {
            // easy way: pop eax, pop ecx, push eax

            var a = new Assembler(32);
            // We use XCHG here, so we don't have to pollute another register (e.g. EAX)
            a.pop(ecx); // Popping the _ret_ address
            // "this" is now the leftmost/_last_ (rtl) parameter on the stack,
            // which we want in ECX, while we want the value from ECX (ret addr)
            // to be the last on the stack.
            a.xchg(__[esp], ecx);
            a.jmp((ulong)target.ToInt32());

            var ms = new MemoryStream();
            a.Assemble(new StreamCodeWriter(ms), (ulong)rip.ToInt32());
            return ms;
        }

        /// <summary>
        /// Create assembly to convert a stdcall to a thiscall and jump to
        /// a dynamic address, which is expected to be the first parameter.<br />
        /// <c>this</c> is expected to be the second parameter of the stdcall
        ///
        /// <b>Note:</b> thiscall is natively supported by P/Invoke, so there isn't much sense in this.
        /// </summary>
        /// <param name="rip">the instruction pointer (memory address where this will reside)</param>
        /// <returns>a memory stream containing the instructions</returns>
        public static MemoryStream FromStdcallToThiscallDynamic(IntPtr rip)
        {
            // See the static case for the basic idea. We have three things on the stack:
            // ret, address, this -> address, this -> address, ret

            var a = new Assembler(32);
            a.pop(ecx);
            a.xchg(__[esp + 4], ecx); // swap our ret and _this_
            a.ret(); // ret to "address", which is now on top of the stack
            // afterwards, the real top of the stack will be _our ret_, so that retting 
            /*a.pop(ecx); // ret
            a.pop(eax); // dynamic address
            a.xchg(__[esp], ecx);
            a.jmp(__[esp + 4]);*/

            var ms = new MemoryStream();
            a.Assemble(new StreamCodeWriter(ms), (ulong) rip.ToInt32());
            return ms;
        }
    }
}
