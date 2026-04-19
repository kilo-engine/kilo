using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// CPU-side view frustum culling. Adds/removes the <see cref="Culled"/> tag on entities.
/// Runs in KiloStage.PostUpdate before object/light prepare systems.
/// </summary>
public sealed class FrustumCullingSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();

        // Build frustum from the active camera
        var cameraQuery = world.QueryBuilder()
            .With<Camera>()
            .With<LocalTransform>()
            .Build();

        Frustum frustum = default;
        bool frustumBuilt = false;

        var camIter = cameraQuery.Iter();
        while (camIter.Next())
        {
            var cameras = camIter.Data<Camera>(camIter.GetColumnIndexOf<Camera>());
            if (cameras[0].IsActive || !frustumBuilt)
            {
                var vp = cameras[0].ViewMatrix * cameras[0].ProjectionMatrix;
                frustum = Frustum.FromViewProjection(vp);
                frustumBuilt = true;
                if (cameras[0].IsActive) break;
            }
        }

        if (!frustumBuilt) return;

        // Test each mesh renderer against the frustum
        var meshQuery = world.QueryBuilder()
            .With<MeshRenderer>()
            .With<LocalToWorld>()
            .Build();

        var meshIter = meshQuery.Iter();
        while (meshIter.Next())
        {
            var renderers = meshIter.Data<MeshRenderer>(meshIter.GetColumnIndexOf<MeshRenderer>());
            var transforms = meshIter.Data<LocalToWorld>(meshIter.GetColumnIndexOf<LocalToWorld>());
            var entities = meshIter.Entities();

            for (int i = 0; i < meshIter.Count; i++)
            {
                var entityId = new EntityId(entities[i].ID);

                if (renderers[i].MeshHandle < 0)
                {
                    // Invalid mesh → mark culled
                    if (!world.Has<Culled>(entityId))
                        world.Set<Culled>(entityId);
                    continue;
                }

                // Transform local AABB to world space using actual mesh bounds
                var mesh = context.Meshes[renderers[i].MeshHandle];
                var worldAABB = TransformAABB(mesh.Bounds, transforms[i].Value);
                bool visible = frustum.IntersectsAABB(worldAABB.Min, worldAABB.Max);

                if (visible)
                    world.Unset<Culled>(entityId);
                else if (!world.Has<Culled>(entityId))
                    world.Set<Culled>(entityId);
            }
        }
    }

    private static (Vector3 Min, Vector3 Max) TransformAABB((Vector3 Min, Vector3 Max) local, Matrix4x4 world)
    {
        var min = local.Min;
        var max = local.Max;

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        for (int corner = 0; corner < 8; corner++)
        {
            float x = (corner & 1) == 0 ? min.X : max.X;
            float y = (corner & 2) == 0 ? min.Y : max.Y;
            float z = (corner & 4) == 0 ? min.Z : max.Z;

            var worldPos = Vector3.Transform(new Vector3(x, y, z), world);

            if (worldPos.X < minX) minX = worldPos.X;
            if (worldPos.Y < minY) minY = worldPos.Y;
            if (worldPos.Z < minZ) minZ = worldPos.Z;
            if (worldPos.X > maxX) maxX = worldPos.X;
            if (worldPos.Y > maxY) maxY = worldPos.Y;
            if (worldPos.Z > maxZ) maxZ = worldPos.Z;
        }

        return (new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
    }
}
