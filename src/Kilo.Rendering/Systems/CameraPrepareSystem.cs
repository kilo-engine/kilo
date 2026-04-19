using Kilo.ECS;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Prepares camera GPU data. Stores result in GpuSceneData.PendingCamera
/// for LightPrepareSystem to finalize with light count.
/// </summary>
public sealed class CameraPrepareSystem
{
    public void Update(KiloWorld world)
    {
        var scene = world.GetResource<GpuSceneData>();

        var cameraQuery = world.QueryBuilder()
            .With<Camera>()
            .With<LocalTransform>()
            .Build();

        CameraData cameraData = default;
        bool cameraFound = false;

        var cameraIter = cameraQuery.Iter();
        while (cameraIter.Next())
        {
            var cameras = cameraIter.Data<Camera>(cameraIter.GetColumnIndexOf<Camera>());
            var transforms = cameraIter.Data<LocalTransform>(cameraIter.GetColumnIndexOf<LocalTransform>());

            for (int i = 0; i < cameraIter.Count; i++)
            {
                if (cameras[i].IsActive || !cameraFound)
                {
                    cameraData.View = cameras[i].ViewMatrix;
                    cameraData.Projection = cameras[i].ProjectionMatrix;
                    cameraData.Position = transforms[i].Position;
                    cameraFound = true;
                    if (cameras[i].IsActive) break;
                }
            }
        }

        scene.PendingCamera = cameraData;
    }
}
