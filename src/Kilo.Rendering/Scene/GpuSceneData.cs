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
    public ITexture? ShadowDepthTexture { get; set; }
    public ITextureView? ShadowDepthView { get; set; }

    /// <summary>Light-space ViewProjection matrix for shadow mapping.</summary>
    public Matrix4x4 ShadowLightVP;

    internal IRenderDriver? Driver;

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

    /// <summary>
    /// Per-camera buffer override for group 0 (camera uniform).
    /// Set by each camera's Forward/Particle pass execute callback to avoid
    /// the shared CameraBuffer being overwritten by multiple cameras.
    /// When null, the draw emitter falls back to material.BindingSets[0].
    /// </summary>
    public IBuffer? CurrentCameraBuffer;

    /// <summary>
    /// Cache for per-camera binding sets. Keyed by (pipeline, cameraBuffer) because
    /// WebGPU implicit bind group layouts are unique per pipeline — a binding set
    /// created from one pipeline's layout is incompatible with another's.
    /// </summary>
    private readonly Dictionary<(IRenderPipeline pipeline, IBuffer buffer), IBindingSet> _cameraBindingCache = [];

    /// <summary>
    /// Gets or creates a binding set for group 0 using the per-camera buffer and
    /// the given pipeline's layout. Cached to avoid per-frame allocation.
    /// </summary>
    public IBindingSet GetOrCreateCameraBindingSet(IRenderPipeline pipeline, IBuffer cameraBuffer, IRenderDriver driver)
    {
        var key = (pipeline, cameraBuffer);
        if (!_cameraBindingCache.TryGetValue(key, out var bindingSet))
        {
            bindingSet = driver.CreateBindingSetForPipeline(pipeline, 0,
                [new UniformBufferBinding { Buffer = cameraBuffer, Binding = 0 }]);
            _cameraBindingCache[key] = bindingSet;
        }
        return bindingSet;
    }

    public DrawData GetDraw(int index) => _drawData[index];

    public void SetDrawData(DrawData[] data, int count, int opaqueCount)
    {
        _drawData = data;
        _drawCount = count;
        _opaqueCount = opaqueCount;
    }

    public void SetLightCount(int count) => _lightCount = count;
}

/// <summary>
/// CPU-side per-draw information.
/// </summary>
public struct DrawData
{
    public MeshHandle MeshHandle;
    public MaterialHandle MaterialHandle;
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
