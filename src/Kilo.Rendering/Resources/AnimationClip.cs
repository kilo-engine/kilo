using System.Numerics;

namespace Kilo.Rendering.Resources;

/// <summary>
/// A single keyframe for a joint's transform property.
/// </summary>
public struct AnimationKeyframe
{
    public float Time;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

/// <summary>
/// Animation channel: keyframes for a single joint.
/// </summary>
public sealed class AnimationChannel
{
    /// <summary>Joint index this channel affects.</summary>
    public int JointIndex;

    /// <summary>Sorted keyframes (by time).</summary>
    public List<AnimationKeyframe> Keyframes = [];
}

/// <summary>
/// An animation clip containing per-joint keyframe data.
/// </summary>
public sealed class AnimationClip
{
    /// <summary>Name of the animation clip.</summary>
    public string Name = "";

    /// <summary>Total duration in seconds.</summary>
    public float Duration;

    /// <summary>Per-joint animation channels.</summary>
    public List<AnimationChannel> Channels = [];

    /// <summary>
    /// Samples the animation at the given time, outputting local transforms per joint.
    /// Returns an array of (position, rotation, scale) for each joint.
    /// </summary>
    public (Vector3 Pos, Quaternion Rot, Vector3 Scale)[] Sample(float time)
    {
        // Clamp or wrap time
        if (Loop)
        {
            time = time % Duration;
            if (time < 0) time += Duration;
        }
        else
        {
            time = Math.Clamp(time, 0, Duration);
        }

        // Initialize output with identity
        int maxJoint = 0;
        foreach (var ch in Channels)
            maxJoint = Math.Max(maxJoint, ch.JointIndex);

        var result = new (Vector3, Quaternion, Vector3)[maxJoint + 1];
        for (int i = 0; i < result.Length; i++)
            result[i] = (Vector3.Zero, Quaternion.Identity, Vector3.One);

        // Sample each channel
        foreach (var channel in Channels)
        {
            if (channel.Keyframes.Count == 0) continue;

            if (channel.Keyframes.Count == 1)
            {
                var kf = channel.Keyframes[0];
                result[channel.JointIndex] = (kf.Position, kf.Rotation, kf.Scale);
                continue;
            }

            // Find surrounding keyframes
            int idx = 0;
            for (int i = 0; i < channel.Keyframes.Count - 1; i++)
            {
                if (time >= channel.Keyframes[i].Time && time < channel.Keyframes[i + 1].Time)
                {
                    idx = i;
                    break;
                }
                if (i == channel.Keyframes.Count - 2) idx = i;
            }

            var k0 = channel.Keyframes[idx];
            var k1 = channel.Keyframes[Math.Min(idx + 1, channel.Keyframes.Count - 1)];

            float t = 0;
            if (k1.Time > k0.Time)
                t = (time - k0.Time) / (k1.Time - k0.Time);

            result[channel.JointIndex] = (
                Vector3.Lerp(k0.Position, k1.Position, t),
                Quaternion.Slerp(k0.Rotation, k1.Rotation, t),
                Vector3.Lerp(k0.Scale, k1.Scale, t)
            );
        }

        return result;
    }

    /// <summary>Whether to loop (used by Sample method).</summary>
    public bool Loop { get; set; } = true;
}
