namespace Kilo.Rendering.RenderGraph;

[Flags]
public enum TextureUsage
{
    RenderAttachment = 1,
    ShaderBinding = 2,
    CopyDst = 4,
    CopySrc = 8,
    Storage = 16,
}

[Flags]
public enum BufferUsage
{
    Vertex = 1,
    Index = 2,
    Uniform = 4,
    CopyDst = 8,
    Storage = 16,
    MapRead = 32,
}

public sealed class TextureDescriptor : IEquatable<TextureDescriptor>
{
    public int Width { get; init; }
    public int Height { get; init; }
    public Driver.DriverPixelFormat Format { get; init; } = Driver.DriverPixelFormat.BGRA8Unorm;
    public int MipLevelCount { get; init; } = 1;
    public int SampleCount { get; init; } = 1;
    public TextureUsage Usage { get; init; } = TextureUsage.RenderAttachment;

    public bool Equals(TextureDescriptor? other)
    {
        if (other is null) return false;
        return Width == other.Width && Height == other.Height && Format == other.Format
            && MipLevelCount == other.MipLevelCount && SampleCount == other.SampleCount
            && Usage == other.Usage;
    }

    public override bool Equals(object? obj) => Equals(obj as TextureDescriptor);

    public override int GetHashCode()
    {
        return HashCode.Combine(Width, Height, Format, MipLevelCount, SampleCount, Usage);
    }
}

public sealed class BufferDescriptor : IEquatable<BufferDescriptor>
{
    public nuint Size { get; init; }
    public BufferUsage Usage { get; init; } = BufferUsage.Vertex;

    public bool Equals(BufferDescriptor? other)
    {
        if (other is null) return false;
        return Size == other.Size && Usage == other.Usage;
    }

    public override bool Equals(object? obj) => Equals(obj as BufferDescriptor);

    public override int GetHashCode()
    {
        return HashCode.Combine(Size, Usage);
    }
}
