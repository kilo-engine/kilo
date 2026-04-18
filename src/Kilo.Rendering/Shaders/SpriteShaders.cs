namespace Kilo.Rendering.Shaders;

internal static class SpriteShaders
{
    public const string WGSL = """
        struct Uniforms {
            model: mat4x4<f32>,
            projection: mat4x4<f32>,
            color: vec4<f32>,
        };

        @group(0) @binding(0) var<uniform> uniforms: Uniforms;

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) color: vec4<f32>,
        };

        @vertex
        fn vs_main(@location(0) position: vec2<f32>) -> VertexOutput {
            var out: VertexOutput;
            out.clip_position = uniforms.projection * uniforms.model * vec4<f32>(position, 0.0, 1.0);
            out.color = uniforms.color;
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return in.color;
        }
        """;
}
