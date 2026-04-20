namespace Kilo.Rendering.Shaders;

public static class ParticleShaders
{
    /// <summary>
    /// Compute shader for particle simulation (spawn + update).
    /// Binding 0: particles storage buffer (read_write)
    /// Binding 1: emitter params uniform
    /// Binding 2: spawn params uniform (per-frame)
    /// </summary>
    public const string UpdateWGSL = """
        struct GpuParticle {
            position: vec3<f32>,
            alive: f32,
            velocity: vec3<f32>,
            age: f32,
            color: vec4<f32>,
            size: f32,
            lifetime: f32,
            _pad: vec2<f32>,
        };

        struct EmitterParams {
            gravity: vec3<f32>,
            _pad0: f32,
            damping: f32,
            base_size: f32,
            lifetime: f32,
            lifetime_variance: f32,
            max_particles: u32,
            _pad1: u32,
            _pad2: u32,
            _pad3: u32,
            _pad4: u32,
            _pad5: u32,
            _pad6: u32,
        };

        struct SpawnParams {
            emitter_position: vec4<f32>,
            initial_velocity: vec4<f32>,
            speed_variance: f32,
            spread: f32,
            dt: f32,
            spawn_count: f32,
            time: f32,
            _pad0: f32,
            _pad1: f32,
            _pad2: f32,
            _pad3: f32,
        };

        @group(0) @binding(0) var<storage, read_write> particles: array<GpuParticle>;
        @group(0) @binding(1) var<uniform> emitter_params: EmitterParams;
        @group(0) @binding(2) var<uniform> spawn_params: SpawnParams;

        // Simple hash for pseudo-random numbers
        fn hash(value: u32) -> f32 {
            var s = value;
            s = s ^ 2747636419u;
            s = s * 2654435769u;
            s = s ^ (s >> 16u);
            s = s * 2654435769u;
            s = s ^ (s >> 16u);
            return f32(s) / 4294967295.0;
        }

        fn random_range(seed: u32, min: f32, max: f32) -> f32 {
            return min + hash(seed) * (max - min);
        }

        fn random_direction(seed: u32) -> vec3<f32> {
            let theta = hash(seed) * 6.283185307;
            let phi = acos(1.0 - 2.0 * hash(seed + 1u));
            let sin_phi = sin(phi);
            return vec3<f32>(sin_phi * cos(theta), sin_phi * sin(theta), cos(phi));
        }

        @compute @workgroup_size(64)
        fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
            let index = global_id.x;
            if (index >= emitter_params.max_particles) { return; }

            var p = particles[index];

            if (p.alive > 0.5) {
                // Update alive particle
                p.velocity = p.velocity + emitter_params.gravity * spawn_params.dt;
                p.velocity = p.velocity * emitter_params.damping;
                p.position = p.position + p.velocity * spawn_params.dt;
                p.age = p.age + spawn_params.dt;

                let t = clamp(p.age / p.lifetime, 0.0, 1.0);

                // Simple color fade: bright yellow → orange → transparent
                p.color = vec4<f32>(1.0 - t * 0.8, 0.8 - t * 0.6, 0.2, 1.0 - t);

                // Size over lifetime: start at base, shrink to 0
                p.size = emitter_params.base_size * (1.0 - t);

                // Check death
                if (p.age >= p.lifetime) {
                    p.alive = 0.0;
                }
            } else if (spawn_params.spawn_count > 0.0) {
                // Respawn dead particle
                p.alive = 1.0;
                p.position = spawn_params.emitter_position.xyz;

                let dir = random_direction(index + u32(spawn_params.time * 1000.0));
                let speed = length(spawn_params.initial_velocity.xyz)
                    * random_range(index * 3u + 7u, 1.0 - spawn_params.speed_variance, 1.0 + spawn_params.speed_variance);
                p.velocity = spawn_params.initial_velocity.xyz + dir * speed * spawn_params.spread;

                p.age = 0.0;
                p.lifetime = emitter_params.lifetime
                    + random_range(index * 2u + 13u, -emitter_params.lifetime_variance, emitter_params.lifetime_variance);
                p.color = vec4<f32>(1.0, 0.8, 0.2, 1.0);
                p.size = emitter_params.base_size;
            }

            particles[index] = p;
        }
        """;

    /// <summary>
    /// Billboard vertex+fragment shader for particle rendering.
    /// Group 0: camera uniform + particle storage buffer
    /// Group 1: particle render resources (not needed - all in group 0)
    /// </summary>
    public const string RenderWGSL = """
        struct CameraData {
            view: mat4x4<f32>,
            projection: mat4x4<f32>,
            position: vec3<f32>,
            light_count: i32,
        };

        struct GpuParticle {
            position: vec3<f32>,
            alive: f32,
            velocity: vec3<f32>,
            age: f32,
            color: vec4<f32>,
            size: f32,
            lifetime: f32,
            _pad: vec2<f32>,
        };

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) uv: vec2<f32>,
            @location(1) color: vec4<f32>,
        };

        @group(0) @binding(0) var<uniform> camera: CameraData;
        @group(0) @binding(1) var<storage, read> particles: array<GpuParticle>;

        @vertex
        fn vs_main(
            @builtin(vertex_index) vertex_index: u32,
            @builtin(instance_index) instance_index: u32
        ) -> VertexOutput {
            let p = particles[instance_index];

            // Quad corners based on vertex_index (TriangleStrip: 0=BL, 1=BR, 2=TL, 3=TR)
            var quad_pos: vec2<f32>;
            var quad_uv: vec2<f32>;
            if (vertex_index == 0u) {
                quad_pos = vec2<f32>(-0.5, -0.5);
                quad_uv = vec2<f32>(0.0, 1.0);
            } else if (vertex_index == 1u) {
                quad_pos = vec2<f32>(0.5, -0.5);
                quad_uv = vec2<f32>(1.0, 1.0);
            } else if (vertex_index == 2u) {
                quad_pos = vec2<f32>(-0.5, 0.5);
                quad_uv = vec2<f32>(0.0, 0.0);
            } else {
                quad_pos = vec2<f32>(0.5, 0.5);
                quad_uv = vec2<f32>(1.0, 0.0);
            }

            // Extract right and up from view matrix
            let right = normalize(vec3<f32>(camera.view[0][0], camera.view[1][0], camera.view[2][0]));
            let up = normalize(vec3<f32>(camera.view[0][1], camera.view[1][1], camera.view[2][1]));

            let offset = quad_pos * p.size;
            let world_pos = p.position + right * offset.x + up * offset.y;

            var out: VertexOutput;
            out.clip_position = camera.projection * camera.view * vec4<f32>(world_pos, 1.0);
            out.uv = quad_uv;
            out.color = p.color;
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            // Soft circle mask
            let center = in.uv - vec2<f32>(0.5);
            let dist = length(center) * 2.0;
            let alpha = 1.0 - smoothstep(0.6, 1.0, dist);
            return vec4<f32>(in.color.rgb, in.color.a * alpha);
        }
        """;
}
