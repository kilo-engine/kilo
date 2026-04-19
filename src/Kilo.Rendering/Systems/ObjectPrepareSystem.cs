using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Prepares per-frame object GPU data and draw lists for static and skinned meshes.
/// Separates opaque and transparent objects; sorts transparent objects back-to-front.
/// </summary>
public sealed class ObjectPrepareSystem
{
    public void Update(KiloWorld world)
    {
        var scene = world.GetResource<GpuSceneData>();
        var context = world.GetResource<RenderContext>();

        int maxObjects = (int)(scene.ObjectDataBuffer.Size / (nuint)ObjectData.Size);
        var collector = new DrawCollector(maxObjects);

        // Static meshes
        var meshQuery = world.QueryBuilder()
            .With<MeshRenderer>()
            .With<LocalToWorld>()
            .Without<Culled>()
            .Without<SkinnedMeshRenderer>()
            .Build();

        var meshIter = meshQuery.Iter();
        while (meshIter.Next())
        {
            var renderers = meshIter.Data<MeshRenderer>(meshIter.GetColumnIndexOf<MeshRenderer>());
            var transforms = meshIter.Data<LocalToWorld>(meshIter.GetColumnIndexOf<LocalToWorld>());

            for (int i = 0; i < meshIter.Count; i++)
            {
                if (collector.IsFull) break;
                collector.Add(renderers[i].MaterialHandle, renderers[i].MeshHandle,
                    isSkinned: false, jointBindingSet: null,
                    transforms[i].Value, context);
            }
        }

        // Skinned meshes
        var skinnedQuery = world.QueryBuilder()
            .With<SkinnedMeshRenderer>()
            .With<LocalToWorld>()
            .Without<Culled>()
            .Build();

        var skinnedIter = skinnedQuery.Iter();
        while (skinnedIter.Next())
        {
            var renderers = skinnedIter.Data<SkinnedMeshRenderer>(skinnedIter.GetColumnIndexOf<SkinnedMeshRenderer>());
            var transforms = skinnedIter.Data<LocalToWorld>(skinnedIter.GetColumnIndexOf<LocalToWorld>());

            for (int i = 0; i < skinnedIter.Count; i++)
            {
                if (collector.IsFull) break;
                collector.Add(renderers[i].MaterialHandle, renderers[i].MeshHandle,
                    isSkinned: true, jointBindingSet: renderers[i].JointBindingSet,
                    transforms[i].Value, context);
            }
        }

        // Sort transparent objects back-to-front with stable secondary key
        collector.SortTransparent(scene.PendingCamera.Position);

        // Build GPU data
        var (draws, objectData, totalCount, opaqueCount) = collector.Build(context);
        scene.SetDrawData(draws, totalCount, opaqueCount);
        if (totalCount > 0)
        {
            scene.ObjectDataBuffer.UploadData<ObjectData>(objectData.AsSpan(0, totalCount));
        }
    }

    /// <summary>
    /// Collects opaque and transparent draw calls, then builds sorted GPU data.
    /// </summary>
    private sealed class DrawCollector
    {
        private readonly int _capacity;
        private readonly List<(DrawData Draw, Matrix4x4 World)> _opaque = [];
        private readonly List<(DrawData Draw, Matrix4x4 World)> _transparent = [];

        public bool IsFull => _opaque.Count + _transparent.Count >= _capacity;

        public DrawCollector(int capacity) => _capacity = capacity;

        public void Add(int materialHandle, int meshHandle, bool isSkinned,
            IBindingSet? jointBindingSet, Matrix4x4 world, RenderContext context)
        {
            bool isTransparent = materialHandle >= 0 && materialHandle < context.Materials.Count
                && context.Materials[materialHandle].IsTransparent;

            var draw = new DrawData
            {
                MeshHandle = meshHandle,
                MaterialId = materialHandle,
                IsSkinned = isSkinned,
                IsTransparent = isTransparent,
                JointBindingSet = jointBindingSet,
            };

            (isTransparent ? _transparent : _opaque).Add((draw, world));
        }

        /// <summary>
        /// Sorts transparent objects back-to-front. Uses MaterialId as secondary key
        /// to ensure stable ordering for objects at the same distance.
        /// </summary>
        public void SortTransparent(Vector3 cameraPosition)
        {
            _transparent.Sort((a, b) =>
            {
                int cmp = Vector3.DistanceSquared(b.World.Translation, cameraPosition)
                    .CompareTo(Vector3.DistanceSquared(a.World.Translation, cameraPosition));
                return cmp != 0 ? cmp : a.Draw.MaterialId.CompareTo(b.Draw.MaterialId);
            });
        }

        /// <summary>
        /// Combines opaque then sorted transparent draws, and populates ObjectData arrays.
        /// </summary>
        public (DrawData[] Draws, ObjectData[] ObjectData, int Count, int OpaqueCount) Build(RenderContext context)
        {
            int opaqueCount = _opaque.Count;
            int total = Math.Min(opaqueCount + _transparent.Count, _capacity);

            var draws = new DrawData[total];
            var objectData = new ObjectData[total];
            int idx = 0;

            foreach (var (draw, world) in _opaque)
            {
                if (idx >= total) break;
                PopulateObject(ref objectData[idx], world, draw.MaterialId, context);
                draws[idx++] = draw;
            }

            foreach (var (draw, world) in _transparent)
            {
                if (idx >= total) break;
                PopulateObject(ref objectData[idx], world, draw.MaterialId, context);
                draws[idx++] = draw;
            }

            return (draws, objectData, idx, opaqueCount);
        }

        private static void PopulateObject(ref ObjectData data, Matrix4x4 world,
            int materialHandle, RenderContext context)
        {
            data.Model = world;
            data.MaterialId = materialHandle;

            if (materialHandle >= 0 && materialHandle < context.Materials.Count)
            {
                var material = context.Materials[materialHandle];
                data.BaseColor = material.BaseColor;
                data.UseTexture = material.UseTexture ? 1 : 0;
                data.Metallic = material.Metallic;
                data.Roughness = material.Roughness;
                data.UseNormalMap = material.UseNormalMap ? 1 : 0;
            }
            else
            {
                data.BaseColor = Vector4.One;
                data.UseTexture = 0;
            }
        }
    }
}
