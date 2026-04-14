using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Tests;

public sealed class MockTexture : ITexture
{
    public int Width { get; init; }
    public int Height { get; init; }
    public DriverPixelFormat Format { get; init; }
    public void Dispose() { }
    public void UploadData<T>(ReadOnlySpan<T> data) where T : unmanaged { }
}

public sealed class MockTextureView : ITextureView
{
    public void Dispose() { }
}

public sealed class MockSampler : ISampler
{
    public void Dispose() { }
}

public sealed class MockBuffer : IBuffer
{
    public nuint Size { get; init; }
    public void UploadData<T>(ReadOnlySpan<T> data, nuint offset = 0) where T : unmanaged { }
    public void Dispose() { }
}

public sealed class MockShaderModule : IShaderModule
{
    public string EntryPoint { get; init; } = "";
    public void Dispose() { }
}

public sealed class MockComputeShaderModule : IComputeShaderModule
{
    public string EntryPoint { get; init; } = "";
    public void Dispose() { }
}

public sealed class MockRenderPipeline : IRenderPipeline
{
    public void Dispose() { }
}

public sealed class MockComputePipeline : IComputePipeline
{
    public void Dispose() { }
}

public sealed class MockBindingSet : IBindingSet
{
    public BindingSetDescriptor? LastDescriptor { get; init; }
    public void Dispose() { }
}

public sealed class MockRenderCommandEncoder : IRenderCommandEncoder
{
    public bool InRenderPass { get; private set; }
    public bool InComputePass { get; private set; }
    public RenderPassDescriptor? LastRenderPassDescriptor { get; private set; }
    public int DrawIndexedCallCount { get; private set; }
    public List<string> ComputeCalls { get; } = [];

    public void BeginRenderPass(ITexture colorTarget, DriverLoadAction loadAction, DriverStoreAction storeAction, in Vector4 clearColor)
    {
        InRenderPass = true;
    }

    public void BeginRenderPass(RenderPassDescriptor descriptor)
    {
        LastRenderPassDescriptor = descriptor;
        InRenderPass = true;
    }

    public void SetPipeline(IRenderPipeline pipeline) { }
    public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth) { }
    public void SetScissor(int x, int y, uint width, uint height) { }
    public void SetVertexBuffer(int slot, IBuffer buffer) { }
    public void SetIndexBuffer(IBuffer buffer) { }
    public void SetBindingSet(int group, IBindingSet bindingSet, uint dynamicOffset) { }
    public void DrawIndexed(int indexCount, int instanceCount) { DrawIndexedCallCount++; }
    public void Draw(int vertexCount, int instanceCount) { }
    public void EndRenderPass()
    {
        InRenderPass = false;
    }

    public void BeginComputePass()
    {
        ComputeCalls.Add("BeginComputePass");
        InComputePass = true;
    }

    public void SetComputePipeline(IComputePipeline pipeline)
    {
        ComputeCalls.Add("SetComputePipeline");
    }

    public void SetComputeBindingSet(int group, IBindingSet bindingSet, uint dynamicOffset)
    {
        ComputeCalls.Add("SetComputeBindingSet");
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        ComputeCalls.Add("Dispatch");
    }

    public void EndComputePass()
    {
        ComputeCalls.Add("EndComputePass");
        InComputePass = false;
    }

    public void CopyBufferToBuffer(IBuffer src, nuint srcOffset, IBuffer dst, nuint dstOffset, nuint size) { }
    public void CopyBufferToTexture(IBuffer src, nuint srcOffset, ITexture dst, TextureCopyRegion region) { }
    public void CopyTextureToBuffer(ITexture src, TextureCopyRegion region, IBuffer dst, nuint dstOffset) { }
    public void Submit() { }
    public void Dispose() { }
}

public sealed class MockRenderDriver : IRenderDriver
{
    public int CreateTextureCallCount { get; private set; }
    public int CreateSamplerCallCount { get; private set; }
    public int CreateBindingSetCallCount { get; private set; }
    public int CreateComputeShaderModuleCallCount { get; private set; }
    public int CreateComputePipelineCallCount { get; private set; }
    public int CompileCallCount { get; private set; }
    public MockRenderCommandEncoder? LastEncoder { get; private set; }

    public ITexture CreateTexture(TextureDescriptor descriptor)
    {
        CreateTextureCallCount++;
        return new MockTexture { Width = descriptor.Width, Height = descriptor.Height, Format = descriptor.Format };
    }

    public ITextureView CreateTextureView(ITexture texture, TextureViewDescriptor descriptor)
    {
        return new MockTextureView();
    }

    public ISampler CreateSampler(SamplerDescriptor descriptor)
    {
        CreateSamplerCallCount++;
        return new MockSampler();
    }

    public IBuffer CreateBuffer(BufferDescriptor descriptor)
    {
        return new MockBuffer { Size = descriptor.Size };
    }

    public IShaderModule CreateShaderModule(string source, string entryPoint)
    {
        return new MockShaderModule { EntryPoint = entryPoint };
    }

    public IComputeShaderModule CreateComputeShaderModule(string source, string entryPoint)
    {
        CreateComputeShaderModuleCallCount++;
        return new MockComputeShaderModule { EntryPoint = entryPoint };
    }

    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescriptor descriptor)
    {
        return new MockRenderPipeline();
    }

    public IComputePipeline CreateComputePipeline(IComputeShaderModule shader, string entryPoint)
    {
        CreateComputePipelineCallCount++;
        return new MockComputePipeline();
    }

    public IBindingSet CreateBindingSet(BindingSetDescriptor descriptor)
    {
        CreateBindingSetCallCount++;
        return new MockBindingSet { LastDescriptor = descriptor };
    }

    public IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, UniformBufferBinding[] uniformBuffers)
    {
        return new MockBindingSet();
    }

    public IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, TextureBinding[] textures, SamplerBinding[] samplers)
    {
        return new MockBindingSet();
    }

    public IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, UniformBufferBinding[] uniformBuffers, TextureBinding[] textures, SamplerBinding[] samplers)
    {
        return new MockBindingSet();
    }

    public IRenderPipeline CreateRenderPipelineWithDynamicUniforms(RenderPipelineDescriptor descriptor, nuint minBindingSize, int groupIndex = 0, int bindGroupCount = 1)
    {
        return new MockRenderPipeline();
    }

    public IBindingSet CreateDynamicUniformBindingSet(IRenderPipeline pipeline, int groupIndex, IBuffer uniformBuffer, nuint bindingSize)
    {
        return new MockBindingSet();
    }

    public IBindingSet CreateBindingSetForComputePipeline(IComputePipeline pipeline, int groupIndex, TextureBinding[] textures, StorageTextureBinding[] storageTextures)
    {
        return new MockBindingSet();
    }

    public DriverPixelFormat SwapchainFormat => DriverPixelFormat.BGRA8Unorm;

    public ITexture GetCurrentSwapchainTexture()
    {
        return new MockTexture { Width = 1280, Height = 720, Format = DriverPixelFormat.BGRA8Unorm };
    }

    public void BeginFrame() { }
    public IRenderCommandEncoder BeginCommandEncoding()
    {
        LastEncoder = new MockRenderCommandEncoder();
        return LastEncoder;
    }
    public void EndFrame() { }
    public void Present() { }
    public void ConfigureSurface(int width, int height) { }
    public void ResizeSurface(int width, int height) { }
    public byte[] ReadBufferSync(IBuffer buffer, nuint offset, nuint size) => new byte[size];
    public void Dispose() { }
}
