using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe partial class WebGPURenderDriver
{
    // --- Readback ---

    [DllImport("wgpu_native", CallingConvention = CallingConvention.Cdecl)]
    private static extern byte wgpuDevicePoll(Silk.NET.WebGPU.Device* device, byte wait, void* submissionIndex);

    private static readonly System.Threading.ManualResetEventSlim s_readbackDone = new(false);
    private static byte[]? s_readbackResult;
    private static string? s_readbackError;
    private static WgpuApi? s_cbWgpu;
    private static Silk.NET.WebGPU.Buffer* s_cbBuffer;
    private static nuint s_cbOffset;
    private static nuint s_cbSize;

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static void OnBufferMapped(BufferMapAsyncStatus status, void* message)
    {
        if (status == BufferMapAsyncStatus.Success)
        {
            var mapped = s_cbWgpu!.BufferGetMappedRange(s_cbBuffer, s_cbOffset, s_cbSize);
            s_readbackResult = new byte[(int)s_cbSize];
            Marshal.Copy((nint)mapped, s_readbackResult, 0, (int)s_cbSize);
            s_cbWgpu.BufferUnmap(s_cbBuffer);
        }
        else
        {
            s_readbackError = $"MapAsync failed with status {status}";
        }
        s_readbackDone.Set();
    }

    public byte[] ReadBufferSync(IBuffer buffer, nuint offset, nuint size)
    {
        var wgpuBuf = ((WebGPUBuffer)buffer).NativePtr;

        s_readbackDone.Reset();
        s_readbackResult = null;
        s_readbackError = null;
        s_cbWgpu = Wgpu;
        s_cbBuffer = wgpuBuf;
        s_cbOffset = offset;
        s_cbSize = size;

        delegate* unmanaged[Cdecl]<BufferMapAsyncStatus, void*, void> cb = &OnBufferMapped;
        Wgpu.BufferMapAsync(wgpuBuf, MapMode.Read, offset, size, new PfnBufferMapCallback(cb), null);

        // Use DevicePoll instead of InstanceProcessEvents to avoid wgpu-native
        // "not implemented" panic (see gfx-rs/wgpu-native issue #551)
        while (!s_readbackDone.Wait(0))
        {
            wgpuDevicePoll(Device, 1, null);
        }

        if (s_readbackError != null)
            throw new InvalidOperationException($"Buffer readback failed: {s_readbackError}");

        return s_readbackResult!;
    }
}
