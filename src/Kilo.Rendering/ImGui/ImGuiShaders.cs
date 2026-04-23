namespace Kilo.Rendering;

/// <summary>
/// WGSL shaders for Dear ImGui rendering via WebGPU.
/// Vertex format: pos(float2) + uv(float2) + col(unorm8x4) = 20 bytes.
/// </summary>
public static class ImGuiShaders
{
    public const string WGSL = """
        struct Uniforms {
            mvp: mat4x4<f32>,
        };

        struct VertexInput {
            @location(0) position: vec2<f32>,
            @location(1) uv: vec2<f32>,
            @location(2) color: vec4<f32>,
        };

        struct VertexOutput {
            @builtin(position) position: vec4<f32>,
            @location(0) color: vec4<f32>,
            @location(1) uv: vec2<f32>,
        };

        @group(0) @binding(0) var<uniform> uniforms: Uniforms;
        @group(0) @binding(1) var font_sampler: sampler;
        @group(1) @binding(0) var font_texture: texture_2d<f32>;

        @vertex
        fn vs_main(in: VertexInput) -> VertexOutput {
            var out: VertexOutput;
            out.position = uniforms.mvp * vec4<f32>(in.position, 0.0, 1.0);
            out.color = in.color;
            out.uv = in.uv;
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return in.color * textureSample(font_texture, font_sampler, in.uv);
        }
        """;
}
