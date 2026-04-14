using System.Numerics;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Driver;

public interface IRenderDriver : IDisposable
{
    // Resource creation
    ITexture CreateTexture(TextureDescriptor descriptor);
    ITextureView CreateTextureView(ITexture texture, TextureViewDescriptor descriptor);
    ISampler CreateSampler(SamplerDescriptor descriptor);
    IBuffer CreateBuffer(BufferDescriptor descriptor);
    IShaderModule CreateShaderModule(string source, string entryPoint);
    IComputeShaderModule CreateComputeShaderModule(string source, string entryPoint);
    IRenderPipeline CreateRenderPipeline(RenderPipelineDescriptor descriptor);
    IComputePipeline CreateComputePipeline(IComputeShaderModule shader, string entryPoint);
    IBindingSet CreateBindingSet(BindingSetDescriptor descriptor);
    IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, UniformBufferBinding[] uniformBuffers);
    IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, TextureBinding[] textures, SamplerBinding[] samplers);
    IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, UniformBufferBinding[] uniformBuffers, TextureBinding[] textures, SamplerBinding[] samplers);
    IRenderPipeline CreateRenderPipelineWithDynamicUniforms(RenderPipelineDescriptor descriptor, nuint minBindingSize, int groupIndex = 0, int bindGroupCount = 1);
    IBindingSet CreateDynamicUniformBindingSet(IRenderPipeline pipeline, int groupIndex, IBuffer uniformBuffer, nuint bindingSize);
    IBindingSet CreateBindingSetForComputePipeline(IComputePipeline pipeline, int groupIndex, TextureBinding[] textures, StorageTextureBinding[] storageTextures);

    // Frame lifecycle
    ITexture GetCurrentSwapchainTexture();
    DriverPixelFormat SwapchainFormat { get; }
    void BeginFrame();
    IRenderCommandEncoder BeginCommandEncoding();
    void EndFrame();
    void Present();

    // Surface management
    void ConfigureSurface(int width, int height);
    void ResizeSurface(int width, int height);

    // Readback
    byte[] ReadBufferSync(IBuffer buffer, nuint offset, nuint size);
}
