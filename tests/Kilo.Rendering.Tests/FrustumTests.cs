using System.Numerics;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Xunit;

namespace Kilo.Rendering.Tests;

public class FrustumTests
{
    [Fact]
    public void FromViewProjection_ProducesNormalizedPlanes()
    {
        var vp = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
                 * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f);

        var frustum = Frustum.FromViewProjection(vp);

        // All plane normals should have unit length
        Assert.Equal(1f, frustum.Left.Normal.Length(), 3);
        Assert.Equal(1f, frustum.Right.Normal.Length(), 3);
        Assert.Equal(1f, frustum.Top.Normal.Length(), 3);
        Assert.Equal(1f, frustum.Bottom.Normal.Length(), 3);
        Assert.Equal(1f, frustum.Near.Normal.Length(), 3);
        Assert.Equal(1f, frustum.Far.Normal.Length(), 3);
    }

    [Fact]
    public void IntersectsAABB_OriginInsideFrustum_ReturnsTrue()
    {
        // Camera at (0,0,5) looking at origin
        var vp = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
                 * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f);
        var frustum = Frustum.FromViewProjection(vp);

        // Small box at origin — should be visible
        var result = frustum.IntersectsAABB(new Vector3(-0.5f), new Vector3(0.5f));
        Assert.True(result);
    }

    [Fact]
    public void IntersectsAABB_FarBehindCamera_ReturnsFalse()
    {
        var vp = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
                 * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f);
        var frustum = Frustum.FromViewProjection(vp);

        // Box far behind camera and outside the far plane
        var result = frustum.IntersectsAABB(new Vector3(-1, -1, 200), new Vector3(1, 1, 202));
        Assert.False(result);
    }

    [Fact]
    public void IntersectsAABB_FarToTheSide_ReturnsFalse()
    {
        var vp = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
                 * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f);
        var frustum = Frustum.FromViewProjection(vp);

        // Box far to the right
        var result = frustum.IntersectsAABB(new Vector3(100, -1, 0), new Vector3(102, 1, 2));
        Assert.False(result);
    }

    [Fact]
    public void IntersectsAABB_LargeBoxAlwaysVisible()
    {
        var vp = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
                 * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, 1f, 0.1f, 100f);
        var frustum = Frustum.FromViewProjection(vp);

        // Huge box encompassing entire frustum
        var result = frustum.IntersectsAABB(new Vector3(-500), new Vector3(500));
        Assert.True(result);
    }

    [Fact]
    public void FromViewProjection_Orthographic_ProducesValidFrustum()
    {
        var vp = Matrix4x4.CreateLookAt(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ)
                 * Matrix4x4.CreateOrthographicOffCenter(-10, 10, -10, 10, 0.1f, 50f);
        var frustum = Frustum.FromViewProjection(vp);

        // Origin should be visible from above with orthographic
        Assert.True(frustum.IntersectsAABB(new Vector3(-1), new Vector3(1)));

        // Outside orthographic bounds should be culled
        Assert.False(frustum.IntersectsAABB(new Vector3(15, 0, 0), new Vector3(16, 1, 1)));
    }
}
