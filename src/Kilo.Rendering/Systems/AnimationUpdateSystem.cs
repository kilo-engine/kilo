using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Resources;

namespace Kilo.Rendering;

/// <summary>
/// Updates animation players: advances time and applies keyframe data to joint LocalTransforms.
/// </summary>
public sealed class AnimationUpdateSystem
{
    public void Update(KiloWorld world, float deltaTime)
    {
        var query = world.QueryBuilder()
            .With<Skeleton>()
            .With<AnimationPlayer>()
            .Build();

        var iter = query.Iter();
        while (iter.Next())
        {
            var skeletons = iter.Data<Skeleton>(iter.GetColumnIndexOf<Skeleton>());
            var players = iter.Data<AnimationPlayer>(iter.GetColumnIndexOf<AnimationPlayer>());

            for (int i = 0; i < iter.Count; i++)
            {
                if (!players[i].IsPlaying) continue;

                var player = players[i];
                player.Time += deltaTime;
                players[i] = player;

                var skeleton = skeletons[i];
                if (skeleton.Data == null || skeleton.JointEntities.Length == 0) continue;

                // Get clip (for now, assume clips are stored on skeleton or a separate resource)
                // This will be expanded when GLTF loading provides clips
            }
        }
    }
}
