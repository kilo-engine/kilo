using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;

namespace Kilo.Rendering;

/// <summary>
/// Computes final joint matrices and uploads them to GPU for skinned mesh rendering.
/// Runs after AnimationUpdateSystem.
/// </summary>
public sealed class SkinnedMeshPrepareSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;

        var query = world.QueryBuilder()
            .With<SkinnedMeshRenderer>()
            .With<Skeleton>()
            .With<LocalToWorld>()
            .Build();

        var iter = query.Iter();
        while (iter.Next())
        {
            var renderers = iter.Data<SkinnedMeshRenderer>(iter.GetColumnIndexOf<SkinnedMeshRenderer>());
            var skeletons = iter.Data<Skeleton>(iter.GetColumnIndexOf<Skeleton>());
            var worldMatrices = iter.Data<LocalToWorld>(iter.GetColumnIndexOf<LocalToWorld>());

            for (int i = 0; i < iter.Count; i++)
            {
                var skeleton = skeletons[i];
                var renderer = renderers[i];

                if (skeleton.Data?.Joints == null || skeleton.JointEntities.Length == 0) continue;

                int jointCount = skeleton.Data.JointCount;
                var jointMatrices = new Matrix4x4[SkeletonData.MaxJoints];

                // Compute world matrix for each joint
                for (int j = 0; j < jointCount; j++)
                {
                    // Get joint's local transform from its entity
                    ulong entityId = (ulong)skeleton.JointEntities[j];
                    var jointEntity = new EntityId(entityId);

                    Matrix4x4 jointWorldMatrix = Matrix4x4.Identity;
                    if (world.Exists(jointEntity) && world.Has<LocalToWorld>(jointEntity))
                    {
                        ref readonly var jointLTW = ref world.Get<LocalToWorld>(jointEntity);
                        jointWorldMatrix = jointLTW.Value;
                    }

                    // Final joint matrix = jointWorld * inverseBindMatrix
                    jointMatrices[j] = jointWorldMatrix * skeleton.Data.Joints[j].InverseBindMatrix;
                }

                // Upload to GPU
                if (renderer.JointMatrixBuffer == null)
                {
                    renderer.JointMatrixBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
                    {
                        Size = (nuint)(SkeletonData.MaxJoints * 64), // 64 bytes per mat4x4
                        Usage = RenderGraph.BufferUsage.Uniform | RenderGraph.BufferUsage.CopyDst,
                    });
                }

                renderer.JointMatrixBuffer.UploadData<Matrix4x4>(jointMatrices.AsSpan().Slice(0, jointCount));
                renderers[i] = renderer;
            }
        }
    }
}
