using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering.Scene;

namespace Kilo.Rendering;

/// <summary>
/// Collects all active cameras sorted by priority into ActiveCameraList.
/// Also maintains backward compatibility by setting GpuSceneData.PendingCamera.
/// </summary>
public sealed class CameraPrepareSystem
{
    public void Update(KiloWorld world)
    {
        var scene = world.GetResource<GpuSceneData>();

        // Try to get ActiveCameraList (may not exist in test contexts)
        ActiveCameraList? activeCameras = null;
        try { activeCameras = world.GetResource<ActiveCameraList>(); activeCameras.Clear(); } catch { }

        var cameraQuery = world.QueryBuilder()
            .With<Camera>()
            .With<LocalTransform>()
            .Build();

        var entries = new List<(ActiveCameraEntry Entry, int Priority)>();

        var cameraIter = cameraQuery.Iter();
        while (cameraIter.Next())
        {
            var cameras = cameraIter.Data<Camera>(cameraIter.GetColumnIndexOf<Camera>());
            var transforms = cameraIter.Data<LocalTransform>(cameraIter.GetColumnIndexOf<LocalTransform>());
            var entities = cameraIter.Entities();

            for (int i = 0; i < cameraIter.Count; i++)
            {
                if (!cameras[i].IsActive) continue;

                ref readonly var cam = ref cameras[i];
                ref readonly var transform = ref transforms[i];

                // Determine render target dimensions
                int renderWidth, renderHeight;
                if (cam.Target == CameraTarget.RenderTexture && cam.RenderTexture != null)
                {
                    renderWidth = cam.RenderTexture.Width;
                    renderHeight = cam.RenderTexture.Height;
                }
                else
                {
                    var ws = world.GetResource<WindowSize>();
                    renderWidth = ws.Width;
                    renderHeight = ws.Height;
                }

                entries.Add((new ActiveCameraEntry
                {
                    CameraData = new CameraData
                    {
                        View = cam.ViewMatrix,
                        Projection = cam.ProjectionMatrix,
                        Position = transform.Position,
                    },
                    Target = cam.Target,
                    RenderTexture = cam.RenderTexture,
                    ClearSettings = cam.ClearSettings,
                    CameraType = cam.CameraType,
                    RenderWidth = renderWidth,
                    RenderHeight = renderHeight,
                    PostProcessEnabled = cam.PostProcessEnabled,
                    RenderLayers = cam.RenderLayers,
                    EntityId = entities[i].ID,
                }, cam.Priority));
            }
        }

        // Sort by priority ascending
        entries.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        if (activeCameras != null)
        {
            foreach (var (entry, _) in entries)
                activeCameras.Cameras.Add(entry);
        }

        // Backward compatibility: set PendingCamera to first active camera
        if (entries.Count > 0)
            scene.PendingCamera = entries[0].Entry.CameraData;
    }
}
