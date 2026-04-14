namespace Kilo.Rendering.Driver;

public interface ISampler : IDisposable { }

public enum FilterMode
{
    Nearest,
    Linear,
}

public enum WrapMode
{
    ClampToEdge,
    Repeat,
    MirrorRepeat,
}

public sealed class SamplerDescriptor
{
    public FilterMode MinFilter { get; init; } = FilterMode.Linear;
    public FilterMode MagFilter { get; init; } = FilterMode.Linear;
    public FilterMode MipFilter { get; init; } = FilterMode.Linear;
    public WrapMode AddressModeU { get; init; } = WrapMode.ClampToEdge;
    public WrapMode AddressModeV { get; init; } = WrapMode.ClampToEdge;
    public WrapMode AddressModeW { get; init; } = WrapMode.ClampToEdge;

    /// <summary>Enable comparison mode for shadow mapping (sampler_comparison in WGSL).</summary>
    public bool Compare { get; init; } = false;

    /// <summary>Compare function used when Compare is true.</summary>
    public DriverCompareFunction CompareFunction { get; init; } = DriverCompareFunction.Less;
}
