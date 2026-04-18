using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Assets;

/// <summary>
/// Loads image files into GPU textures with caching.
/// </summary>
public sealed class TextureLoader
{
    private readonly Dictionary<string, ITexture> _textureCache = [];
    private readonly Dictionary<string, ITextureView> _textureViewCache = [];
    private ISampler? _defaultSampler;
    private ITextureView? _placeholderDepthView;

    public ITexture LoadTexture(IRenderDriver driver, string path)
    {
        if (_textureCache.TryGetValue(path, out var existing))
            return existing;

        using var image = Image.Load<Rgba32>(path);
        var width = image.Width;
        var height = image.Height;

        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];
                int idx = (y * width + x) * 4;
                pixels[idx] = pixel.R;
                pixels[idx + 1] = pixel.G;
                pixels[idx + 2] = pixel.B;
                pixels[idx + 3] = pixel.A;
            }
        }

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = width,
            Height = height,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        texture.UploadData<byte>(pixels);

        _textureCache[path] = texture;
        return texture;
    }

    public ITextureView GetOrCreateView(IRenderDriver driver, ITexture texture)
    {
        string key = $"{texture.Width}x{texture.Height}_{texture.Format}";
        if (_textureViewCache.TryGetValue(key, out var existing))
            return existing;

        var view = driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = texture.Format,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });
        _textureViewCache[key] = view;
        return view;
    }

    public ISampler GetOrCreateSampler(IRenderDriver driver)
    {
        if (_defaultSampler != null)
            return _defaultSampler;

        _defaultSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
            AddressModeU = WrapMode.Repeat,
            AddressModeV = WrapMode.Repeat,
            AddressModeW = WrapMode.Repeat,
        });
        return _defaultSampler;
    }

    public ITextureView GetOrCreatePlaceholderDepthView(IRenderDriver driver)
    {
        if (_placeholderDepthView != null)
            return _placeholderDepthView;

        var depthTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.Depth24Plus,
            Usage = TextureUsage.ShaderBinding | TextureUsage.RenderAttachment,
        });
        _placeholderDepthView = driver.CreateTextureView(depthTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.Depth24Plus,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });
        return _placeholderDepthView;
    }
}
