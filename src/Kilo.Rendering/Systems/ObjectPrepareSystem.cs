using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Prepares per-frame object GPU data and draw lists for static and skinned meshes.
/// </summary>
public sealed class ObjectPrepareSystem
{
    public void Update(KiloWorld world)
    {
        var scene = world.GetResource<GpuSceneData>();
        var context = world.GetResource<RenderContext>();

        int maxObjects = (int)(scene.ObjectDataBuffer.Size / (nuint)ObjectData.Size);
        var objectData = new ObjectData[maxObjects];
        var drawData = new List<DrawData>();

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
                if (drawData.Count >= maxObjects) break;

                int index = drawData.Count;
                objectData[index].Model = transforms[i].Value;
                objectData[index].MaterialId = renderers[i].MaterialHandle;

                if (renderers[i].MaterialHandle >= 0 && renderers[i].MaterialHandle < context.Materials.Count)
                {
                    var material = context.Materials[renderers[i].MaterialHandle];
                    objectData[index].BaseColor = material.BaseColor;
                    objectData[index].UseTexture = material.UseTexture ? 1 : 0;
                }
                else
                {
                    objectData[index].BaseColor = Vector4.One;
                    objectData[index].UseTexture = 0;
                }

                drawData.Add(new DrawData
                {
                    MeshHandle = renderers[i].MeshHandle,
                    MaterialId = renderers[i].MaterialHandle,
                    IsSkinned = false,
                });
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
                if (drawData.Count >= maxObjects) break;

                int index = drawData.Count;
                objectData[index].Model = transforms[i].Value;
                objectData[index].MaterialId = renderers[i].MaterialHandle;

                if (renderers[i].MaterialHandle >= 0 && renderers[i].MaterialHandle < context.Materials.Count)
                {
                    var material = context.Materials[renderers[i].MaterialHandle];
                    objectData[index].BaseColor = material.BaseColor;
                    objectData[index].UseTexture = material.UseTexture ? 1 : 0;
                }
                else
                {
                    objectData[index].BaseColor = Vector4.One;
                    objectData[index].UseTexture = 0;
                }

                drawData.Add(new DrawData
                {
                    MeshHandle = renderers[i].MeshHandle,
                    MaterialId = renderers[i].MaterialHandle,
                    IsSkinned = true,
                    JointBindingSet = renderers[i].JointBindingSet,
                });
            }
        }

        scene.SetDrawData(drawData.ToArray(), drawData.Count);
        if (drawData.Count > 0)
        {
            scene.ObjectDataBuffer.UploadData<ObjectData>(objectData.AsSpan(0, drawData.Count));
        }
    }
}
