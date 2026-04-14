using Kilo.Rendering;
using Xunit;

namespace Kilo.Rendering.Tests;

public class AnimationPlayerTests
{
    [Fact]
    public void AnimationPlayer_DefaultPlays()
    {
        var player = new AnimationPlayer();

        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void AnimationPlayer_DefaultLoops()
    {
        var player = new AnimationPlayer();

        Assert.True(player.Loop);
    }

    [Fact]
    public void AnimationPlayer_InitialTime()
    {
        var player = new AnimationPlayer();

        Assert.Equal(0f, player.Time);
    }
}
