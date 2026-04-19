using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;

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
    public ITexture? ShadowDepthTexture { get; set; }
    public ITextureView? ShadowDepthView { get; set; }

    /// <summary>Light-space ViewProjection matrix for shadow mapping.</summary>
    public Matrix4x4 ShadowLightVP;

    // Draw data — encapsulated
    private DrawData[] _drawData = [];
    private int _drawCount;
    private int _opaqueCount;
    private int _lightCount;

    public int DrawCount => _drawCount;
    public int OpaqueCount => _opaqueCount;
    public int LightCount => _lightCount;

    /// <summary>Pending camera data, written by CameraPrepareSystem, finalized by LightPrepareSystem.</summary>
    public CameraData PendingCamera;

    public DrawData GetDraw(int index) => _drawData[index];

    public void SetDrawData(DrawData[] data, int count, int opaqueCount)
    {
        _drawData = data;
        _drawCount = count;
        _opaqueCount = opaqueCount;
    }

    public void SetLightCount(int count) => _lightCount = count;

    /// <summary>
    /// Emits a single draw call for the draw at the given index.
    /// Handles static and skinned meshes uniformly.
    /// </summary>
    public void EmitDraw(IRenderCommandEncoder encoder, RenderContext context, int index)
    {
        var draw = _drawData[index];
        if (draw.MeshHandle < 0 || draw.MeshHandle >= context.Meshes.Count) return;
        if (draw.MaterialId < 0 || draw.MaterialId >= context.Materials.Count) return;

        var mesh = context.Meshes[draw.MeshHandle];
        var material = context.Materials[draw.MaterialId];

        encoder.SetPipeline(material.Pipeline);
        encoder.SetVertexBuffer(0, mesh.VertexBuffer);
        encoder.SetIndexBuffer(mesh.IndexBuffer);
        encoder.SetBindingSet(0, material.BindingSets[0]);
        encoder.SetBindingSet(1, material.BindingSets[1], (uint)(index * ObjectData.Size));
        encoder.SetBindingSet(2, material.BindingSets[2]);
        if (material.BindingSets.Length > 3)
            encoder.SetBindingSet(3, material.BindingSets[3]);
        if (draw.IsSkinned && draw.JointBindingSet != null)
            encoder.SetBindingSet(4, draw.JointBindingSet);
        encoder.DrawIndexed((int)mesh.IndexCount);
    }
}

/// <summary>
/// CPU-side per-draw information.
/// </summary>
public struct DrawData
{
    public int MeshHandle;
    public int MaterialId;
    public bool IsSkinned;
    public bool IsTransparent;
    public IBindingSet? JointBindingSet;
}

/// <summary>
/// GPU camera uniform data. Padded to 256 bytes for WebGPU alignment.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct CameraData
{
    [FieldOffset(0)] public Matrix4x4 View;
    [FieldOffset(64)] public Matrix4x4 Projection;
    [FieldOffset(128)] public Vector3 Position;
    [FieldOffset(144)] public int LightCount;

    public static int Size => 256;
}

/// <summary>
/// GPU object uniform data. Padded to 256 bytes for dynamic uniform offsets.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct ObjectData
{
    [FieldOffset(0)] public Matrix4x4 Model;
    [FieldOffset(64)] public Vector4 BaseColor;
    [FieldOffset(80)] public int MaterialId;
    [FieldOffset(84)] public int UseTexture;
    [FieldOffset(88)] public float Metallic;
    [FieldOffset(92)] public float Roughness;
    [FieldOffset(96)] public int UseNormalMap;

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
