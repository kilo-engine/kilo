using System.Numerics;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class RenderPass
{
    public string Name { get; }
    internal readonly List<RenderResourceHandle> ReadResources = [];
    internal readonly List<RenderResourceHandle> WrittenResources = [];
    internal readonly List<RenderResourceHandle> CreatedResources = [];
    internal Vector4? Viewport;
    internal Vector4Int? Scissor;
    internal List<ColorAttachmentConfig> ColorAttachments = [];
    internal DepthStencilAttachmentConfig? DepthStencilAttachment;
    internal bool IsCompute;

    private readonly Action<PassBuilder> _setup;
    private readonly Action<RenderPassExecutionContext> _execute;

    internal RenderPass(string name, bool isCompute, Action<PassBuilder> setup, Action<RenderPassExecutionContext> execute)
    {
        Name = name;
        IsCompute = isCompute;
        _setup = setup;
        _execute = execute;
    }

    internal void RunSetup(PassBuilder builder) => _setup(builder);
    internal void RunExecute(RenderPassExecutionContext ctx) => _execute(ctx);
}

public sealed class ColorAttachmentConfig
{
    public RenderResourceHandle Target { get; init; }
    public DriverLoadAction LoadAction { get; init; } = DriverLoadAction.Clear;
    public DriverStoreAction StoreAction { get; init; } = DriverStoreAction.Store;
    public Vector4? ClearColor { get; init; }
}

public sealed class DepthStencilAttachmentConfig
{
    public RenderResourceHandle Target { get; init; }
    public DriverLoadAction DepthLoadAction { get; init; } = DriverLoadAction.Clear;
    public DriverStoreAction DepthStoreAction { get; init; } = DriverStoreAction.Store;
    public float? ClearDepth { get; init; } = 1.0f;
}

public readonly struct Vector4Int
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
    public readonly int W;

    public Vector4Int(int x, int y, int z, int w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }
}
