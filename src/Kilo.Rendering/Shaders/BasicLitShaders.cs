namespace Kilo.Rendering.Shaders;

public static class BasicLitShaders
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

        struct ObjectData {
            model: mat4x4<f32>,
            base_color: vec4<f32>,
            material_id: i32,
            use_texture: i32,
        };

        struct LightData {
            direction_or_position: vec3<f32>,
            _pad0: f32,
            color: vec3<f32>,
            intensity: f32,
            range: f32,
            light_type: i32,
            _pad1: i32,
            _pad2: i32,
        };

        struct Lights {
            data: array<LightData, 64>,
        };

        struct ShadowData {
            light_vp: mat4x4<f32>,
            shadow_enabled: i32,
            _pad0: i32,
            _pad1: i32,
            _pad2: i32,
        };

        @group(0) @binding(0) var<uniform> camera: CameraData;
        @group(1) @binding(0) var<uniform> object: ObjectData;
        @group(2) @binding(0) var<uniform> lights: Lights;
        @group(3) @binding(0) var albedo_texture: texture_2d<f32>;
        @group(3) @binding(1) var albedo_sampler: sampler;
        @group(3) @binding(2) var shadow_map: texture_depth_2d;
        @group(3) @binding(3) var shadow_sampler: sampler_comparison;
        @group(3) @binding(4) var<uniform> shadow_data: ShadowData;

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) world_pos: vec3<f32>,
            @location(1) normal: vec3<f32>,
            @location(2) uv: vec2<f32>,
            @location(3) shadow_pos: vec3<f32>,
        };

        @vertex
        fn vs_main(@location(0) position: vec3<f32>, @location(1) normal: vec3<f32>, @location(2) uv: vec2<f32>) -> VertexOutput {
            var out: VertexOutput;
            let world_pos = object.model * vec4<f32>(position, 1.0);
            out.clip_position = camera.projection * camera.view * world_pos;
            out.world_pos = world_pos.xyz;
            out.normal = normalize((object.model * vec4<f32>(normal, 0.0)).xyz);
            out.uv = uv;
            // Shadow space coordinates
            let shadow_clip = shadow_data.light_vp * world_pos;
            out.shadow_pos = vec3<f32>(
                shadow_clip.x * 0.5 + 0.5,
                shadow_clip.y * 0.5 + 0.5,
                shadow_clip.z * 0.5 + 0.5
            );
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            // Per-cube vivid color based on world position
            let hue = in.world_pos.x * 0.5;
            let procedural_color = vec3<f32>(
                0.5 + 0.5 * sin(hue),
                0.5 + 0.5 * sin(hue + 2.094),
                0.5 + 0.5 * sin(hue + 4.189)
            );

            var base_color = object.base_color.rgb;
            if (object.use_texture != 0) {
                base_color = textureSample(albedo_texture, albedo_sampler, in.uv).rgb;
            } else {
                base_color = base_color * procedural_color;
            }

            var color = base_color * 0.15; // ambient
            let N = normalize(in.normal);
            let V = normalize(camera.position - in.world_pos);

            for (var i = 0; i < camera.light_count; i++) {
                let light = lights.data[i];
                let L = normalize(-light.direction_or_position);
                let NdotL = max(dot(N, L), 0.0);
                let diffuse = base_color * NdotL;
                let H = normalize(L + V);
                let NdotH = max(dot(N, H), 0.0);
                let specular = pow(NdotH, 32.0) * vec3<f32>(0.5, 0.5, 0.5);

                // Shadow: sample comparison sampler
                var shadow = 1.0;
                if (shadow_data.shadow_enabled != 0 &&
                    in.shadow_pos.x >= 0.0 && in.shadow_pos.x <= 1.0 &&
                    in.shadow_pos.y >= 0.0 && in.shadow_pos.y <= 1.0 &&
                    in.shadow_pos.z >= 0.0 && in.shadow_pos.z <= 1.0)
                {
                    shadow = textureSampleCompare(
                        shadow_map, shadow_sampler,
                        in.shadow_pos.xy, in.shadow_pos.z - 0.005
                    );
                }

                color += (diffuse + specular) * light.color * light.intensity * shadow;
            }

            return vec4<f32>(color, 1.0);
        }
        """;
}
