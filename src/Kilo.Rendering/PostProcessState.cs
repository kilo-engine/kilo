using Kilo.Rendering.Driver;

namespace Kilo.Rendering;

public sealed class PostProcessState
{
    // Persistent intermediate textures (reused across frames, recreated on resize)
    public ITexture? SceneColorTexture;
    public int SceneColorWidth, SceneColorHeight;
    public ITexture? BrightExtractTexture;
    public ITexture? BloomBlurHTexture;
    public ITexture? BloomBlurVTexture;
    public ITexture? ToneMappedTexture;
    public int TextureWidth, TextureHeight;

    // Texture views for compute storage access
    public ITextureView? BrightExtractStorageView;
    public ITextureView? BloomBlurHStorageView;
    public ITextureView? BloomBlurVStorageView;

    // Post-processing pipelines (lazy init)
    public IComputePipeline? BloomExtractPipeline;
    public IComputePipeline? BloomBlurHPipeline;
    public IComputePipeline? BloomBlurVPipeline;
    public IRenderPipeline? CompositeToneMapPipeline;
    public IRenderPipeline? FxaaPipeline;
    public IRenderPipeline? BlitPipeline;
    public ISampler? LinearSampler;
    public IBuffer? ParamsBuffer;
    public bool Initialized;
}
