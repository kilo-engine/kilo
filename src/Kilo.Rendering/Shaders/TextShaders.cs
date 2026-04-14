namespace Kilo.Rendering.Shaders;

public static class TextShaders
{
    public const string WGSL = """
        struct Uniforms {
            projection: mat4x4<f32>,
        };

        @group(0) @binding(0) var<uniform> uniforms: Uniforms;
        @group(0) @binding(1) var font_atlas: texture_2d<f32>;
        @group(0) @binding(2) var font_sampler: sampler;

        struct VertexInput {
            @location(0) position: vec2<f32>,
            @location(1) uv: vec2<f32>,
        };

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) uv: vec2<f32>,
            @location(1) color: vec4<f32>,
        };

        struct QuadParams {
            color: vec4<f32>,
        };

        @vertex
        fn vs_main(in: VertexInput) -> VertexOutput {
            var out: VertexOutput;
            out.clip_position = uniforms.projection * vec4<f32>(in.position, 0.0, 1.0);
            out.uv = in.uv;
            out.color = vec4<f32>(1.0, 1.0, 1.0, 1.0);
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            let glyph = textureSample(font_atlas, font_sampler, in.uv);
            return vec4<f32>(in.color.rgb, in.color.a * glyph.r);
        }
        """;
}
