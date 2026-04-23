namespace Kilo.Rendering.Driver;

public enum VertexFormat { Float32x2, Float32x3, Float32x4, UInt32, UInt32x4, Unorm8x4 }

public enum BlendOperation { Add, Subtract, ReverseSubtract, Min, Max }

public sealed class BlendComponentDescriptor
{
    public DriverBlendFactor SrcFactor { get; init; } = DriverBlendFactor.One;
    public DriverBlendFactor DstFactor { get; init; } = DriverBlendFactor.Zero;
    public BlendOperation Operation { get; init; } = BlendOperation.Add;
}

public sealed class BlendStateDescriptor
{
    public BlendComponentDescriptor Color { get; init; } = new();
    public BlendComponentDescriptor Alpha { get; init; } = new();
}

public sealed class ColorTargetDescriptor
{
    public DriverPixelFormat Format { get; init; }
    public BlendStateDescriptor? Blend { get; init; }
}

public sealed class VertexAttributeDescriptor
{
    public int ShaderLocation { get; init; }
    public VertexFormat Format { get; init; }
    public nuint Offset { get; init; }
}

public sealed class VertexBufferLayout
{
    public nuint ArrayStride { get; init; }
    public VertexAttributeDescriptor[] Attributes { get; init; } = [];
}

public sealed class DepthStencilStateDescriptor
{
    public DriverCompareFunction DepthCompare { get; init; } = DriverCompareFunction.Less;
    public bool DepthWriteEnabled { get; init; } = true;
    public DriverPixelFormat Format { get; init; } = DriverPixelFormat.Depth24Plus;
}

public sealed class RenderPipelineDescriptor
{
    public IShaderModule VertexShader { get; init; } = null!;
    public IShaderModule FragmentShader { get; init; } = null!;
    public DriverPrimitiveTopology Topology { get; init; } = DriverPrimitiveTopology.TriangleList;
    public ColorTargetDescriptor[] ColorTargets { get; init; } = [];
    public VertexBufferLayout[] VertexBuffers { get; init; } = [];
    public int SampleCount { get; init; } = 1;
    public DepthStencilStateDescriptor? DepthStencil { get; init; }
}
