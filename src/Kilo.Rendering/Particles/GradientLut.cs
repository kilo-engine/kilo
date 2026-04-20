using System.Runtime.InteropServices;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Particles;

/// <summary>
/// Generates 256x1 LUT textures from Gradient curves for GPU sampling.
/// </summary>
public static class GradientLut
{
    public static ITexture CreateColorLut(IRenderDriver driver, Gradient<Color4>? gradient)
    {
        // Default: white to transparent
        gradient ??= Gradient<Color4>.FromColors((0f, Color4.White), (1f, Color4.Transparent));

        var pixels = new byte[256 * 4];
        for (int i = 0; i < 256; i++)
        {
            var c = gradient.Evaluate(i / 255f);
            pixels[i * 4 + 0] = (byte)(Math.Clamp(c.R, 0f, 1f) * 255f);
            pixels[i * 4 + 1] = (byte)(Math.Clamp(c.G, 0f, 1f) * 255f);
            pixels[i * 4 + 2] = (byte)(Math.Clamp(c.B, 0f, 1f) * 255f);
            pixels[i * 4 + 3] = (byte)(Math.Clamp(c.A, 0f, 1f) * 255f);
        }

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 256,
            Height = 1,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        texture.UploadData(pixels);
        return texture;
    }

    public static ITexture CreateSizeLut(IRenderDriver driver, Gradient<float>? gradient)
    {
        // Default: constant 1.0
        gradient ??= Gradient<float>.FromValues((0f, 1f), (1f, 1f));

        // Store as RGBA8Unorm to match UploadData's 4-bytes-per-pixel assumption
        // R channel = size value mapped to 0-255
        var pixels = new byte[256 * 4];
        for (int i = 0; i < 256; i++)
        {
            var v = Math.Clamp(gradient.Evaluate(i / 255f), 0f, 1f);
            pixels[i * 4 + 0] = (byte)(v * 255f); // R = size
            pixels[i * 4 + 1] = 255;               // A = 1.0
            pixels[i * 4 + 2] = 0;
            pixels[i * 4 + 3] = 0;
        }

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 256,
            Height = 1,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        texture.UploadData<byte>(pixels);
        return texture;
    }
}
