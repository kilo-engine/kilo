using System.Numerics;
using System.Runtime.InteropServices;
using Xunit;

namespace Kilo.Rendering.Tests;

public class LocalTransformTests
{
    [Fact]
    public void LocalTransform_IdentityValues()
    {
        var identity = LocalTransform.Identity;

        Assert.Equal(0f, identity.Position.X);
        Assert.Equal(0f, identity.Position.Y);
        Assert.Equal(0f, identity.Position.Z);

        Assert.Equal(0f, identity.Rotation.X);
        Assert.Equal(0f, identity.Rotation.Y);
        Assert.Equal(0f, identity.Rotation.Z);
        Assert.Equal(1f, identity.Rotation.W);

        Assert.Equal(1f, identity.Scale.X);
        Assert.Equal(1f, identity.Scale.Y);
        Assert.Equal(1f, identity.Scale.Z);
    }

    [Fact]
    public void LocalTransform_StructLayout()
    {
        // Verify the struct has the expected size
        // Vector3 = 12 bytes, Quaternion = 16 bytes
        // Position (12) + Rotation (16) + Scale (12) = 40 bytes
        Assert.Equal(40, Marshal.SizeOf<LocalTransform>());
    }

    [Fact]
    public void LocalTransform_CanBeModified()
    {
        var transform = LocalTransform.Identity;
        transform.Position = new Vector3(1, 2, 3);
        transform.Scale = new Vector3(2, 2, 2);

        Assert.Equal(1f, transform.Position.X);
        Assert.Equal(2f, transform.Scale.X);
    }
}
