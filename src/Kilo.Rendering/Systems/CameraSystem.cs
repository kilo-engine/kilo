using System.Numerics;
using Kilo.ECS;

namespace Kilo.Rendering;

/// <summary>
/// System that updates camera view and projection matrices.
/// </summary>
public sealed class CameraSystem
{
    /// <summary>Update all cameras.</summary>
    public void Update(KiloWorld world)
    {
        var query = world.QueryBuilder()
            .With<Camera>()
            .With<LocalTransform>()
            .Build();

        var iter = query.Iter();
        while (iter.Next())
        {
            var cameras = iter.Data<Camera>(iter.GetColumnIndexOf<Camera>());
            var transforms = iter.Data<LocalTransform>(iter.GetColumnIndexOf<LocalTransform>());

            for (int i = 0; i < iter.Count; i++)
            {
                ref var camera = ref cameras[i];
                ref var transform = ref transforms[i];

                // Compute view matrix
                var position = transform.Position;
                var rotation = transform.Rotation;

                // Calculate camera forward, right, and up vectors from rotation
                var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
                var right = Vector3.Transform(Vector3.UnitX, rotation);
                var up = Vector3.Transform(Vector3.UnitY, rotation);

                // View matrix: world to camera space
                camera.ViewMatrix = Matrix4x4.CreateLookAt(position, position + forward, up);

                // Get window size for aspect ratio
                var windowSize = world.GetResource<WindowSize>();
                float aspectRatio = (float)windowSize.Width / windowSize.Height;

                // Projection matrix: camera to clip space
                camera.ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                    camera.FieldOfView,
                    aspectRatio,
                    camera.NearPlane,
                    camera.FarPlane);
            }
        }
    }
}
