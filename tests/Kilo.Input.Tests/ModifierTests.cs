using System.Numerics;
using Kilo.Input.Modifiers;
using Xunit;

namespace Kilo.Input.Tests;

public class ModifierTests
{
    [Fact]
    public void ScaleModifier_Float()
    {
        var mod = new ScaleModifier { Factor = 2.5f };
        Assert.Equal(5.0f, mod.ModifyFloat(2.0f, 0.016f));
    }

    [Fact]
    public void ScaleModifier_Vector2()
    {
        var mod = new ScaleModifier { Factor = 3.0f };
        var result = mod.ModifyVector2(new Vector2(1f, -1f), 0f);
        Assert.Equal(new Vector2(3f, -3f), result);
    }

    [Fact]
    public void NegateModifier_Float()
    {
        var mod = new NegateModifier();
        Assert.Equal(-0.5f, mod.ModifyFloat(0.5f, 0f));
    }

    [Fact]
    public void NegateModifier_Vector2_BothAxes()
    {
        var mod = new NegateModifier { NegateX = true, NegateY = true };
        var result = mod.ModifyVector2(new Vector2(1f, -1f), 0f);
        Assert.Equal(new Vector2(-1f, 1f), result);
    }

    [Fact]
    public void NegateModifier_Vector2_XOnly()
    {
        var mod = new NegateModifier { NegateX = true, NegateY = false };
        var result = mod.ModifyVector2(new Vector2(1f, 1f), 0f);
        Assert.Equal(new Vector2(-1f, 1f), result);
    }

    [Fact]
    public void DeadZoneModifier_Float_BelowLower_ReturnsZero()
    {
        var mod = new DeadZoneModifier { Lower = 0.2f, Upper = 0.9f };
        Assert.Equal(0f, mod.ModifyFloat(0.1f, 0f));
    }

    [Fact]
    public void DeadZoneModifier_Float_AboveUpper_ReturnsOne()
    {
        var mod = new DeadZoneModifier { Lower = 0.2f, Upper = 0.9f };
        Assert.Equal(1f, mod.ModifyFloat(1.0f, 0f));
    }

    [Fact]
    public void DeadZoneModifier_Float_InRange_Remap()
    {
        var mod = new DeadZoneModifier { Lower = 0.2f, Upper = 0.9f };
        // (0.55 - 0.2) / (0.9 - 0.2) = 0.5
        float result = mod.ModifyFloat(0.55f, 0f);
        Assert.True(MathF.Abs(result - 0.5f) < 0.001f, $"Expected 0.5, got {result}");
    }

    [Fact]
    public void DeadZoneModifier_Vector2_BelowThreshold_ReturnsZero()
    {
        var mod = new DeadZoneModifier { Lower = 0.2f };
        Assert.Equal(Vector2.Zero, mod.ModifyVector2(new Vector2(0.1f, 0f), 0f));
    }

    [Fact]
    public void ScaleByDeltaModifier_Float()
    {
        var mod = new ScaleByDeltaModifier();
        Assert.Equal(0.016f, mod.ModifyFloat(1.0f, 0.016f));
    }

    [Fact]
    public void ScaleByDeltaModifier_Vector2()
    {
        var mod = new ScaleByDeltaModifier();
        var result = mod.ModifyVector2(new Vector2(5f, 5f), 0.016f);
        Assert.True(MathF.Abs(result.X - 0.08f) < 0.001f);
        Assert.True(MathF.Abs(result.Y - 0.08f) < 0.001f);
    }
}
