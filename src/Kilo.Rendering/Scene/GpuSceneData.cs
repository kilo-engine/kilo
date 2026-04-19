using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.Scene;

/// <summary>
/// Per-frame GPU scene data buffers and draw information.
/// </summary>
public sealed class GpuSceneData
{
    public IBuffer CameraBuffer { get; set; } = null!;
    public IBuffer ObjectDataBuffer { get; set; } = null!;
    public IBuffer LightBuffer { get; set; } = null!;
    public IBuffer? ShadowDataBuffer { get; set; }
    public ISampler? ShadowSampler { get; set; }

    /// <summary>Light-space ViewProjection matrix for shadow mapping.</summary>
    public Matrix4x4 ShadowLightVP;

    // Draw data — encapsulated
    private DrawData[] _drawData = [];
    private int _drawCount;
    private int _lightCount;

    public int DrawCount => _drawCount;
    public int LightCount => _lightCount;

    /// <summary>Pending camera data, written by CameraPrepareSystem, finalized by LightPrepareSystem.</summary>
    internal CameraData PendingCamera;

    public DrawData GetDraw(int index) => _drawData[index];

    public void SetDrawData(DrawData[] data, int count)
    {
        _drawData = data;
        _drawCount = count;
    }

    public void SetLightCount(int count) => _lightCount = count;
}

/// <summary>
/// CPU-side per-draw information.
/// </summary>
public struct DrawData
{
    public int MeshHandle;
    public int MaterialId;
    public bool IsSkinned;
    public IBindingSet? JointBindingSet;
}

/// <summary>
/// GPU camera uniform data. Padded to 256 bytes for WebGPU alignment.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CameraData
{
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Vector3 Position;
    private float _pad0;
    public int LightCount;
    private int _pad1;
    private int _pad2;
    private int _pad3;
    private Vector4 _pad4;
    private Vector4 _pad5;
    private Vector4 _pad6;
    private Vector4 _pad7;
    private Vector4 _pad8;
    private Vector4 _pad9;

    public static int Size => 256;
}

/// <summary>
/// GPU object uniform data. Padded to 256 bytes for dynamic uniform offsets.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ObjectData
{
    public Matrix4x4 Model;       // offset 0, 64 bytes
    public Vector4 BaseColor;     // offset 64, 16 bytes (vec4 = alignment 16)
    public int MaterialId;        // offset 80, 4 bytes
    public int UseTexture;        // offset 84, 4 bytes
    private int _pad0;            // offset 88
    private int _pad1;            // offset 92
    private Vector4 _pad3;
    private Vector4 _pad4;
    private Vector4 _pad5;
    private Vector4 _pad6;
    private Vector4 _pad7;
    private Vector4 _pad8;
    private Vector4 _pad9;
    private Vector4 _pad10;
    private Vector4 _pad11;
    private Vector4 _pad12;

    public static int Size => 256;
}

/// <summary>
/// GPU light data. Storage buffer element.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightData
{
    public Vector3 DirectionOrPosition;
    public float _pad0;
    public Vector3 Color;
    public float Intensity;
    public float Range;
    public int LightType;
    private int _pad1;
    private int _pad2;
}
