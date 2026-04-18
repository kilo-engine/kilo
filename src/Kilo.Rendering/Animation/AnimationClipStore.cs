namespace Kilo.Rendering.Animation;

/// <summary>
/// Stores animation clips keyed by entity, enabling the AnimationUpdateSystem
/// to look up clips by the owner entity's AnimationPlayer.ClipIndex.
/// </summary>
public sealed class AnimationClipStore
{
    /// <summary>Per-entity animation clip lists. Key = entity ID as ulong.</summary>
    public Dictionary<ulong, List<AnimationClip>> EntityClips = [];
}
