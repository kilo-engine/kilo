using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class ComputePassBuilder
{
    private readonly PassBuilder _passBuilder;

    internal ComputePassBuilder(PassBuilder passBuilder)
    {
        _passBuilder = passBuilder;
    }

    public RenderResourceHandle CreateTexture(TextureDescriptor descriptor)
        => _passBuilder.CreateTexture(descriptor);

    public RenderResourceHandle CreateBuffer(BufferDescriptor descriptor)
        => _passBuilder.CreateBuffer(descriptor);

    public ComputePassBuilder ReadTexture(RenderResourceHandle handle)
    {
        _passBuilder.ReadTexture(handle);
        return this;
    }

    public ComputePassBuilder WriteTexture(RenderResourceHandle handle)
    {
        _passBuilder.WriteTexture(handle);
        return this;
    }

    public ComputePassBuilder ReadBuffer(RenderResourceHandle handle)
    {
        _passBuilder.ReadBuffer(handle);
        return this;
    }

    public ComputePassBuilder WriteBuffer(RenderResourceHandle handle)
    {
        _passBuilder.WriteBuffer(handle);
        return this;
    }

    public RenderResourceHandle ImportTexture(string name, TextureDescriptor descriptor)
        => _passBuilder.ImportTexture(name, descriptor);

    public RenderResourceHandle ImportBuffer(string name, BufferDescriptor descriptor)
        => _passBuilder.ImportBuffer(name, descriptor);
}
