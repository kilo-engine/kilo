namespace Kilo.Rendering.Shaders;

public static class UnlitShaders
{
    public const string WGSL = """
        struct CameraData {
            view: mat4x4<f32>,
            projection: mat4x4<f32>,
            position: vec3<f32>,
        };

        struct ObjectData {
            model: mat4x4<f32>,
            material_id: i32,
        };

        @group(0) @binding(0) var<uniform> camera: CameraData;
        @group(1) @binding(0) var<uniform> object: ObjectData;

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
        };

        @vertex
        fn vs_main(@location(0) position: vec3<f32>, @location(1) normal: vec3<f32>) -> VertexOutput {
            var out: VertexOutput;
            out.clip_position = camera.projection * camera.view * object.model * vec4<f32>(position, 1.0);
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return vec4<f32>(0.8, 0.8, 0.8, 1.0);
        }
        """;
}
