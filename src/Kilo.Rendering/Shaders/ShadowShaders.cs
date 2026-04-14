namespace Kilo.Rendering.Shaders;

public static class ShadowShaders
{
    /// <summary>
    /// Depth-only shader for shadow map rendering.
    /// Uses the same ObjectData @group(1) but with a light-space CameraData @group(0).
    /// No fragment shader output — only writes depth.
    /// </summary>
    public const string WGSL = """
        struct CameraData {
            view: mat4x4<f32>,
            projection: mat4x4<f32>,
            position: vec3<f32>,
            _pad0: f32,
            light_count: i32,
            _pad1: i32,
            _pad2: i32,
            _pad3: i32,
        };

        struct ObjectData {
            model: mat4x4<f32>,
            base_color: vec4<f32>,
            material_id: i32,
            use_texture: i32,
        };

        @group(0) @binding(0) var<uniform> camera: CameraData;
        @group(1) @binding(0) var<uniform> object: ObjectData;

        @vertex
        fn vs_main(@location(0) position: vec3<f32>, @location(1) normal: vec3<f32>, @location(2) uv: vec2<f32>) -> @builtin(position) vec4<f32> {
            let world_pos = object.model * vec4<f32>(position, 1.0);
            return camera.projection * camera.view * world_pos;
        }
        """;
}
