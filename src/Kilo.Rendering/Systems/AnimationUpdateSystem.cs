using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Resources;

namespace Kilo.Rendering;

/// <summary>
/// Updates animation players: advances time and applies keyframe data to joint LocalTransforms.
/// Requires an <see cref="AnimationClipStore"/> resource to look up clips per entity.
/// </summary>
public sealed class AnimationUpdateSystem
{
    public void Update(KiloWorld world, float deltaTime)
    {
        var clipStore = world.GetResource<AnimationClipStore>();
        if (clipStore == null) return;

        var query = world.QueryBuilder()
            .With<Skeleton>()
            .With<AnimationPlayer>()
            .Build();

        var iter = query.Iter();
        while (iter.Next())
        {
            var skeletons = iter.Data<Skeleton>(iter.GetColumnIndexOf<Skeleton>());
            var players = iter.Data<AnimationPlayer>(iter.GetColumnIndexOf<AnimationPlayer>());
            var entities = iter.Entities();

            for (int i = 0; i < iter.Count; i++)
            {
                if (!players[i].IsPlaying) continue;

                var skeleton = skeletons[i];
                if (skeleton.Data == null || skeleton.JointEntities.Length == 0) continue;

                // Look up clips for this entity
                var entityId = (ulong)entities[i].ID;
                if (!clipStore.EntityClips.TryGetValue(entityId, out var clips)) continue;
                if (clips.Count == 0) continue;

                var player = players[i];

                // Validate clip index
                int clipIdx = Math.Clamp(player.ClipIndex, 0, clips.Count - 1);
                var clip = clips[clipIdx];

                // Advance time
                player.Time += deltaTime;
                if (player.Loop && clip.Duration > 0)
                {
                    player.Time %= clip.Duration;
                }
                else if (player.Time >= clip.Duration)
                {
                    player.Time = clip.Duration;
                    player.IsPlaying = false;
                }
                players[i] = player;

                // Sample the clip at current time
                var samples = clip.Sample(player.Time);

                // Apply transforms to joint entities
                for (int j = 0; j < samples.Length && j < skeleton.JointEntities.Length; j++)
                {
                    var jointId = new EntityId((ulong)skeleton.JointEntities[j]);
                    if (!world.Exists(jointId) || !world.Has<LocalTransform>(jointId)) continue;

                    var (pos, rot, scale) = samples[j];
                    world.Set(jointId, new LocalTransform
                    {
                        Position = pos,
                        Rotation = rot,
                        Scale = scale
                    });
                }
            }
        }
    }
}
