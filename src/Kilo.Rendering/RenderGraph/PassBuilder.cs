using System.Numerics;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class PassBuilder
{
    private readonly RenderGraph _graph;
    private readonly RenderPass _pass;

    internal PassBuilder(RenderGraph graph, RenderPass pass)
    {
        _graph = graph;
        _pass = pass;
    }

    public RenderResourceHandle CreateTexture(TextureDescriptor descriptor)
    {
        var handle = _graph.AllocateHandle(RenderResourceType.Texture, descriptor);
        _pass.CreatedResources.Add(handle);
        return handle;
    }

    public RenderResourceHandle CreateBuffer(BufferDescriptor descriptor)
    {
        var handle = _graph.AllocateHandle(RenderResourceType.Buffer, descriptor);
        _pass.CreatedResources.Add(handle);
        return handle;
    }

    public PassBuilder Read(RenderResourceHandle handle)
    {
        _pass.ReadResources.Add(handle);
        return this;
    }

    public PassBuilder Write(RenderResourceHandle handle)
    {
        _pass.WrittenResources.Add(handle);
        return this;
    }

    public PassBuilder ReadTexture(RenderResourceHandle handle) => Read(handle);
    public PassBuilder WriteTexture(RenderResourceHandle handle) => Write(handle);
    public PassBuilder ReadBuffer(RenderResourceHandle handle) => Read(handle);
    public PassBuilder WriteBuffer(RenderResourceHandle handle) => Write(handle);

    public PassBuilder SetViewport(float x, float y, float width, float height)
    {
        _pass.Viewport = new Vector4(x, y, width, height);
        return this;
    }

    public PassBuilder SetScissor(int x, int y, uint width, uint height)
    {
        _pass.Scissor = new Vector4Int(x, y, (int)width, (int)height);
        return this;
    }

    public PassBuilder ColorAttachment(RenderResourceHandle target, DriverLoadAction loadAction = DriverLoadAction.Clear, DriverStoreAction storeAction = DriverStoreAction.Store, Vector4? clearColor = null)
    {
        _pass.ColorAttachments.Add(new ColorAttachmentConfig
        {
            Target = target,
            LoadAction = loadAction,
            StoreAction = storeAction,
            ClearColor = clearColor,
        });
        return this;
    }

    public PassBuilder DepthStencilAttachment(RenderResourceHandle target, DriverLoadAction depthLoadAction = DriverLoadAction.Clear, DriverStoreAction depthStoreAction = DriverStoreAction.Store, float? clearDepth = null)
    {
        _pass.DepthStencilAttachment = new DepthStencilAttachmentConfig
        {
            Target = target,
            DepthLoadAction = depthLoadAction,
            DepthStoreAction = depthStoreAction,
            ClearDepth = clearDepth,
        };
        return this;
    }

    public RenderResourceHandle ImportTexture(string name, TextureDescriptor descriptor)
        => _graph.ImportResource(name, RenderResourceType.Texture, descriptor);

    public RenderResourceHandle ImportBuffer(string name, BufferDescriptor descriptor)
        => _graph.ImportResource(name, RenderResourceType.Buffer, descriptor);
}
