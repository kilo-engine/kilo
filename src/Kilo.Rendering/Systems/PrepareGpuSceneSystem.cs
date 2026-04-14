using System.Numerics;
using System.Runtime.InteropServices;
using Kilo.ECS;
using Kilo.Rendering.Resources;

namespace Kilo.Rendering;

/// <summary>
/// System that prepares per-frame GPU scene data: camera, objects, and lights.
/// Runs in KiloStage.Last before any rendering passes.
/// </summary>
public sealed class PrepareGpuSceneSystem
{
    public void Update(KiloWorld world)
    {
        var scene = world.GetResource<GpuSceneData>();
        var context = world.GetResource<RenderContext>();

        // --- Camera ---
        var cameraQuery = world.QueryBuilder()
            .With<Camera>()
            .With<LocalTransform>()
            .Build();

        CameraData cameraData = default;
        bool cameraFound = false;
        int frame = (int)world.CurrentTick;

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

        // --- Draw objects (skip culled entities) ---
        var meshQuery = world.QueryBuilder()
            .With<MeshRenderer>()
            .With<LocalToWorld>()
            .Without<Culled>()
            .Build();

        int maxObjects = (int)(scene.ObjectDataBuffer.Size / 256);
        var objectData = new ObjectData[maxObjects];
        var drawData = new List<DrawData>();

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

                // Populate material properties into ObjectData
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
                });
            }
        }

        scene.DrawCount = drawData.Count;
        scene.DrawData = drawData.ToArray();
        if (drawData.Count > 0)
        {
            scene.ObjectDataBuffer.UploadData<ObjectData>(objectData.AsSpan(0, drawData.Count));
            if (frame <= 3)
                Console.WriteLine($"[PrepareGpuScene] Frame {frame}: DrawCount={drawData.Count}, firstObjTrans={objectData[0].Model.Translation}");
        }

        // --- Lights ---
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

        scene.LightCount = lights.Count;
        cameraData.LightCount = lights.Count;
        var cameraArray = new CameraData[1];
        cameraArray[0] = cameraData;
        scene.CameraBuffer.UploadData<CameraData>(cameraArray.AsSpan());
        if (frame <= 3)
            Console.WriteLine($"[PrepareGpuScene] Frame {frame}: Lights={lights.Count}, CamPos={cameraData.Position}, CamZ={cameraData.View.M31:F2},{cameraData.View.M32:F2},{cameraData.View.M33:F2}");

        if (lights.Count > 0)
        {
            int maxLights = (int)(scene.LightBuffer.Size / (nuint)Marshal.SizeOf<LightData>());
            int uploadCount = Math.Min(lights.Count, maxLights);
            var lightArray = lights.ToArray();
            scene.LightBuffer.UploadData<LightData>(lightArray.AsSpan(0, uploadCount));
        }
    }
}
