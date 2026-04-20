using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using RgTextureUsage = Kilo.Rendering.RenderGraph.TextureUsage;
using RgBufferUsage = Kilo.Rendering.RenderGraph.BufferUsage;

internal static class WebGPUMappings
{
    internal static TextureFormat MapPixelFormat(DriverPixelFormat format) => format switch
    {
        DriverPixelFormat.BGRA8Unorm => TextureFormat.Bgra8Unorm,
        DriverPixelFormat.BGRA8UnormSrgb => TextureFormat.Bgra8UnormSrgb,
        DriverPixelFormat.RGBA8Unorm => TextureFormat.Rgba8Unorm,
        DriverPixelFormat.Depth24Plus => TextureFormat.Depth24Plus,
        DriverPixelFormat.Depth24PlusStencil8 => TextureFormat.Depth24PlusStencil8,
        DriverPixelFormat.Depth32Float => TextureFormat.Depth24Plus,
        DriverPixelFormat.RGBA16Float => TextureFormat.Rgba16float,
        _ => TextureFormat.Bgra8Unorm,
    };

    internal static Silk.NET.WebGPU.TextureViewDimension MapTextureViewDimension(Kilo.Rendering.Driver.TextureViewDimension dimension) => dimension switch
    {
        Kilo.Rendering.Driver.TextureViewDimension.View1D => Silk.NET.WebGPU.TextureViewDimension.Dimension1D,
        Kilo.Rendering.Driver.TextureViewDimension.View2D => Silk.NET.WebGPU.TextureViewDimension.Dimension2D,
        Kilo.Rendering.Driver.TextureViewDimension.View2DArray => Silk.NET.WebGPU.TextureViewDimension.Dimension2DArray,
        Kilo.Rendering.Driver.TextureViewDimension.View3D => Silk.NET.WebGPU.TextureViewDimension.Dimension3D,
        Kilo.Rendering.Driver.TextureViewDimension.ViewCube => Silk.NET.WebGPU.TextureViewDimension.DimensionCube,
        _ => Silk.NET.WebGPU.TextureViewDimension.Dimension2D,
    };

    internal static Silk.NET.WebGPU.FilterMode MapFilterMode(Kilo.Rendering.Driver.FilterMode mode) => mode switch
    {
        Kilo.Rendering.Driver.FilterMode.Nearest => Silk.NET.WebGPU.FilterMode.Nearest,
        Kilo.Rendering.Driver.FilterMode.Linear => Silk.NET.WebGPU.FilterMode.Linear,
        _ => Silk.NET.WebGPU.FilterMode.Linear,
    };

    internal static MipmapFilterMode MapMipmapFilter(Kilo.Rendering.Driver.FilterMode mode) => mode switch
    {
        Kilo.Rendering.Driver.FilterMode.Nearest => MipmapFilterMode.Nearest,
        Kilo.Rendering.Driver.FilterMode.Linear => MipmapFilterMode.Linear,
        _ => MipmapFilterMode.Linear,
    };

    internal static Silk.NET.WebGPU.AddressMode MapWrapMode(WrapMode mode) => mode switch
    {
        WrapMode.ClampToEdge => Silk.NET.WebGPU.AddressMode.ClampToEdge,
        WrapMode.Repeat => Silk.NET.WebGPU.AddressMode.Repeat,
        WrapMode.MirrorRepeat => Silk.NET.WebGPU.AddressMode.MirrorRepeat,
        _ => Silk.NET.WebGPU.AddressMode.ClampToEdge,
    };

    internal static Silk.NET.WebGPU.TextureUsage MapTextureUsage(RgTextureUsage usage)
    {
        Silk.NET.WebGPU.TextureUsage result = 0;
        if (usage.HasFlag(RgTextureUsage.RenderAttachment)) result |= Silk.NET.WebGPU.TextureUsage.RenderAttachment;
        if (usage.HasFlag(RgTextureUsage.ShaderBinding)) result |= Silk.NET.WebGPU.TextureUsage.TextureBinding;
        if (usage.HasFlag(RgTextureUsage.CopyDst)) result |= Silk.NET.WebGPU.TextureUsage.CopyDst;
        if (usage.HasFlag(RgTextureUsage.CopySrc)) result |= Silk.NET.WebGPU.TextureUsage.CopySrc;
        if (usage.HasFlag(RgTextureUsage.Storage)) result |= Silk.NET.WebGPU.TextureUsage.StorageBinding;
        return result;
    }

    internal static Silk.NET.WebGPU.BufferUsage MapBufferUsage(RgBufferUsage usage)
    {
        Silk.NET.WebGPU.BufferUsage result = 0;
        if (usage.HasFlag(RgBufferUsage.Vertex)) result |= Silk.NET.WebGPU.BufferUsage.Vertex;
        if (usage.HasFlag(RgBufferUsage.Index)) result |= Silk.NET.WebGPU.BufferUsage.Index;
        if (usage.HasFlag(RgBufferUsage.Uniform)) result |= Silk.NET.WebGPU.BufferUsage.Uniform;
        if (usage.HasFlag(RgBufferUsage.CopyDst)) result |= Silk.NET.WebGPU.BufferUsage.CopyDst;
        if (usage.HasFlag(RgBufferUsage.Storage)) result |= Silk.NET.WebGPU.BufferUsage.Storage;
        if (usage.HasFlag(RgBufferUsage.MapRead)) result |= Silk.NET.WebGPU.BufferUsage.MapRead;
        return result;
    }

    internal static BlendFactor MapBlendFactor(DriverBlendFactor factor) => factor switch
    {
        DriverBlendFactor.Zero => BlendFactor.Zero,
        DriverBlendFactor.One => BlendFactor.One,
        DriverBlendFactor.SrcColor => BlendFactor.Src,
        DriverBlendFactor.OneMinusSrcColor => BlendFactor.OneMinusSrc,
        DriverBlendFactor.SrcAlpha => BlendFactor.SrcAlpha,
        DriverBlendFactor.OneMinusSrcAlpha => BlendFactor.OneMinusSrcAlpha,
        DriverBlendFactor.DstColor => BlendFactor.Dst,
        DriverBlendFactor.DstAlpha => BlendFactor.DstAlpha,
        _ => BlendFactor.Zero,
    };

    internal static Silk.NET.WebGPU.BlendOperation MapBlendOperation(BlendOperation op) => op switch
    {
        BlendOperation.Add => Silk.NET.WebGPU.BlendOperation.Add,
        BlendOperation.Subtract => Silk.NET.WebGPU.BlendOperation.Subtract,
        BlendOperation.ReverseSubtract => Silk.NET.WebGPU.BlendOperation.ReverseSubtract,
        BlendOperation.Min => Silk.NET.WebGPU.BlendOperation.Min,
        BlendOperation.Max => Silk.NET.WebGPU.BlendOperation.Max,
        _ => Silk.NET.WebGPU.BlendOperation.Add,
    };

    internal static PrimitiveTopology MapTopology(DriverPrimitiveTopology topology) => topology switch
    {
        DriverPrimitiveTopology.TriangleList => PrimitiveTopology.TriangleList,
        DriverPrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
        DriverPrimitiveTopology.LineList => PrimitiveTopology.LineList,
        DriverPrimitiveTopology.LineStrip => PrimitiveTopology.LineStrip,
        DriverPrimitiveTopology.PointList => PrimitiveTopology.PointList,
        _ => PrimitiveTopology.TriangleList,
    };

    internal static Silk.NET.WebGPU.VertexFormat MapVertexFormat(VertexFormat format) => format switch
    {
        VertexFormat.Float32x2 => Silk.NET.WebGPU.VertexFormat.Float32x2,
        VertexFormat.Float32x3 => Silk.NET.WebGPU.VertexFormat.Float32x3,
        VertexFormat.Float32x4 => Silk.NET.WebGPU.VertexFormat.Float32x4,
        VertexFormat.UInt32 => Silk.NET.WebGPU.VertexFormat.Uint32,
        VertexFormat.UInt32x4 => Silk.NET.WebGPU.VertexFormat.Uint32x4,
        _ => Silk.NET.WebGPU.VertexFormat.Float32x2,
    };

    internal static Silk.NET.WebGPU.ShaderStage MapShaderVisibility(ShaderVisibility visibility)
    {
        Silk.NET.WebGPU.ShaderStage result = 0;
        if (visibility.HasFlag(ShaderVisibility.Vertex)) result |= Silk.NET.WebGPU.ShaderStage.Vertex;
        if (visibility.HasFlag(ShaderVisibility.Fragment)) result |= Silk.NET.WebGPU.ShaderStage.Fragment;
        if (visibility.HasFlag(ShaderVisibility.Compute)) result |= Silk.NET.WebGPU.ShaderStage.Compute;
        return result;
    }

    internal static CompareFunction MapCompareFunction(DriverCompareFunction func) => func switch
    {
        DriverCompareFunction.Never => CompareFunction.Never,
        DriverCompareFunction.Less => CompareFunction.Less,
        DriverCompareFunction.Equal => CompareFunction.Equal,
        DriverCompareFunction.LessEqual => CompareFunction.LessEqual,
        DriverCompareFunction.Greater => CompareFunction.Greater,
        DriverCompareFunction.NotEqual => CompareFunction.NotEqual,
        DriverCompareFunction.GreaterEqual => CompareFunction.GreaterEqual,
        DriverCompareFunction.Always => CompareFunction.Always,
        _ => CompareFunction.Less,
    };
}
