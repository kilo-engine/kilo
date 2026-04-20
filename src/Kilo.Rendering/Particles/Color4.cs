namespace Kilo.Rendering.Particles;

public struct Color4
{
    public float R, G, B, A;

    public Color4(float r, float g, float b, float a = 1f)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Color4 White => new(1f, 1f, 1f, 1f);
    public static Color4 Transparent => new(0f, 0f, 0f, 0f);

    public static Color4 Lerp(Color4 a, Color4 b, float t)
    {
        return new Color4(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );
    }
}
