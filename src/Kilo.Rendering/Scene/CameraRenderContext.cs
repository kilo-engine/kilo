namespace Kilo.Rendering.Scene;

/// <summary>
/// Per-camera rendering context passed to render systems within the camera loop.
/// Provides unique resource names and camera-specific settings.
/// </summary>
public readonly struct CameraRenderContext
{
    public readonly ActiveCameraEntry Camera;
    public readonly string Prefix;

    public CameraRenderContext(ActiveCameraEntry camera)
    {
        Camera = camera;
        Prefix = camera.CameraType == CameraType.UIOverlay
            ? "Overlay_"
            : camera.Target == CameraTarget.Screen
                ? ""
                : $"Cam{camera.EntityId}_";
    }

    /// <summary>Name for the HDR scene color texture in the RenderGraph.</summary>
    public string SceneColorName => $"{Prefix}SceneColor";

    /// <summary>Name for the final output target. Screen cameras use "Backbuffer" (the RenderGraph's swapchain name).</summary>
    public string OutputName => Camera.Target == CameraTarget.Screen ? "Backbuffer" : $"{Prefix}Output";

    /// <summary>Name for the depth texture in the RenderGraph.</summary>
    public string DepthName => $"{Prefix}Depth";

    /// <summary>Name for the tone-mapped LDR texture (before final output).</summary>
    public string ToneMappedName => $"{Prefix}ToneMapped";

    /// <summary>Render target width in pixels.</summary>
    public int Width => Camera.RenderWidth;

    /// <summary>Render target height in pixels.</summary>
    public int Height => Camera.RenderHeight;
}
