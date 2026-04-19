using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Prepares light GPU data and finalizes camera upload with light count.
/// Must run after CameraPrepareSystem.
/// </summary>
public sealed class LightPrepareSystem
{
    public void Update(KiloWorld world)
    {
        var scene = world.GetResource<GpuSceneData>();

        var lights = new List<LightData>();

        var dirLightQuery = world.QueryBuilder()
            .With<DirectionalLight>()
            .Build();

        var dirIter = dirLightQuery.Iter();
        while (dirIter.Next())
        {
            var dirs = dirIter.Data<DirectionalLight>(dirIter.GetColumnIndexOf<DirectionalLight>());
            for (int i = 0; i < dirIter.Count; i++)
            {
                lights.Add(new LightData
                {
                    DirectionOrPosition = dirs[i].Direction,
                    Color = dirs[i].Color,
                    Intensity = dirs[i].Intensity,
                    Range = 0.0f,
                    LightType = 0,
                });
            }
        }

        var pointLightQuery = world.QueryBuilder()
            .With<PointLight>()
            .Build();

        var pointIter = pointLightQuery.Iter();
        while (pointIter.Next())
        {
            var points = pointIter.Data<PointLight>(pointIter.GetColumnIndexOf<PointLight>());
            for (int i = 0; i < pointIter.Count; i++)
            {
                lights.Add(new LightData
                {
                    DirectionOrPosition = points[i].Position,
                    Color = points[i].Color,
                    Intensity = points[i].Intensity,
                    Range = points[i].Range,
                    LightType = 1,
                });
            }
        }

        scene.SetLightCount(lights.Count);

        // Finalize camera data with light count and upload
        var cameraData = scene.PendingCamera;
        cameraData.LightCount = lights.Count;
        var cameraArray = new CameraData[1];
        cameraArray[0] = cameraData;
        scene.CameraBuffer.UploadData<CameraData>(cameraArray.AsSpan());

        if (lights.Count > 0)
        {
            int maxLights = (int)(scene.LightBuffer.Size / (nuint)Marshal.SizeOf<LightData>());
            int uploadCount = Math.Min(lights.Count, maxLights);
            var lightArray = lights.ToArray();
            scene.LightBuffer.UploadData<LightData>(lightArray.AsSpan(0, uploadCount));
        }
    }
}
