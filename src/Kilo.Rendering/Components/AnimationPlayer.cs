namespace Kilo.Rendering;

/// <summary>
/// Animation player component. Controls playback of an animation clip.
/// </summary>
public struct AnimationPlayer
{
    /// <summary>Index of the animation clip to play.</summary>
    public int ClipIndex;

    /// <summary>Current playback time in seconds.</summary>
    public float Time;

    /// <summary>Whether the animation is currently playing.</summary>
    public bool IsPlaying = true;

    /// <summary>Whether to loop the animation.</summary>
    public bool Loop = true;

    public AnimationPlayer() { }
}
