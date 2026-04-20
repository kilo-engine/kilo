namespace Kilo.Rendering.Particles;

public sealed class Gradient<T>
{
    public List<(float Time, T Value)> Keys { get; set; } = [];

    public T Evaluate(float t)
    {
        if (Keys.Count == 0) return default!;
        if (Keys.Count == 1) return Keys[0].Value;

        t = Math.Clamp(t, 0f, 1f);

        for (int i = 0; i < Keys.Count - 1; i++)
        {
            var (t0, v0) = Keys[i];
            var (t1, v1) = Keys[i + 1];
            if (t >= t0 && t <= t1)
            {
                float localT = t0 < t1 ? (t - t0) / (t1 - t0) : 0f;
                return LerpValue(v0, v1, localT);
            }
        }

        return Keys[^1].Value;
    }

    private static T LerpValue(T a, T b, float t)
    {
        if (typeof(T) == typeof(float))
            return (T)(object)(((float)(object)a!) + ((float)(object)b! - (float)(object)a!) * t);
        if (typeof(T) == typeof(Color4))
            return (T)(object)Color4.Lerp((Color4)(object)a!, (Color4)(object)b!, t);
        return t < 0.5f ? a : b;
    }

    public static Gradient<float> FromValues(params (float time, float value)[] keys)
    {
        var g = new Gradient<float>();
        foreach (var (time, value) in keys)
            g.Keys.Add((time, value));
        return g;
    }

    public static Gradient<Color4> FromColors(params (float time, Color4 color)[] keys)
    {
        var g = new Gradient<Color4>();
        foreach (var (time, color) in keys)
            g.Keys.Add((time, color));
        return g;
    }
}
