using System.Numerics;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Flags controlling which render layers a camera draws.
/// Combinable via bitwise OR: e.g. RenderLayers.Meshes | RenderLayers.Particles.
/// </summary>
[System.Flags]
public enum RenderLayers
{
    None      = 0,
    Meshes    = 1 << 0,
    Particles = 1 << 1,
    Sprites   = 1 << 2,
    Text      = 1 << 3,
    /// <summary>Default for Scene cameras: meshes + particles + sprites + text.</summary>
    All = Meshes | Particles | Sprites | Text,
}

/// <summary>
/// Camera component with view and projection matrices.
/// </summary>
public struct Camera
{
    public Camera() { }

    /// <summary>
    /// Creates a perspective camera with sensible defaults for 3D scene rendering.
    /// </summary>
    public static Camera Perspective(float fieldOfView = MathF.PI / 4f, float nearPlane = 0.1f, float farPlane = 100f)
    {
        return new Camera
        {
            FieldOfView = fieldOfView,
            NearPlane = nearPlane,
            FarPlane = farPlane,
            IsActive = true,
            Priority = 0,
            Target = CameraTarget.Screen,
            ClearSettings = CameraClearSettings.Skybox,
            CameraType = CameraType.Scene,
            PostProcessEnabled = true,
            RenderLayers = RenderLayers.All,
        };
    }

    /// <summary>
    /// Creates a UI overlay camera that renders sprites and text on top of the scene.
    /// </summary>
    public static Camera UIOverlay(int priority = 2, RenderLayers layers = RenderLayers.Sprites | RenderLayers.Text)
    {
        return new Camera
        {
            FieldOfView = MathF.PI / 4f,
            NearPlane = 0.1f,
            FarPlane = 100f,
            IsActive = true,
            Priority = priority,
            Target = CameraTarget.Screen,
            ClearSettings = CameraClearSettings.DontClear,
            CameraType = CameraType.UIOverlay,
            PostProcessEnabled = false,
            RenderLayers = layers,
        };
    }

    /// <summary>View matrix (world to camera space). System-managed — do not set manually.</summary>
    public Matrix4x4 ViewMatrix;

    /// <summary>Projection matrix (camera to clip space). System-managed — do not set manually.</summary>
    public Matrix4x4 ProjectionMatrix;

    /// <summary>Field of view in radians.</summary>
    public float FieldOfView;

    /// <summary>Near clipping plane distance.</summary>
    public float NearPlane;

    /// <summary>Far clipping plane distance.</summary>
    public float FarPlane;

    /// <summary>Whether this camera is currently active for rendering.</summary>
    public bool IsActive;

    // --- Multi-camera fields (backward-compatible defaults) ---

    /// <summary>Render priority. Lower values render first. Default 0.</summary>
    public int Priority;

    /// <summary>What this camera renders to. Default <see cref="CameraTarget.Screen"/>.</summary>
    public CameraTarget Target;

    /// <summary>The RenderTexture to render into (when Target == RenderTexture).</summary>
    public RenderTexture? RenderTexture;

    /// <summary>How to clear the render target. Default Skybox.</summary>
    public CameraClearSettings ClearSettings;

    /// <summary>Camera type: Scene (3D) or UIOverlay (2D). Default Scene.</summary>
    public CameraType CameraType;

    /// <summary>Whether to apply post-processing to this camera's output. Default true.</summary>
    public bool PostProcessEnabled = true;

    /// <summary>Which render layers this camera draws. Default <see cref="RenderLayers.All"/>.</summary>
    public RenderLayers RenderLayers = RenderLayers.All;
}
