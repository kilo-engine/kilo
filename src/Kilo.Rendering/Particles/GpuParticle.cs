using System.Numerics;
using System.Runtime.InteropServices;

namespace Kilo.Rendering.Particles;

/// <summary>
/// GPU particle data. 64 bytes, aligned for storage buffers.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuParticle
{
    public Vector3 Position;   // 12
    public float Alive;        // 4  (1.0 = alive, 0.0 = dead)
    public Vector3 Velocity;   // 12
    public float Age;          // 4
    public Vector4 Color;      // 16
    public float Size;         // 4
    public float Lifetime;     // 4
    private Vector2 _pad;      // 8
}
