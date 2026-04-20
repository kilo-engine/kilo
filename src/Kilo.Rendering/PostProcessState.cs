using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering;

/// <summary>
/// Per-camera intermediate textures for post-processing.
/// </summary>
public sealed class PerCameraTextures
{
    public ITexture? SceneColorTexture;
    public int SceneColorWidth, SceneColorHeight;

    public ITexture? BrightExtractTexture;
    public ITexture? BloomBlurHTexture;
    public ITexture? BloomBlurVTexture;
    public ITexture? ToneMappedTexture;
    public int TextureWidth, TextureHeight;

    public ITextureView? BrightExtractStorageView;
    public ITextureView? BloomBlurHStorageView;
    public ITextureView? BloomBlurVStorageView;

    // Per-camera GPU buffer for camera uniform data (avoids shared-buffer overwrite between cameras)
    public IBuffer? CameraBuffer;

    public void EnsureSceneColor(IRenderDriver driver, int width, int height)
    {
        if (SceneColorTexture != null && SceneColorWidth == width && SceneColorHeight == height)
            return;
        SceneColorTexture?.Dispose();
        SceneColorTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = width,
            Height = height,
            Format = DriverPixelFormat.RGBA16Float,
            Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
        });
        SceneColorWidth = width;
        SceneColorHeight = height;
    }

    public void EnsureBloomTextures(IRenderDriver driver, int width, int height)
    {
        if (BrightExtractTexture != null && TextureWidth == width && TextureHeight == height)
            return;

        BrightExtractTexture?.Dispose();
        BloomBlurHTexture?.Dispose();
        BloomBlurVTexture?.Dispose();
        ToneMappedTexture?.Dispose();
        BrightExtractStorageView?.Dispose();
        BloomBlurHStorageView?.Dispose();
        BloomBlurVStorageView?.Dispose();

        var hdrDesc = new TextureDescriptor
        {
            Width = width,
            Height = height,
            Format = DriverPixelFormat.RGBA16Float,
            Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
        };

        BrightExtractTexture = driver.CreateTexture(hdrDesc);
        BrightExtractStorageView = driver.CreateTextureView(BrightExtractTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA16Float,
            Dimension = TextureViewDimension.View2D,
        });

        BloomBlurHTexture = driver.CreateTexture(hdrDesc);
        BloomBlurHStorageView = driver.CreateTextureView(BloomBlurHTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA16Float,
            Dimension = TextureViewDimension.View2D,
        });

        BloomBlurVTexture = driver.CreateTexture(hdrDesc);
        BloomBlurVStorageView = driver.CreateTextureView(BloomBlurVTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA16Float,
            Dimension = TextureViewDimension.View2D,
        });

        ToneMappedTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = width,
            Height = height,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
        });

        TextureWidth = width;
        TextureHeight = height;
    }

    /// <summary>
    /// Ensures a per-camera GPU buffer exists for uploading camera uniform data.
    /// Each camera gets its own buffer to avoid the shared-buffer overwrite issue
    /// when multiple cameras submit QueueWriteBuffer in the same command encoder.
    /// </summary>
    public void EnsureCameraBuffer(IRenderDriver driver)
    {
        if (CameraBuffer != null) return;
        CameraBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 256, // CameraData.Size (256 bytes, WebGPU alignment)
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
    }

    public void Dispose()
    {
        SceneColorTexture?.Dispose();
        BrightExtractTexture?.Dispose();
        BloomBlurHTexture?.Dispose();
        BloomBlurVTexture?.Dispose();
        ToneMappedTexture?.Dispose();
        BrightExtractStorageView?.Dispose();
        BloomBlurHStorageView?.Dispose();
        BloomBlurVStorageView?.Dispose();
        CameraBuffer?.Dispose();
    }
}

public sealed class PostProcessState
{
    // Per-camera textures, keyed by camera prefix ("Screen_", "Cam123_", etc.)
    private readonly Dictionary<string, PerCameraTextures> _cameraTextures = [];

    // Post-processing pipelines (lazy init, shared across all cameras)
    public IComputePipeline? BloomExtractPipeline;
    public IComputePipeline? BloomBlurHPipeline;
    public IComputePipeline? BloomBlurVPipeline;
    public IRenderPipeline? CompositeToneMapPipeline;
    public IRenderPipeline? FxaaPipeline;
    public IRenderPipeline? BlitPipeline;
    public IRenderPipeline? OverlayBlitPipeline;
    public ISampler? LinearSampler;
    public IBuffer? ParamsBuffer;
    public bool Initialized;

    /// <summary>
    /// Gets or creates per-camera textures for the given prefix.
    /// </summary>
    public PerCameraTextures GetCameraTextures(string prefix)
    {
        if (!_cameraTextures.TryGetValue(prefix, out var tex))
        {
            tex = new PerCameraTextures();
            _cameraTextures[prefix] = tex;
        }
        return tex;
    }

    // Backward compatibility: screen camera uses empty prefix
    public ITexture? SceneColorTexture => GetCameraTextures("").SceneColorTexture;
    public int SceneColorWidth => GetCameraTextures("").SceneColorWidth;
    public int SceneColorHeight => GetCameraTextures("").SceneColorHeight;
}
