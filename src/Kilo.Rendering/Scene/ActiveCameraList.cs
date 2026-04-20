using System.Numerics;

namespace Kilo.Rendering.Scene;

/// <summary>
/// Per-frame resource containing all active cameras sorted by priority.
/// Written by CameraPrepareSystem, consumed by CameraRenderLoopSystem.
/// </summary>
public sealed class ActiveCameraList
{
    public List<ActiveCameraEntry> Cameras { get; } = [];

    public void Clear() => Cameras.Clear();
}

/// <summary>
/// Entry for a single active camera in the render loop.
/// </summary>
public struct ActiveCameraEntry
{
    public CameraData CameraData;
    public CameraTarget Target;
    public RenderTexture? RenderTexture;
    public CameraClearSettings ClearSettings;
    public CameraType CameraType;
    public int RenderWidth;
    public int RenderHeight;
    public bool PostProcessEnabled;
    public RenderLayers RenderLayers;
    public ulong EntityId;
}
