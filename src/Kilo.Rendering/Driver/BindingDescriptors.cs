namespace Kilo.Rendering.Driver;

public enum BindingType { UniformBuffer, Texture, Sampler, StorageTexture, StorageBuffer }

public enum ShaderVisibility
{
    None = 0,
    Vertex = 1,
    Fragment = 2,
    Compute = 4,
    All = Vertex | Fragment | Compute,
}

public sealed class BindingLayoutEntry
{
    public int Binding { get; init; }
    public BindingType Type { get; init; }
    public ShaderVisibility Visibility { get; init; } = ShaderVisibility.All;
    public nuint MinBindingSize { get; init; } = 0;
}

public sealed class BindingSetLayout
{
    public BindingLayoutEntry[] Entries { get; init; } = [];
}

public sealed class TextureBinding
{
    public ITextureView TextureView { get; init; } = null!;
    public int Binding { get; init; }
}

public sealed class SamplerBinding
{
    public ISampler Sampler { get; init; } = null!;
    public int Binding { get; init; }
}

public sealed class UniformBufferBinding
{
    public IBuffer Buffer { get; init; } = null!;
    public int Binding { get; init; }
}

public sealed class StorageTextureBinding
{
    public ITextureView TextureView { get; init; } = null!;
    public int Binding { get; init; }
    public DriverPixelFormat Format { get; init; }
}

public sealed class StorageBufferBinding
{
    public IBuffer Buffer { get; init; } = null!;
    public int Binding { get; init; }
}

public sealed class BindingSetDescriptor
{
    public BindingSetLayout Layout { get; init; } = null!;
    public UniformBufferBinding[] UniformBuffers { get; init; } = [];
    public TextureBinding[] Textures { get; init; } = [];
    public SamplerBinding[] Samplers { get; init; } = [];
    public StorageTextureBinding[] StorageTextures { get; init; } = [];
    public StorageBufferBinding[] StorageBuffers { get; init; } = [];
}
