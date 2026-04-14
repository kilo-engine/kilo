using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe class WebGPUCommandEncoder : IRenderCommandEncoder
{
    private readonly WgpuApi _wgpu;
    private readonly Device* _device;
    private CommandEncoder* _encoder;
    private RenderPassEncoder* _renderPass;
    private ComputePassEncoder* _computePass;
    private TextureView* _legacyColorView;
    private bool _inRenderPass;
    private bool _inComputePass;
    private bool _disposed;

    internal WebGPUCommandEncoder(WgpuApi wgpu, Device* device, CommandEncoder* encoder)
    {
        _wgpu = wgpu;
        _device = device;
        _encoder = encoder;
    }

    // Legacy overload for backward compatibility
    public void BeginRenderPass(ITexture colorTarget, DriverLoadAction loadAction, DriverStoreAction storeAction, in Vector4 clearColor)
    {
        EndAnyPass();

        var wgpuTexture = (WebGPUTexture)colorTarget;
        _legacyColorView = _wgpu.TextureCreateView(wgpuTexture.NativePtr, null);

        var colorAttachment = new RenderPassColorAttachment
        {
            View = _legacyColorView,
            ResolveTarget = null,
            LoadOp = MapLoadOp(loadAction),
            StoreOp = MapStoreOp(storeAction),
            ClearValue = new Color { R = clearColor.X, G = clearColor.Y, B = clearColor.Z, A = clearColor.W }
        };

        var desc = new Silk.NET.WebGPU.RenderPassDescriptor
        {
            ColorAttachments = &colorAttachment,
            ColorAttachmentCount = 1,
            DepthStencilAttachment = null
        };

        _renderPass = _wgpu.CommandEncoderBeginRenderPass(_encoder, in desc);
        _inRenderPass = true;
    }

    public void BeginRenderPass(Kilo.Rendering.Driver.RenderPassDescriptor descriptor)
    {
        EndAnyPass();

        RenderPassColorAttachment* colorAttachments = null;
        int colorCount = descriptor.ColorAttachments.Length;
        if (colorCount > 0)
        {
            colorAttachments = (RenderPassColorAttachment*)NativeMemory.Alloc(
                (nuint)(colorCount * sizeof(RenderPassColorAttachment)));
            for (int i = 0; i < colorCount; i++)
            {
                var ca = descriptor.ColorAttachments[i];
                var view = ((WebGPUTextureView)ca.RenderTarget).NativePtr;
                colorAttachments[i] = new RenderPassColorAttachment
                {
                    View = view,
                    ResolveTarget = null,
                    LoadOp = MapLoadOp(ca.LoadAction),
                    StoreOp = MapStoreOp(ca.StoreAction),
                };
                if (ca.ClearColor.HasValue)
                {
                    var cc = ca.ClearColor.Value;
                    colorAttachments[i].ClearValue = new Color { R = cc.X, G = cc.Y, B = cc.Z, A = cc.W };
                }
            }
        }

        RenderPassDepthStencilAttachment* depthAttachment = null;
        if (descriptor.DepthStencilAttachment != null)
        {
            var ds = descriptor.DepthStencilAttachment;
            depthAttachment = (RenderPassDepthStencilAttachment*)NativeMemory.Alloc(
                (nuint)sizeof(RenderPassDepthStencilAttachment));
            *depthAttachment = new RenderPassDepthStencilAttachment
            {
                View = ((WebGPUTextureView)ds.View).NativePtr,
                DepthLoadOp = MapLoadOp(ds.DepthLoadAction),
                DepthStoreOp = MapStoreOp(ds.DepthStoreAction),
                DepthReadOnly = false,
            };
            if (ds.ClearDepth.HasValue)
            {
                depthAttachment->DepthClearValue = ds.ClearDepth.Value;
            }
        }

        var wgpuDesc = new Silk.NET.WebGPU.RenderPassDescriptor
        {
            ColorAttachments = colorAttachments,
            ColorAttachmentCount = (uint)colorCount,
            DepthStencilAttachment = depthAttachment,
        };

        _renderPass = _wgpu.CommandEncoderBeginRenderPass(_encoder, in wgpuDesc);
        _inRenderPass = true;

        if (colorAttachments != null)
            NativeMemory.Free(colorAttachments);
        if (depthAttachment != null)
            NativeMemory.Free(depthAttachment);
    }

    public void SetPipeline(IRenderPipeline pipeline)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderSetPipeline(_renderPass, ((WebGPUPipeline)pipeline).NativePtr);
    }

    public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderSetViewport(_renderPass, x, y, width, height, minDepth, maxDepth);
    }

    public void SetScissor(int x, int y, uint width, uint height)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderSetScissorRect(_renderPass, (uint)x, (uint)y, width, height);
    }

    public void SetVertexBuffer(int slot, IBuffer buffer)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderSetVertexBuffer(_renderPass, (uint)slot, ((WebGPUBuffer)buffer).NativePtr, 0, ulong.MaxValue);
    }

    public void SetIndexBuffer(IBuffer buffer)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderSetIndexBuffer(_renderPass, ((WebGPUBuffer)buffer).NativePtr, IndexFormat.Uint32, 0, ulong.MaxValue);
    }

    public void SetBindingSet(int group, IBindingSet bindingSet, uint dynamicOffset)
    {
        EnsureInRenderPass();
        var wgpuBs = (WebGPUBindingSet)bindingSet;
        uint offset = dynamicOffset;
        uint count = wgpuBs.HasDynamicOffsets ? 1u : 0u;
        _wgpu.RenderPassEncoderSetBindGroup(_renderPass, (uint)group, wgpuBs.NativePtr, count, count > 0 ? &offset : null);
    }

    public void DrawIndexed(int indexCount, int instanceCount)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderDrawIndexed(_renderPass, (uint)indexCount, (uint)instanceCount, 0, 0, 0);
    }

    public void Draw(int vertexCount, int instanceCount)
    {
        EnsureInRenderPass();
        _wgpu.RenderPassEncoderDraw(_renderPass, (uint)vertexCount, (uint)instanceCount, 0, 0);
    }

    public void EndRenderPass()
    {
        if (!_inRenderPass) return;
        _wgpu.RenderPassEncoderEnd(_renderPass);
        _wgpu.RenderPassEncoderRelease(_renderPass);
        _renderPass = null;
        _inRenderPass = false;

        if (_legacyColorView != null)
        {
            _wgpu.TextureViewRelease(_legacyColorView);
            _legacyColorView = null;
        }
    }

    // Compute
    public void BeginComputePass()
    {
        EndAnyPass();
        _computePass = _wgpu.CommandEncoderBeginComputePass(_encoder, new ComputePassDescriptor());
        _inComputePass = true;
    }

    public void SetComputePipeline(IComputePipeline pipeline)
    {
        EnsureInComputePass();
        _wgpu.ComputePassEncoderSetPipeline(_computePass, ((WebGPUComputePipeline)pipeline).NativePtr);
    }

    public void SetComputeBindingSet(int group, IBindingSet bindingSet, uint dynamicOffset)
    {
        EnsureInComputePass();
        var wgpuBs = (WebGPUBindingSet)bindingSet;
        uint offset = dynamicOffset;
        uint count = wgpuBs.HasDynamicOffsets ? 1u : 0u;
        _wgpu.ComputePassEncoderSetBindGroup(_computePass, (uint)group, wgpuBs.NativePtr, count, count > 0 ? &offset : null);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        EnsureInComputePass();
        _wgpu.ComputePassEncoderDispatchWorkgroups(_computePass, groupCountX, groupCountY, groupCountZ);
    }

    public void EndComputePass()
    {
        if (!_inComputePass) return;
        _wgpu.ComputePassEncoderEnd(_computePass);
        _wgpu.ComputePassEncoderRelease(_computePass);
        _computePass = null;
        _inComputePass = false;
    }

    // Copy commands
    public void CopyBufferToBuffer(IBuffer src, nuint srcOffset, IBuffer dst, nuint dstOffset, nuint size)
    {
        EndAnyPass();
        var srcBuf = ((WebGPUBuffer)src).NativePtr;
        var dstBuf = ((WebGPUBuffer)dst).NativePtr;
        _wgpu.CommandEncoderCopyBufferToBuffer(_encoder, srcBuf, srcOffset, dstBuf, dstOffset, size);
    }

    public void CopyBufferToTexture(IBuffer src, nuint srcOffset, ITexture dst, TextureCopyRegion region)
    {
        EndAnyPass();
        var srcBuf = ((WebGPUBuffer)src).NativePtr;
        var dstTex = ((WebGPUTexture)dst).NativePtr;

        var source = new ImageCopyBuffer
        {
            Layout = new TextureDataLayout { Offset = srcOffset, BytesPerRow = (uint)(region.Width * 4), RowsPerImage = (uint)region.Height },
            Buffer = srcBuf,
        };
        var destination = new ImageCopyTexture
        {
            Texture = dstTex,
            Aspect = TextureAspect.All,
        };
        var extent = new Extent3D
        {
            Width = (uint)region.Width,
            Height = (uint)region.Height,
            DepthOrArrayLayers = (uint)region.DepthOrArrayLayers,
        };

        _wgpu.CommandEncoderCopyBufferToTexture(_encoder, in source, in destination, in extent);
    }

    public void CopyTextureToBuffer(ITexture src, TextureCopyRegion region, IBuffer dst, nuint dstOffset)
    {
        EndAnyPass();
        var srcTex = ((WebGPUTexture)src).NativePtr;
        var dstBuf = ((WebGPUBuffer)dst).NativePtr;

        // WebGPU requires bytesPerRow to be a multiple of 256
        uint bytesPerRow = (uint)(((region.Width * 4) + 255) & ~255);

        var source = new ImageCopyTexture
        {
            Texture = srcTex,
            Aspect = TextureAspect.All,
        };
        var destination = new ImageCopyBuffer
        {
            Layout = new TextureDataLayout { Offset = dstOffset, BytesPerRow = bytesPerRow, RowsPerImage = (uint)region.Height },
            Buffer = dstBuf,
        };
        var extent = new Extent3D
        {
            Width = (uint)region.Width,
            Height = (uint)region.Height,
            DepthOrArrayLayers = (uint)region.DepthOrArrayLayers,
        };

        _wgpu.CommandEncoderCopyTextureToBuffer(_encoder, in source, in destination, in extent);
    }

    public void Submit()
    {
        EndAnyPass();

        var cmdBuffer = _wgpu.CommandEncoderFinish(_encoder, new CommandBufferDescriptor());
        var queue = _wgpu.DeviceGetQueue(_device);
        _wgpu.QueueSubmit(queue, 1, &cmdBuffer);

        _wgpu.CommandBufferRelease(cmdBuffer);
        _wgpu.CommandEncoderRelease(_encoder);
        _encoder = null;
    }

    private void EndAnyPass()
    {
        EndRenderPass();
        EndComputePass();
    }

    private void EnsureInRenderPass()
    {
        if (!_inRenderPass) throw new InvalidOperationException("Not in a render pass.");
    }

    private void EnsureInComputePass()
    {
        if (!_inComputePass) throw new InvalidOperationException("Not in a compute pass.");
    }

    private static LoadOp MapLoadOp(DriverLoadAction action) => action switch
    {
        DriverLoadAction.Clear => LoadOp.Clear,
        DriverLoadAction.Load => LoadOp.Load,
        _ => LoadOp.Clear,
    };

    private static StoreOp MapStoreOp(DriverStoreAction action) => action switch
    {
        DriverStoreAction.Store => StoreOp.Store,
        _ => StoreOp.Discard,
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EndAnyPass();
        if (_encoder != null)
        {
            _wgpu.CommandEncoderRelease(_encoder);
            _encoder = null;
        }
    }
}
