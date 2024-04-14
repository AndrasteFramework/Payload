using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using SharpDX.Direct3D9;

namespace Andraste.Payload.D3D9
{
    /// <summary>
    /// The full list of IDirect3DDevice9 functions with the correct index
    /// </summary>
    public enum Direct3DDevice9FunctionOrdinals : short
    {
        QueryInterface = 0,
        AddRef = 1,
        Release = 2,
        TestCooperativeLevel = 3,
        GetAvailableTextureMem = 4,
        EvictManagedResources = 5,
        GetDirect3D = 6,
        GetDeviceCaps = 7,
        GetDisplayMode = 8,
        GetCreationParameters = 9,
        SetCursorProperties = 10,
        SetCursorPosition = 11,
        ShowCursor = 12,
        CreateAdditionalSwapChain = 13,
        GetSwapChain = 14,
        GetNumberOfSwapChains = 15,
        Reset = 16,
        Present = 17,
        GetBackBuffer = 18,
        GetRasterStatus = 19,
        SetDialogBoxMode = 20,
        SetGammaRamp = 21,
        GetGammaRamp = 22,
        CreateTexture = 23,
        CreateVolumeTexture = 24,
        CreateCubeTexture = 25,
        CreateVertexBuffer = 26,
        CreateIndexBuffer = 27,
        CreateRenderTarget = 28,
        CreateDepthStencilSurface = 29,
        UpdateSurface = 30,
        UpdateTexture = 31,
        GetRenderTargetData = 32,
        GetFrontBufferData = 33,
        StretchRect = 34,
        ColorFill = 35,
        CreateOffscreenPlainSurface = 36,
        SetRenderTarget = 37,
        GetRenderTarget = 38,
        SetDepthStencilSurface = 39,
        GetDepthStencilSurface = 40,
        BeginScene = 41,
        EndScene = 42,
        Clear = 43,
        SetTransform = 44,
        GetTransform = 45,
        MultiplyTransform = 46,
        SetViewport = 47,
        GetViewport = 48,
        SetMaterial = 49,
        GetMaterial = 50,
        SetLight = 51,
        GetLight = 52,
        LightEnable = 53,
        GetLightEnable = 54,
        SetClipPlane = 55,
        GetClipPlane = 56,
        SetRenderState = 57,
        GetRenderState = 58,
        CreateStateBlock = 59,
        BeginStateBlock = 60,
        EndStateBlock = 61,
        SetClipStatus = 62,
        GetClipStatus = 63,
        GetTexture = 64,
        SetTexture = 65,
        GetTextureStageState = 66,
        SetTextureStageState = 67,
        GetSamplerState = 68,
        SetSamplerState = 69,
        ValidateDevice = 70,
        SetPaletteEntries = 71,
        GetPaletteEntries = 72,
        SetCurrentTexturePalette = 73,
        GetCurrentTexturePalette = 74,
        SetScissorRect = 75,
        GetScissorRect = 76,
        SetSoftwareVertexProcessing = 77,
        GetSoftwareVertexProcessing = 78,
        SetNPatchMode = 79,
        GetNPatchMode = 80,
        DrawPrimitive = 81,
        DrawIndexedPrimitive = 82,
        DrawPrimitiveUP = 83,
        DrawIndexedPrimitiveUP = 84,
        ProcessVertices = 85,
        CreateVertexDeclaration = 86,
        SetVertexDeclaration = 87,
        GetVertexDeclaration = 88,
        SetFVF = 89,
        GetFVF = 90,
        CreateVertexShader = 91,
        SetVertexShader = 92,
        GetVertexShader = 93,
        SetVertexShaderConstantF = 94,
        GetVertexShaderConstantF = 95,
        SetVertexShaderConstantI = 96,
        GetVertexShaderConstantI = 97,
        SetVertexShaderConstantB = 98,
        GetVertexShaderConstantB = 99,
        SetStreamSource = 100,
        GetStreamSource = 101,
        SetStreamSourceFreq = 102,
        GetStreamSourceFreq = 103,
        SetIndices = 104,
        GetIndices = 105,
        CreatePixelShader = 106,
        SetPixelShader = 107,
        GetPixelShader = 108,
        SetPixelShaderConstantF = 109,
        GetPixelShaderConstantF = 110,
        SetPixelShaderConstantI = 111,
        GetPixelShaderConstantI = 112,
        SetPixelShaderConstantB = 113,
        GetPixelShaderConstantB = 114,
        DrawRectPatch = 115,
        DrawTriPatch = 116,
        DeletePatch = 117,
        CreateQuery = 118,
    }

    public enum Direct3DDevice9ExFunctionOrdinals : short
    {
        SetConvolutionMonoKernel = 119,
        ComposeRects = 120,
        PresentEx = 121,
        GetGPUThreadPriority = 122,
        SetGPUThreadPriority = 123,
        WaitForVBlank = 124,
        CheckResourceResidency = 125,
        SetMaximumFrameLatency = 126,
        GetMaximumFrameLatency = 127,
        CheckDeviceState_ = 128,
        CreateRenderTargetEx = 129,
        CreateOffscreenPlainSurfaceEx = 130,
        CreateDepthStencilSurfaceEx = 131,
        ResetEx = 132,
        GetDisplayModeEx = 133,
    }

    public enum IDirect3DVertexBuffer9FunctionOrdinals : short
    {
        QueryInterface = 0,
        AddRef = 1,
        Release = 2,
        GetDevice = 3,
        SetPrivateData = 4,
        GetPrivateData = 5,
        FreePrivateData = 6,
        SetPriority = 7,
        GetPriority = 8,
        PreLoad = 9,
        GetType = 10,
        Lock = 11,
        Unlock = 12, 
        GetDesc = 13,
    }
    
    // Calling convention: DX9 methods are COM (combaseapi.h) and as such, since C-abi has no __thiscall, they are
    // strictly stdcall and by-convention push "this" on the stack.

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3D9Device_BeginSceneDelegate(IntPtr device);

    /// <summary>
    /// The IDirect3DDevice9.EndScene function definition
    /// </summary>
    /// <param name="device"></param>
    /// <returns></returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3D9Device_EndSceneDelegate(IntPtr device);

    /// <summary>
    /// The IDirect3DDevice9.Reset function definition
    /// </summary>
    /// <param name="device"></param>
    /// <param name="presentParameters"></param>
    /// <returns></returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3D9Device_ResetDelegate(IntPtr device, ref PresentParameters presentParameters);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public unsafe delegate int Direct3D9Device_PresentDelegate(IntPtr devicePtr, Rectangle* pSourceRect,
        Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public unsafe delegate int Direct3D9DeviceEx_PresentExDelegate(IntPtr devicePtr, Rectangle* pSourceRect,
        Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, Present dwFlags);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public unsafe delegate int Direct3D9Device_CreateShader(IntPtr devicePtr, IntPtr pFunction, IntPtr ppShader);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate int Direct3D9Device_CreateTexture(IntPtr devicePtr, uint width, uint height, uint levels, uint usage, 
        uint format, uint pool, IntPtr *ppTexture, IntPtr pSharedHandle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3D9Device_SetShader(IntPtr devicePtr, IntPtr pShader);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3D9Device_DrawIndexedPrimitive(IntPtr devicePtr, uint primitiveType, int baseVertexIndex, 
        uint minVertexIndex, uint numVertices, uint startIndex, uint primCount);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3DVertexBuffer9_Lock(IntPtr pBuffer, uint offsetToLock, uint sizeToLock, IntPtr ppbData, int flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3DVertexBuffer9_Unlock(IntPtr pBuffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    public delegate int Direct3D9Device_SetIndices(IntPtr devicePtr, IntPtr pIndexBuffer);

    public static class Functions
    {
        public const int D3D9_DEVICE_METHOD_COUNT = 119;
        public const int D3D9Ex_DEVICE_METHOD_COUNT = 15;
        public const int D3D9_METHOD_COUNT = 17;
        public const int D3D9_VERTEX_BUFFER_METHOD_COUNT = 14;

        public static IntPtr[] GetVTblAddresses(IntPtr pointer, int numberOfMethods)
        {
            return GetVTblAddresses(pointer, 0, numberOfMethods);
        }

        public static IntPtr[] GetVTblAddresses(IntPtr pointer, int startIndex, int numberOfMethods)
        {
            var vtblAddresses = new List<IntPtr>();
            var vTable = Marshal.ReadIntPtr(pointer);
            for (int i = startIndex; i < startIndex + numberOfMethods; i++)
                // using IntPtr.Size allows us to support both 32 and 64-bit processes
                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size));

            return vtblAddresses.ToArray();
        }
    }
}
