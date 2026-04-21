using Kilo.Rendering.Driver;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Meshes;

namespace Kilo.Rendering.Scene;

/// <summary>
/// Handles emitting draw calls from GPU scene data to a render command encoder.
/// Separated from GpuSceneData to follow Single Responsibility Principle.
/// </summary>
public static class DrawEmitter
{
    /// <summary>
    /// Emits a single draw call for the draw at the given index.
    /// Handles static and skinned meshes uniformly.
    /// </summary>
    public static void EmitDraw(IRenderCommandEncoder encoder, GpuSceneData scene,
        RenderResourceStore store, int index)
    {
        var draw = scene.GetDraw(index);
        if (!draw.MeshHandle.IsValid || draw.MeshHandle.Value >= store.Meshes.Count) return;
        if (!draw.MaterialHandle.IsValid || draw.MaterialHandle.Value >= store.Materials.Count) return;

        var mesh = store.Meshes[draw.MeshHandle.Value];
        var material = store.Materials[draw.MaterialHandle.Value];

        encoder.SetPipeline(material.Pipeline);
        encoder.SetVertexBuffer(0, mesh.VertexBuffer);
        encoder.SetIndexBuffer(mesh.IndexBuffer);

        var cameraBindingSet = scene.CurrentCameraBuffer != null
            ? scene.GetOrCreateCameraBindingSet(material.Pipeline, scene.CurrentCameraBuffer, scene.Driver!)
            : material.BindingSets[0];
        encoder.SetBindingSet(0, cameraBindingSet);
        encoder.SetBindingSet(1, material.BindingSets[1], (uint)(index * ObjectData.Size));
        encoder.SetBindingSet(2, material.BindingSets[2]);
        if (material.BindingSets.Length > 3)
            encoder.SetBindingSet(3, material.BindingSets[3]);
        if (draw.IsSkinned && draw.JointBindingSet != null)
            encoder.SetBindingSet(4, draw.JointBindingSet);
        encoder.DrawIndexed((int)mesh.IndexCount);
    }
}
