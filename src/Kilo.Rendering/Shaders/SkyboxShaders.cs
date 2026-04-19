namespace Kilo.Rendering.Shaders;

public static class SkyboxShaders
{
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

        @group(0) @binding(0) var<uniform> camera: CameraData;
        @group(1) @binding(0) var cube_texture: texture_cube<f32>;
        @group(1) @binding(1) var cube_sampler: sampler;

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) direction: vec3<f32>,
        };

        @vertex
        fn vs_main(@location(0) position: vec3<f32>) -> VertexOutput {
            var out: VertexOutput;
            // Strip translation from view matrix so skybox is always centered on camera
            let view_rot = mat4x4<f32>(
                camera.view[0],
                camera.view[1],
                camera.view[2],
                vec4<f32>(0.0, 0.0, 0.0, 1.0),
            );
            let clip = camera.projection * view_rot * vec4<f32>(position, 1.0);
            // Set z = w so depth = 1.0 (far plane), ensuring skybox is behind everything
            out.clip_position = vec4<f32>(clip.x, clip.y, clip.w, clip.w);
            out.direction = position;
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return textureSample(cube_texture, cube_sampler, in.direction);
        }
        """;
}
