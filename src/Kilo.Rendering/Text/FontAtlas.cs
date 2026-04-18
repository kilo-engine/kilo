using System.Numerics;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace Kilo.Rendering.Text;

/// <summary>
/// Glyph info stored in the font atlas.
/// </summary>
public struct GlyphInfo
{
    public Vector2 UVMin;
    public Vector2 UVMax;
    public Vector2 Size;       // glyph size in pixels
    public Vector2 Offset;     // offset from baseline
    public float Advance;      // horizontal advance
}

/// <summary>
/// A font atlas: packs glyphs into a GPU texture for text rendering.
/// </summary>
public sealed class FontAtlas
{
    public ITexture Texture { get; set; } = null!;
    public ITextureView TextureView { get; set; } = null!;
    public Dictionary<char, GlyphInfo> Glyphs { get; set; } = [];
    public int AtlasWidth { get; set; }
    public int AtlasHeight { get; set; }
    public float FontSize { get; set; }

    /// <summary>
    /// Build a font atlas from the system default font (or a provided TTF path).
    /// </summary>
    public static FontAtlas Build(IRenderDriver driver, float fontSize, string? fontPath = null)
    {
        SixLabors.Fonts.Font font;
        if (fontPath != null && File.Exists(fontPath))
        {
            var collection = new SixLabors.Fonts.FontCollection();
            using var stream = File.OpenRead(fontPath);
            var family = collection.Add(stream);
            font = family.CreateFont(fontSize);
        }
        else
        {
            // Try common fonts, fall back to first available
            var families = SixLabors.Fonts.SystemFonts.Families;
            SixLabors.Fonts.FontFamily family = default;
            bool found = false;
            foreach (var f in families)
            {
                if (f.Name.Contains("Arial") || f.Name.Contains("Consola"))
                {
                    family = f;
                    found = true;
                    break;
                }
            }
            if (!found)
                family = families.First();
            font = family.CreateFont(fontSize);
        }

        var fontMetrics = font.FontMetrics;
        float scaleFactor = fontSize / fontMetrics.UnitsPerEm;

        var glyphs = new Dictionary<char, GlyphInfo>();

        // Characters to bake: ASCII printable range
        var chars = new List<char>();
        for (int c = 32; c < 127; c++) chars.Add((char)c);

        int atlasWidth = 1024;
        int atlasHeight = 1024;
        int cursorX = 0;
        int cursorY = 0;
        int rowHeight = 0;
        const int Padding = 2; // padding between glyphs to prevent atlas bleeding

        var layoutInfos = new List<(char C, int X, int Y, int W, int H, float Advance, float OffsetX)>();

        foreach (var ch in chars)
        {
            var cp = new SixLabors.Fonts.Unicode.CodePoint(ch);
            if (fontMetrics.TryGetGlyphMetrics(cp,
                    SixLabors.Fonts.TextAttributes.None,
                    SixLabors.Fonts.TextDecorations.None,
                    SixLabors.Fonts.LayoutMode.HorizontalTopBottom,
                    SixLabors.Fonts.ColorFontSupport.None,
                    out var metricsList))
            {
                var gm = metricsList[0];
                int glyphW = (int)(gm.AdvanceWidth * scaleFactor) + Padding * 2;
                int glyphH = (int)(fontSize + 4);
                float advance = gm.AdvanceWidth * scaleFactor;
                float offsetX = gm.LeftSideBearing * scaleFactor;

                if (cursorX + glyphW > atlasWidth)
                {
                    cursorX = 0;
                    cursorY += rowHeight + Padding;
                    rowHeight = 0;
                }

                layoutInfos.Add((ch, cursorX, cursorY, glyphW, glyphH, advance, offsetX));
                cursorX += glyphW;
                if (glyphH > rowHeight) rowHeight = glyphH;
            }
        }

        // Resize atlas if needed
        while (cursorY + rowHeight > atlasHeight) atlasHeight *= 2;

        // Second pass: render glyphs to atlas
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(
            atlasWidth, atlasHeight,
            new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 0));

        foreach (var (ch, x, y, w, h, advance, offsetX) in layoutInfos)
        {
            // Render single character into temp image
            using var charImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(
                w, h,
                new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 0));

            charImage.Mutate(ctx =>
            {
                ctx.DrawText(ch.ToString(), font,
                    SixLabors.ImageSharp.Color.White,
                    new SixLabors.ImageSharp.PointF(-offsetX + Padding, 0));
            });

            // Copy to atlas
            image.Mutate(ctx =>
            {
                ctx.DrawImage(charImage, new SixLabors.ImageSharp.Point(x, y), 1f);
            });

            glyphs[ch] = new GlyphInfo
            {
                UVMin = new Vector2((float)(x + Padding) / atlasWidth, (float)y / atlasHeight),
                UVMax = new Vector2((float)(x + w - Padding) / atlasWidth, (float)(y + h) / atlasHeight),
                Size = new Vector2(w - Padding * 2, h),
                Offset = new Vector2(offsetX, 0),
                Advance = advance,
            };
        }

        // Upload to GPU
        var pixelData = new byte[atlasWidth * atlasHeight * 4];
        image.CopyPixelDataTo(pixelData);

        var texture = driver.CreateTexture(new TextureDescriptor
        {
            Width = atlasWidth,
            Height = atlasHeight,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        texture.UploadData<byte>(pixelData);

        var textureView = driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        return new FontAtlas
        {
            Texture = texture,
            TextureView = textureView,
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
            FontSize = fontSize,
            Glyphs = glyphs,
        };
    }
}
