using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering.Assets;

/// <summary>
/// Loads cubemap textures from 6 face images or generates procedural gradient cubemaps.
/// </summary>
public sealed class CubemapLoader
{
    /// <summary>
    /// Loads a cubemap from 6 face image paths (order: +X, -X, +Y, -Y, +Z, -Z).
    /// </summary>
    public ITextureView LoadCubemap(IRenderDriver driver, string[] facePaths)
    {
        if (facePaths.Length != 6)
            throw new ArgumentException("Cubemap requires exactly 6 face paths (+X, -X, +Y, -Y, +Z, -Z).");

        // Load first image to get dimensions
        using var firstImage = Image.Load<Rgba32>(facePaths[0]);
        int size = firstImage.Width;

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = size,
            Height = size,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
            DepthOrArrayLayers = 6,
        });

        for (int face = 0; face < 6; face++)
        {
            using var image = Image.Load<Rgba32>(facePaths[face]);
            var pixels = new byte[image.Width * image.Height * 4];
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    int idx = (y * image.Width + x) * 4;
                    pixels[idx] = pixel.R;
                    pixels[idx + 1] = pixel.G;
                    pixels[idx + 2] = pixel.B;
                    pixels[idx + 3] = pixel.A;
                }
            }
            texture.UploadLayer<byte>(pixels, face);
        }

        return driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.ViewCube,
            MipLevelCount = 1,
        });
    }

    /// <summary>
    /// Creates a procedural gradient cubemap (top-color to bottom-color on +Y/-Y faces,
    /// solid mid-color on other faces).
    /// </summary>
    public ITextureView CreateGradientCubemap(IRenderDriver driver,
        Vector3 topColor, Vector3 bottomColor, int size = 256)
    {
        var midColor = (topColor + bottomColor) * 0.5f;

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = size,
            Height = size,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
            DepthOrArrayLayers = 6,
        });

        byte toByte(float f) => (byte)(Math.Clamp(f, 0f, 1f) * 255f);

        for (int face = 0; face < 6; face++)
        {
            var pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1); // 0 = top, 1 = bottom
                var color = Vector3.Lerp(topColor, bottomColor, t);

                for (int x = 0; x < size; x++)
                {
                    // For +Y (face 2) and -Y (face 3): use gradient
                    // For other faces: use gradient mapped horizontally
                    var c = color;
                    if (face < 2 || face >= 4)
                    {
                        // Side faces: gradient top-to-bottom
                        c = color;
                    }

                    int idx = (y * size + x) * 4;
                    pixels[idx] = toByte(c.X);
                    pixels[idx + 1] = toByte(c.Y);
                    pixels[idx + 2] = toByte(c.Z);
                    pixels[idx + 3] = 255;
                }
            }
            texture.UploadLayer<byte>(pixels, face);
        }

        return driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.ViewCube,
            MipLevelCount = 1,
        });
    }
}
