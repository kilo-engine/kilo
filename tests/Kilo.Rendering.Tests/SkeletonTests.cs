using System.Numerics;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Xunit;

namespace Kilo.Rendering.Tests;

public class SkeletonTests
{
    [Fact]
    public void JointInfo_DefaultValues()
    {
        var joint = new JointInfo();

        Assert.Equal(-1, joint.ParentIndex);
        Assert.Equal(Matrix4x4.Identity, joint.InverseBindMatrix);
        Assert.Empty(joint.Name);
    }

    [Fact]
    public void SkeletonData_MaxJoints_Is64()
    {
        Assert.Equal(64, SkeletonData.MaxJoints);
    }

    [Fact]
    public void SkeletonData_JointCount()
    {
        var skeleton = new SkeletonData
        {
            Joints = new JointInfo[5]
        };

        Assert.Equal(5, skeleton.JointCount);
    }
}
