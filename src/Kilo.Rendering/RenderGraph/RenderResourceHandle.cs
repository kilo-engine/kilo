namespace Kilo.Rendering.RenderGraph;

public enum RenderResourceType { Texture, Buffer }

public readonly struct RenderResourceHandle : IEquatable<RenderResourceHandle>
{
    internal readonly int Id;
    internal readonly RenderResourceType Type;

    internal RenderResourceHandle(int id, RenderResourceType type)
    {
        Id = id;
        Type = type;
    }

    public bool Equals(RenderResourceHandle other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is RenderResourceHandle h && Equals(h);
    public override int GetHashCode() => Id;
    public static bool operator ==(RenderResourceHandle a, RenderResourceHandle b) => a.Id == b.Id;
    public static bool operator !=(RenderResourceHandle a, RenderResourceHandle b) => a.Id != b.Id;
}
