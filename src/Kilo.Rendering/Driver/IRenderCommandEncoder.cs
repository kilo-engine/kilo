using System.Numerics;

namespace Kilo.Rendering.Driver;

public interface IRenderCommandEncoder : IDisposable
{
    void BeginRenderPass(RenderPassDescriptor descriptor);
    void SetPipeline(IRenderPipeline pipeline);
    void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1);
    void SetScissor(int x, int y, uint width, uint height);
    void SetVertexBuffer(int slot, IBuffer buffer);
    void SetIndexBuffer(IBuffer buffer);
    void SetBindingSet(int group, IBindingSet bindingSet, uint dynamicOffset = 0);
    void DrawIndexed(int indexCount, int instanceCount = 1);
    void Draw(int vertexCount, int instanceCount = 1);
    void EndRenderPass();

    // Compute
    void BeginComputePass();
    void SetComputePipeline(IComputePipeline pipeline);
    void SetComputeBindingSet(int group, IBindingSet bindingSet, uint dynamicOffset = 0);
    void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ);
    void EndComputePass();

    // Copy commands
    void CopyBufferToBuffer(IBuffer src, nuint srcOffset, IBuffer dst, nuint dstOffset, nuint size);
    void CopyBufferToTexture(IBuffer src, nuint srcOffset, ITexture dst, TextureCopyRegion region);
    void CopyTextureToBuffer(ITexture src, TextureCopyRegion region, IBuffer dst, nuint dstOffset);

    void Submit();
}
