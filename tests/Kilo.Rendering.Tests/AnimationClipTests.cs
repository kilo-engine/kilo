using System.Numerics;
using Kilo.Rendering.Resources;
using Xunit;

namespace Kilo.Rendering.Tests;

public class AnimationClipTests
{
    [Fact]
    public void Sample_EmptyClip_ReturnsIdentityTransform()
    {
        var clip = new AnimationClip
        {
            Name = "empty",
            Duration = 1.0f,
            Channels = []
        };

        var result = clip.Sample(0.5f);

        // With no channels, returns single entry with identity
        Assert.Single(result);
        Assert.Equal(Vector3.Zero, result[0].Pos);
        Assert.Equal(Quaternion.Identity, result[0].Rot);
        Assert.Equal(Vector3.One, result[0].Scale);
    }

    [Fact]
    public void Sample_SingleKeyframe_ReturnsExactValues()
    {
        var clip = new AnimationClip
        {
            Name = "single",
            Duration = 2.0f,
            Channels =
            [
                new AnimationChannel
                {
                    JointIndex = 0,
                    Keyframes =
                    [
                        new AnimationKeyframe
                        {
                            Time = 0,
                            Position = new Vector3(1, 2, 3),
                            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4),
                            Scale = new Vector3(2, 2, 2)
                        }
                    ]
                }
            ]
        };

        var result = clip.Sample(0.5f);

        Assert.Single(result);
        Assert.Equal(new Vector3(1, 2, 3), result[0].Pos);
        Assert.Equal(Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4), result[0].Rot);
        Assert.Equal(new Vector3(2, 2, 2), result[0].Scale);
    }

    [Fact]
    public void Sample_TwoKeyframes_InterpolatesPosition()
    {
        var clip = new AnimationClip
        {
            Name = "position_interp",
            Duration = 2.0f,
            Channels =
            [
                new AnimationChannel
                {
                    JointIndex = 0,
                    Keyframes =
                    [
                        new AnimationKeyframe { Time = 0, Position = new Vector3(0, 0, 0) },
                        new AnimationKeyframe { Time = 1, Position = new Vector3(1, 0, 0) }
                    ]
                }
            ]
        };

        var result = clip.Sample(0.5f);

        Assert.Equal(0.5f, result[0].Pos.X, precision: 5);
        Assert.Equal(0, result[0].Pos.Y);
        Assert.Equal(0, result[0].Pos.Z);
    }

    [Fact]
    public void Sample_TwoKeyframes_InterpolatesRotation()
    {
        var startRot = Quaternion.Identity;
        var endRot = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2); // 90 degrees

        var clip = new AnimationClip
        {
            Name = "rotation_interp",
            Duration = 2.0f,
            Channels =
            [
                new AnimationChannel
                {
                    JointIndex = 0,
                    Keyframes =
                    [
                        new AnimationKeyframe { Time = 0, Rotation = startRot },
                        new AnimationKeyframe { Time = 1, Rotation = endRot }
                    ]
                }
            ]
        };

        var result = clip.Sample(0.5f);
        var expectedRot = Quaternion.Slerp(startRot, endRot, 0.5f);

        Assert.Equal(expectedRot.X, result[0].Rot.X, precision: 5);
        Assert.Equal(expectedRot.Y, result[0].Rot.Y, precision: 5);
        Assert.Equal(expectedRot.Z, result[0].Rot.Z, precision: 5);
        Assert.Equal(expectedRot.W, result[0].Rot.W, precision: 5);
    }

    [Fact]
    public void Sample_TimeWraps_WhenLoopTrue()
    {
        var clip = new AnimationClip
        {
            Name = "looping",
            Duration = 2.0f,
            Channels =
            [
                new AnimationChannel
                {
                    JointIndex = 0,
                    Keyframes =
                    [
                        new AnimationKeyframe { Time = 0, Position = new Vector3(0, 0, 0) },
                        new AnimationKeyframe { Time = 1, Position = new Vector3(1, 0, 0) },
                        new AnimationKeyframe { Time = 2, Position = new Vector3(0, 0, 0) }
                    ]
                }
            ]
        };

        var resultAt2_5 = clip.Sample(2.5f);
        var resultAt0_5 = clip.Sample(0.5f);

        Assert.Equal(resultAt0_5[0].Pos.X, resultAt2_5[0].Pos.X, precision: 5);
    }

    [Fact]
    public void Sample_TimeClamped_WhenLoopFalse()
    {
        var clip = new AnimationClip
        {
            Name = "non_looping",
            Duration = 2.0f,
            Loop = false,
            Channels =
            [
                new AnimationChannel
                {
                    JointIndex = 0,
                    Keyframes =
                    [
                        new AnimationKeyframe { Time = 0, Position = new Vector3(0, 0, 0) },
                        new AnimationKeyframe { Time = 2, Position = new Vector3(1, 0, 0) }
                    ]
                }
            ]
        };

        var resultAt3 = clip.Sample(3f);
        var resultAt2 = clip.Sample(2f);

        // Should be clamped to end value
        Assert.Equal(1f, resultAt3[0].Pos.X, precision: 5);
        Assert.Equal(1f, resultAt2[0].Pos.X, precision: 5);
    }
}
