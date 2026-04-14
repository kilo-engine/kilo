namespace Kilo.Rendering.Shaders;

public static class ComputeBlurShaders
{
    public const string BlurHorizontalWGSL = """
        @group(0) @binding(0) var src: texture_2d<f32>;
        @group(0) @binding(1) var dst: texture_storage_2d<rgba8unorm, write>;

        @compute @workgroup_size(16, 16)
        fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
            let dims = vec2<i32>(textureDimensions(src));
            if (global_id.x >= u32(dims.x) || global_id.y >= u32(dims.y)) {
                return;
            }
            let coord = vec2<i32>(i32(global_id.x), i32(global_id.y));
            var sum = vec4<f32>(0.0);
            var totalWeight = 0.0;
            let radius = 15;
            let sigma = 6.0;

            for (var x = -radius; x <= radius; x = x + 1) {
                let sx = coord.x + x;
                if (sx >= 0 && sx < dims.x) {
                    let w = exp(-f32(x * x) / (2.0 * sigma * sigma));
                    sum = sum + textureLoad(src, vec2<i32>(sx, coord.y), 0) * w;
                    totalWeight = totalWeight + w;
                }
            }
            textureStore(dst, coord, sum / totalWeight);
        }
        """;

    public const string BlurVerticalWGSL = """
        @group(0) @binding(0) var src: texture_2d<f32>;
        @group(0) @binding(1) var dst: texture_storage_2d<rgba8unorm, write>;

        @compute @workgroup_size(16, 16)
        fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
            let dims = vec2<i32>(textureDimensions(src));
            if (global_id.x >= u32(dims.x) || global_id.y >= u32(dims.y)) {
                return;
            }
            let coord = vec2<i32>(i32(global_id.x), i32(global_id.y));
            var sum = vec4<f32>(0.0);
            var totalWeight = 0.0;
            let radius = 15;
            let sigma = 6.0;

            for (var y = -radius; y <= radius; y = y + 1) {
                let sy = coord.y + y;
                if (sy >= 0 && sy < dims.y) {
                    let w = exp(-f32(y * y) / (2.0 * sigma * sigma));
                    sum = sum + textureLoad(src, vec2<i32>(coord.x, sy), 0) * w;
                    totalWeight = totalWeight + w;
                }
            }
            textureStore(dst, coord, sum / totalWeight);
        }
        """;

    // Keep old name as alias for backward compatibility
    public const string BlurComputeWGSL = BlurHorizontalWGSL;

    public const string FullscreenBlitWGSL = """
        @group(0) @binding(0) var src: texture_2d<f32>;
        @group(0) @binding(1) var smp: sampler;

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) uv: vec2<f32>,
        };

        @vertex
        fn vs_main(@builtin(vertex_index) vertex_index: u32) -> VertexOutput {
            var out: VertexOutput;
            var pos = array<vec2<f32>, 3>(
                vec2<f32>(-1.0, -1.0),
                vec2<f32>( 3.0, -1.0),
                vec2<f32>(-1.0,  3.0)
            );
            let p = pos[vertex_index];
            out.clip_position = vec4<f32>(p, 0.0, 1.0);
            out.uv = vec2<f32>(p.x * 0.5 + 0.5, -p.y * 0.5 + 0.5);
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return textureSample(src, smp, in.uv);
        }
        """;
}
