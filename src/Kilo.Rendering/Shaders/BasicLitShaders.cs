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
            metallic: f32,
            roughness: f32,
            use_normal_map: i32,
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
        @group(3) @binding(5) var normal_map: texture_2d<f32>;
        @group(3) @binding(6) var normal_sampler: sampler;

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) world_pos: vec3<f32>,
            @location(1) normal: vec3<f32>,
            @location(2) uv: vec2<f32>,
            @location(3) tangent: vec4<f32>,
            @location(4) shadow_pos: vec3<f32>,
        };

        // ---- PBR helper functions ----

        fn DistributionGGX(N: vec3<f32>, H: vec3<f32>, roughness: f32) -> f32 {
            let a = roughness * roughness;
            let a2 = a * a;
            let NdotH = max(dot(N, H), 0.0);
            let NdotH2 = NdotH * NdotH;
            let denom = NdotH2 * (a2 - 1.0) + 1.0;
            return a2 / (3.14159265 * denom * denom);
        }

        fn GeometrySchlickGGX(NdotV: f32, roughness: f32) -> f32 {
            let r = roughness + 1.0;
            let k = (r * r) / 8.0;
            return NdotV / (NdotV * (1.0 - k) + k);
        }

        fn GeometrySmith(N: vec3<f32>, V: vec3<f32>, L: vec3<f32>, roughness: f32) -> f32 {
            let NdotV = max(dot(N, V), 0.0);
            let NdotL = max(dot(N, L), 0.0);
            return GeometrySchlickGGX(NdotV, roughness) * GeometrySchlickGGX(NdotL, roughness);
        }

        fn FresnelSchlick(cosTheta: f32, F0: vec3<f32>) -> vec3<f32> {
            return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
        }

        // ---- Vertex shader ----

        @vertex
        fn vs_main(
            @location(0) position: vec3<f32>,
            @location(1) normal: vec3<f32>,
            @location(2) uv: vec2<f32>,
            @location(3) tangent: vec4<f32>
        ) -> VertexOutput {
            var out: VertexOutput;
            let world_pos = object.model * vec4<f32>(position, 1.0);
            out.clip_position = camera.projection * camera.view * world_pos;
            out.world_pos = world_pos.xyz;
            out.normal = normalize((object.model * vec4<f32>(normal, 0.0)).xyz);
            out.uv = uv;
            // Transform tangent direction (xyz), preserve handedness (w)
            let t = normalize((object.model * vec4<f32>(tangent.xyz, 0.0)).xyz);
            out.tangent = vec4<f32>(t, tangent.w);
            // Shadow space coordinates
            let shadow_clip = shadow_data.light_vp * world_pos;
            // X,Y: [-1,1] → [0,1] for UV sampling; Z: already in [0,1] from lightVP remap
            out.shadow_pos = vec3<f32>(
                shadow_clip.x * 0.5 + 0.5,
                shadow_clip.y * 0.5 + 0.5,
                shadow_clip.z
            );
            return out;
        }

        // ---- Fragment shader ----

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            // Albedo
            var albedo = object.base_color.rgb;
            var alpha = object.base_color.a;
            if (object.use_texture != 0) {
                let tex = textureSample(albedo_texture, albedo_sampler, in.uv);
                albedo = tex.rgb;
                alpha = alpha * tex.a;
            }

            // Normal mapping
            var N = normalize(in.normal);
            if (object.use_normal_map != 0) {
                let T = normalize(in.tangent.xyz);
                let B = cross(N, T) * in.tangent.w;
                let TBN = mat3x3<f32>(T, B, N);
                let nm = textureSample(normal_map, normal_sampler, in.uv);
                let tangentNormal = nm.rgb * 2.0 - 1.0;
                N = normalize(TBN * tangentNormal);
            }

            // PBR parameters
            let metallic = object.metallic;
            let roughness = max(object.roughness, 0.04); // clamp to avoid division by zero
            let V = normalize(camera.position - in.world_pos);

            // Dielectric F0 = 0.04, metallic F0 = albedo
            let F0 = mix(vec3<f32>(0.04, 0.04, 0.04), albedo, metallic);

            // Ambient (indirect approximation)
            var color = albedo * 0.03;

            for (var i = 0; i < camera.light_count; i++) {
                let light = lights.data[i];
                let L = normalize(-light.direction_or_position);
                let H = normalize(L + V);

                // Shadow
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

                // Cook-Torrance BRDF
                let NDF = DistributionGGX(N, H, roughness);
                let G = GeometrySmith(N, V, L, roughness);
                let fresnel = FresnelSchlick(max(dot(H, V), 0.0), F0);

                let NdotL = max(dot(N, L), 0.0);
                let numerator = NDF * G * fresnel;
                let denominator = 4.0 * max(dot(N, V), 0.0) * NdotL + 0.0001;
                let specular = numerator / denominator;

                // Energy conservation: ks + kd = 1
                let ks = fresnel;
                let kd = (vec3<f32>(1.0, 1.0, 1.0) - ks) * (1.0 - metallic);

                // Lambertian diffuse
                let diffuse = kd * albedo / 3.14159265;

                color += (diffuse + specular) * light.color * light.intensity * NdotL * shadow;
            }

            return vec4<f32>(color, alpha);
        }
        """;
}
