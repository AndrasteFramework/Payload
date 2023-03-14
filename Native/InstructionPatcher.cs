using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Andraste.Payload.Hooking;

namespace Andraste.Payload.Native
{
    [ApiVisibility(Visibility = ApiVisibilityAttribute.EVisibility.PublicAPI)]
    public class InstructionPatcher
    {
        private bool _didApplyPatch;
        private readonly byte[] _expectation;
        private readonly byte[] _patchContent;
        private readonly IntPtr _address;
        private readonly IntPtr _size;

        public IntPtr Address => _address;
        
        public InstructionPatcher(IntPtr address, byte[] expectation, byte[] patchContent)
        {
            _expectation = expectation;
            _patchContent = patchContent;
            _address = address;

            if (_expectation.Length != _patchContent.Length)
            {
                throw new ArgumentException("Expectation and Patch don't have a matching length");
            }

            _size = new IntPtr(_expectation.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Patch(bool apply)
        {
            Kernel32.VirtualProtect(_address, _size, 0x40, out var old);
            if (apply)
            {
                WritePatch();
                _didApplyPatch = true;
            }
            else
            {
                WriteExpectation();
                _didApplyPatch = false;
            }

            Kernel32.VirtualProtect(_address, _size, old, out var _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WritePatch()
        {
            Marshal.Copy(_patchContent, 0, _address, _patchContent.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteExpectation()
        {
            Marshal.Copy(_expectation, 0, _address, _expectation.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBeenPatched()
        {
            return _didApplyPatch;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesExpectation()
        {
            var actual = new byte[_expectation.Length];
            Marshal.Copy(_address, actual, 0, actual.Length);
            return _expectation.SequenceEqual(actual);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesPatch()
        {
            var actual = new byte[_patchContent.Length];
            Marshal.Copy(_address, actual, 0, actual.Length);
            return _patchContent.SequenceEqual(actual);
        }
    }
}
