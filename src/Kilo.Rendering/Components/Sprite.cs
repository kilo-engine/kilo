using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// 2D sprite rendering component.
/// </summary>
public struct Sprite
{
    /// <summary>Color tint for the sprite (RGBA).</summary>
    public Vector4 Tint;

    /// <summary>Size of the sprite in world units.</summary>
    public Vector2 Size;

    /// <summary>Handle/index to the texture resource.</summary>
    public int TextureHandle;

    /// <summary>Z-index for draw order sorting.</summary>
    public float ZIndex;
}
