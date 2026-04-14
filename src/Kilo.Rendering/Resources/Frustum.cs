using System.Numerics;

namespace Kilo.Rendering.Resources;

/// <summary>
/// View frustum for culling, built from a ViewProjection matrix.
/// </summary>
public struct Frustum
{
    public Plane Left;
    public Plane Right;
    public Plane Top;
    public Plane Bottom;
    public Plane Near;
    public Plane Far;

    public static Frustum FromViewProjection(Matrix4x4 vp)
    {
        // Extract planes from the ViewProjection matrix (column-major access via Mij)
        // Left:   m14 + m11
        var left = new Plane(
            vp.M14 + vp.M11,
            vp.M24 + vp.M21,
            vp.M34 + vp.M31,
            vp.M44 + vp.M41);

        // Right:  m14 - m11
        var right = new Plane(
            vp.M14 - vp.M11,
            vp.M24 - vp.M21,
            vp.M34 - vp.M31,
            vp.M44 - vp.M41);

        // Top:    m14 - m12
        var top = new Plane(
            vp.M14 - vp.M12,
            vp.M24 - vp.M22,
            vp.M34 - vp.M32,
            vp.M44 - vp.M42);

        // Bottom: m14 + m12
        var bottom = new Plane(
            vp.M14 + vp.M12,
            vp.M24 + vp.M22,
            vp.M34 + vp.M32,
            vp.M44 + vp.M42);

        // Near:   m13
        var near = new Plane(
            vp.M13,
            vp.M23,
            vp.M33,
            vp.M43);

        // Far:    m14 - m13
        var far = new Plane(
            vp.M14 - vp.M13,
            vp.M24 - vp.M23,
            vp.M34 - vp.M33,
            vp.M44 - vp.M43);

        // Normalize all planes
        Normalize(ref left);
        Normalize(ref right);
        Normalize(ref top);
        Normalize(ref bottom);
        Normalize(ref near);
        Normalize(ref far);

        return new Frustum { Left = left, Right = right, Top = top, Bottom = bottom, Near = near, Far = far };
    }

    /// <summary>
    /// Test if a world-space AABB intersects the frustum.
    /// </summary>
    public bool IntersectsAABB(Vector3 min, Vector3 max)
    {
        return IsInside(Left, min, max)
            && IsInside(Right, min, max)
            && IsInside(Top, min, max)
            && IsInside(Bottom, min, max)
            && IsInside(Near, min, max)
            && IsInside(Far, min, max);
    }

    private static bool IsInside(Plane plane, Vector3 min, Vector3 max)
    {
        // Pick the positive vertex (the corner most aligned with the plane normal)
        var p = new Vector3(
            plane.Normal.X >= 0 ? max.X : min.X,
            plane.Normal.Y >= 0 ? max.Y : min.Y,
            plane.Normal.Z >= 0 ? max.Z : min.Z);

        return plane.Normal.X * p.X + plane.Normal.Y * p.Y + plane.Normal.Z * p.Z + plane.D >= 0;
    }

    private static void Normalize(ref Plane plane)
    {
        float len = plane.Normal.Length();
        if (len > 0)
        {
            float inv = 1f / len;
            plane = new Plane(plane.Normal * inv, plane.D * inv);
        }
    }
}
