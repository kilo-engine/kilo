namespace Kilo.Rendering.Shaders;

public static class PostProcessShaders
{
    public const string BloomExtractWGSL = """
        @group(0) @binding(0) var scene_color: texture_2d<f32>;
        @group(0) @binding(1) var bright_extract: texture_storage_2d<rgba16float, write>;
        @group(0) @binding(2) var<uniform> params: PostProcessParams;

        struct PostProcessParams {
            bloom_threshold: f32,
            bloom_intensity: f32,
            _pad0: f32,
            _pad1: f32,
        };

        @compute @workgroup_size(16, 16)
        fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
            let dims = vec2<i32>(textureDimensions(scene_color));
            if (global_id.x >= u32(dims.x) || global_id.y >= u32(dims.y)) { return; }
            let coord = vec2<i32>(i32(global_id.x), i32(global_id.y));
            let color = textureLoad(scene_color, coord, 0);
            let brightness = dot(color.rgb, vec3<f32>(0.2126, 0.7152, 0.0722));
            let contribution = max(color.rgb - vec3<f32>(params.bloom_threshold), vec3<f32>(0.0));
            textureStore(bright_extract, coord, vec4<f32>(contribution * params.bloom_intensity, color.a));
        }
        """;

    public const string BloomBlurHWGSL = """
        @group(0) @binding(0) var src: texture_2d<f32>;
        @group(0) @binding(1) var dst: texture_storage_2d<rgba16float, write>;

        @compute @workgroup_size(16, 16)
        fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
            let dims = vec2<i32>(textureDimensions(src));
            if (global_id.x >= u32(dims.x) || global_id.y >= u32(dims.y)) { return; }
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

    public const string BloomBlurVWGSL = """
        @group(0) @binding(0) var src: texture_2d<f32>;
        @group(0) @binding(1) var dst: texture_storage_2d<rgba16float, write>;

        @compute @workgroup_size(16, 16)
        fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
            let dims = vec2<i32>(textureDimensions(src));
            if (global_id.x >= u32(dims.x) || global_id.y >= u32(dims.y)) { return; }
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

    public const string CompositeToneMapWGSL = """
        @group(0) @binding(0) var scene_color: texture_2d<f32>;
        @group(0) @binding(1) var bloom_blur: texture_2d<f32>;
        @group(0) @binding(2) var smp: sampler;
        @group(0) @binding(3) var<uniform> params: PostProcessParams;

        struct PostProcessParams {
            bloom_threshold: f32,
            bloom_intensity: f32,
            bloom_enabled: f32,
            tonemap_enabled: f32,
        };

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

        fn aces_tonemap(x: vec3<f32>) -> vec3<f32> {
            let a = 2.51;
            let b = 0.03;
            let c = 2.43;
            let d = 0.59;
            let e = 0.14;
            return clamp((x * (a * x + b)) / (x * (c * x + d) + e), vec3<f32>(0.0), vec3<f32>(1.0));
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            var color = textureSample(scene_color, smp, in.uv).rgb;

            // Add bloom contribution
            if (params.bloom_enabled > 0.5) {
                let bloom = textureSample(bloom_blur, smp, in.uv).rgb;
                color = color + bloom * params.bloom_intensity;
            }

            // ACES tone mapping
            if (params.tonemap_enabled > 0.5) {
                color = aces_tonemap(color);
            }

            return vec4<f32>(color, 1.0);
        }
        """;

    public const string FxaaWGSL = """
        @group(0) @binding(0) var scene_color: texture_2d<f32>;
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
            let texelSize = 1.0 / vec2<f32>(textureDimensions(scene_color));

            let rgbN  = textureSample(scene_color, smp, in.uv + vec2<f32>( 0.0, -texelSize.y)).rgb;
            let rgbS  = textureSample(scene_color, smp, in.uv + vec2<f32>( 0.0,  texelSize.y)).rgb;
            let rgbW  = textureSample(scene_color, smp, in.uv + vec2<f32>(-texelSize.x,  0.0)).rgb;
            let rgbE  = textureSample(scene_color, smp, in.uv + vec2<f32>( texelSize.x,  0.0)).rgb;
            let rgbM  = textureSample(scene_color, smp, in.uv).rgb;

            let lumaM = dot(rgbM, vec3<f32>(0.299, 0.587, 0.114));
            let lumaN = dot(rgbN, vec3<f32>(0.299, 0.587, 0.114));
            let lumaS = dot(rgbS, vec3<f32>(0.299, 0.587, 0.114));
            let lumaW = dot(rgbW, vec3<f32>(0.299, 0.587, 0.114));
            let lumaE = dot(rgbE, vec3<f32>(0.299, 0.587, 0.114));

            let rangeMin = min(lumaM, min(min(lumaN, lumaS), min(lumaW, lumaE)));
            let rangeMax = max(lumaM, max(max(lumaN, lumaS), max(lumaW, lumaE)));
            let range = rangeMax - rangeMin;

            // Early exit if no edge detected
            if (range < max(0.0312, rangeMax * 0.125)) {
                return vec4<f32>(rgbM, 1.0);
            }

            let lumaNW = dot(textureSample(scene_color, smp, in.uv + vec2<f32>(-texelSize.x, -texelSize.y)).rgb, vec3<f32>(0.299, 0.587, 0.114));
            let lumaNE = dot(textureSample(scene_color, smp, in.uv + vec2<f32>( texelSize.x, -texelSize.y)).rgb, vec3<f32>(0.299, 0.587, 0.114));
            let lumaSW = dot(textureSample(scene_color, smp, in.uv + vec2<f32>(-texelSize.x,  texelSize.y)).rgb, vec3<f32>(0.299, 0.587, 0.114));
            let lumaSE = dot(textureSample(scene_color, smp, in.uv + vec2<f32>( texelSize.x,  texelSize.y)).rgb, vec3<f32>(0.299, 0.587, 0.114));

            let edgeVert = abs(lumaN + lumaS - 2.0 * lumaM) * 2.0
                         + abs(lumaNE + lumaSE - 2.0 * lumaE)
                         + abs(lumaNW + lumaSW - 2.0 * lumaW);
            let edgeHorz = abs(lumaW + lumaE - 2.0 * lumaM) * 2.0
                         + abs(lumaNW + lumaNE - 2.0 * lumaN)
                         + abs(lumaSW + lumaSE - 2.0 * lumaS);

            var dir: vec2<f32>;
            if (edgeHorz >= edgeVert) {
                dir = vec2<f32>(0.0, texelSize.y);
            } else {
                dir = vec2<f32>(texelSize.x, 0.0);
            }

            let negDir = -dir;
            let rgbA = 0.5 * (
                textureSample(scene_color, smp, in.uv + dir * 0.5).rgb +
                textureSample(scene_color, smp, in.uv + negDir * 0.5).rgb
            );
            let rgbB = 0.5 * (
                textureSample(scene_color, smp, in.uv + dir * 2.0).rgb +
                textureSample(scene_color, smp, in.uv + negDir * 2.0).rgb
            );

            let lumaA = dot(rgbA, vec3<f32>(0.299, 0.587, 0.114));
            let lumaB = dot(rgbB, vec3<f32>(0.299, 0.587, 0.114));

            if (abs(lumaA - lumaM) < abs(lumaB - lumaM)) {
                return vec4<f32>(rgbA, 1.0);
            } else {
                return vec4<f32>(rgbB, 1.0);
            }
        }
        """;

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
