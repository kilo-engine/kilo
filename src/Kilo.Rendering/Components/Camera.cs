using System.Numerics;

namespace Kilo.Rendering;

/// <summary>
/// Camera component with view and projection matrices.
/// </summary>
public struct Camera
{
    /// <summary>View matrix (world to camera space).</summary>
    public Matrix4x4 ViewMatrix;

    /// <summary>Projection matrix (camera to clip space).</summary>
    public Matrix4x4 ProjectionMatrix;

    /// <summary>Field of view in radians.</summary>
    public float FieldOfView;

    /// <summary>Near clipping plane distance.</summary>
    public float NearPlane;

    /// <summary>Far clipping plane distance.</summary>
    public float FarPlane;

    /// <summary>Whether this camera is currently active for rendering.</summary>
    public bool IsActive;
}
