using System.Numerics;
using Kilo.ECS;
using Parent = TinyEcs.Parent;

namespace Kilo.Rendering;

public sealed class LocalToWorldSystem
{
    public void Update(KiloWorld world)
    {
        // Pass 1: Process root entities (no Parent) first.
        var rootQuery = world.QueryBuilder()
            .With<LocalTransform>()
            .With<LocalToWorld>()
            .Without<Parent>()
            .Build();

        var rootIter = rootQuery.Iter();
        while (rootIter.Next())
        {
            var transforms = rootIter.Data<LocalTransform>(rootIter.GetColumnIndexOf<LocalTransform>());
            var worlds = rootIter.Data<LocalToWorld>(rootIter.GetColumnIndexOf<LocalToWorld>());

            for (int i = 0; i < rootIter.Count; i++)
            {
                ref readonly var t = ref transforms[i];
                worlds[i].Value =
                    Matrix4x4.CreateScale(t.Scale)
                    * Matrix4x4.CreateFromQuaternion(t.Rotation)
                    * Matrix4x4.CreateTranslation(t.Position);
            }
        }

        // Pass 2: Process child entities (with Parent).
        const int maxPasses = 8;
        var childQuery = world.QueryBuilder()
            .With<LocalTransform>()
            .With<LocalToWorld>()
            .With<Parent>()
            .Build();

        for (int pass = 0; pass < maxPasses; pass++)
        {
            var childIter = childQuery.Iter();
            while (childIter.Next())
            {
                var transforms = childIter.Data<LocalTransform>(childIter.GetColumnIndexOf<LocalTransform>());
                var worlds = childIter.Data<LocalToWorld>(childIter.GetColumnIndexOf<LocalToWorld>());
                var entities = childIter.Entities();

                for (int i = 0; i < childIter.Count; i++)
                {
                    ref readonly var t = ref transforms[i];
                    var localMatrix =
                        Matrix4x4.CreateScale(t.Scale)
                        * Matrix4x4.CreateFromQuaternion(t.Rotation)
                        * Matrix4x4.CreateTranslation(t.Position);

                    var entityId = new EntityId(entities[i].ID);
                    var parentId = new EntityId(world.Get<Parent>(entityId).Id);
                    if (world.Exists(parentId) && world.Has<LocalToWorld>(parentId))
                    {
                        ref readonly var parentWorld = ref world.Get<LocalToWorld>(parentId);
                        worlds[i].Value = localMatrix * parentWorld.Value;
                    }
                    else
                    {
                        worlds[i].Value = localMatrix;
                    }
                }
            }
        }
    }
}
