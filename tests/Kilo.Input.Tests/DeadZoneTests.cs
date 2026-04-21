using System.Numerics;
using Kilo.Input.Processing;
using Xunit;

namespace Kilo.Input.Tests;

public class DeadZoneTests
{
    [Fact]
    public void RadialDeadZone_ZeroInput_ReturnsZero()
    {
        Assert.Equal(Vector2.Zero, DeadZone.ApplyRadial(0f, 0f, 0.15f));
    }

    [Fact]
    public void RadialDeadZone_BelowThreshold_ReturnsZero()
    {
        Assert.Equal(Vector2.Zero, DeadZone.ApplyRadial(0.1f, 0f, 0.15f));
    }

    [Fact]
    public void RadialDeadZone_AboveThreshold_NonZero()
    {
        var result = DeadZone.ApplyRadial(0.5f, 0f, 0.15f);
        Assert.NotEqual(Vector2.Zero, result);
        Assert.True(result.X > 0f);
    }

    [Fact]
    public void RadialDeadZone_LinearRemap()
    {
        // Input magnitude 0.575 with deadZone 0.15 → remapped magnitude ≈ 0.5
        var result = DeadZone.ApplyRadial(0.575f, 0f, 0.15f);
        Assert.True(MathF.Abs(result.X - 0.5f) < 0.05f, $"Expected ~0.5, got {result.X}");
    }

    [Fact]
    public void RadialDeadZone_PreservesDirection()
    {
        var result = DeadZone.ApplyRadial(0.5f, 0.5f, 0.15f);
        float angle = MathF.Atan2(result.Y, result.X);
        float expected = MathF.Atan2(1f, 1f);
        Assert.True(MathF.Abs(angle - expected) < 0.01f);
    }

    [Fact]
    public void Hysteresis_BelowPressThreshold_NotPressed()
    {
        Assert.False(DeadZone.ApplyHysteresis(0.5f, false, 0.75f, 0.3f));
    }

    [Fact]
    public void Hysteresis_AbovePressThreshold_Presses()
    {
        Assert.True(DeadZone.ApplyHysteresis(0.8f, false, 0.75f, 0.3f));
    }

    [Fact]
    public void Hysteresis_AboveReleaseThreshold_StaysPressed()
    {
        Assert.True(DeadZone.ApplyHysteresis(0.5f, true, 0.75f, 0.3f));
    }

    [Fact]
    public void Hysteresis_BelowReleaseThreshold_Releases()
    {
        Assert.False(DeadZone.ApplyHysteresis(0.2f, true, 0.75f, 0.55f));
    }
}
