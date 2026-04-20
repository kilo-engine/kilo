// =============================================================================
// Kilo.Samples.RenderDemo — Unified Rendering + Input Demo
// =============================================================================
// 本示例整合了所有渲染和输入功能：
//   1) 3D 前向渲染：3 不透明 + 3 半透明旋转立方体 + PBR光照 + 相机控制 + Alpha 排序
//   2) Compute Blur 后处理：按 B 键开关模糊效果
//   3) 2D 精灵渲染：3 个不同轨迹运动的彩色方块
//   4) 文字渲染：字体图集 + 动态文本
//   5) 输入系统：WASD/QE 相机移动 + 按键状态控制台输出
//   6) GLTF 模型加载：传入 .gltf/.glb 文件路径即可加载
//   7) 骨骼动画演示：2 关节程序化手臂，N 键播放/暂停，M 键切换循环
//
// 运行方式：
//   dotnet run --project samples/Kilo.Samples.RenderDemo
//   dotnet run --project samples/Kilo.Samples.RenderDemo -- path/to/model.glb
// =============================================================================

using System.Numerics;
using Kilo.ECS;
using Kilo.Window;
using Kilo.Rendering;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Driver.WebGPUImpl;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Meshes;
using Kilo.Rendering.Materials;
using Kilo.Rendering.Animation;
using Kilo.Rendering.Particles;
using Kilo.Rendering.Text;
using Kilo.Rendering.Scene;
using Kilo.Rendering.Shaders;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Parent = TinyEcs.Parent;
using JointInfo = Kilo.Rendering.Animation.JointInfo;

var app = new KiloApp();
var settings = new RenderSettings
{
    Width = 1280,
    Height = 720,
    Title = "Kilo — Render Demo"
};

var plugin = new RenderDemoPlugin(settings, modelPath: args.Length > 0 ? args[0] : null);
app.AddPlugin(plugin);

// ── 场景创建 ────────────────────────────────────────────────────────────────
app.AddSystem(KiloStage.Startup, world =>
{
    var context = world.GetResource<RenderContext>();
    var scene = world.GetResource<GpuSceneData>();
    var driver = context.Driver!;

    // ── 3 不透明 + 3 半透明 立方体 (PBR) ──────────────────────────────────────────
    var colors = new[]
    {
        new Vector4(0.8f, 0.2f, 0.2f, 1.0f), // Red (opaque, rough)
        new Vector4(0.2f, 0.8f, 0.2f, 1.0f), // Green (opaque, metallic)
        new Vector4(0.2f, 0.3f, 0.9f, 1.0f), // Blue (opaque, smooth)
        new Vector4(1.0f, 1.0f, 0.3f, 0.4f), // Yellow (transparent)
        new Vector4(1.0f, 0.3f, 1.0f, 0.6f), // Magenta (transparent)
        new Vector4(0.3f, 1.0f, 1.0f, 0.5f), // Cyan (transparent)
    };
    var metallic = new[] { 0.0f, 1.0f, 0.5f, 0.0f, 0.0f, 0.0f };
    var roughness = new[] { 0.8f, 0.3f, 0.1f, 0.5f, 0.5f, 0.5f };

    var materialIds = new int[colors.Length];
    for (int i = 0; i < colors.Length; i++)
    {
        materialIds[i] = context.MaterialManager.CreateMaterial(context, scene, new MaterialDescriptor
        {
            BaseColor = colors[i],
            Metallic = metallic[i],
            Roughness = roughness[i],
        });
    }

    for (int i = 0; i < 6; i++)
    {
        // Pair each opaque cube with a transparent cube at the same X, closer to camera
        int pair = i % 3;
        float x = (pair - 1) * 3.0f;
        bool isTransparent = i >= 3;
        float z = isTransparent ? 2.0f : 0.0f;
        world.Entity($"Cube{i}")
            .Set(new LocalTransform
            {
                Position = new Vector3(x, 0, z),
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            })
            .Set(new LocalToWorld())
            .Set(new MeshRenderer { MeshHandle = 0, MaterialHandle = materialIds[i] });
    }

    // ── 纹理立方体 ────────────────────────────────────────────────────────
    const int textureSize = 64;
    const int checkerSize = 8; // 8x8 checker grid
    using (var checkerImage = new Image<Rgba32>(textureSize, textureSize))
    {
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Create 8x8 checker pattern (8 cells per row)
                bool isRed = ((x / checkerSize) + (y / checkerSize)) % 2 == 0;
                checkerImage[x, y] = isRed
                    ? new Rgba32(255, 50, 50, 255) // Red
                    : new Rgba32(255, 255, 255, 255); // White
            }
        }
        var tempPath = Path.Combine(Path.GetTempPath(), $"kilo_checker_{Guid.NewGuid():N}.png");
        checkerImage.SaveAsPng(tempPath);
        var texturedMaterial = context.MaterialManager.CreateMaterial(context, scene, new MaterialDescriptor
        {
            AlbedoTexturePath = tempPath,
            BaseColor = new Vector4(1f, 1f, 1f, 0.7f),
            Metallic = 0.3f,
            Roughness = 0.4f,
        });
        world.Entity("TexturedCube")
            .Set(new LocalTransform
            {
                Position = new Vector3(7, 0, 1),
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            })
            .Set(new LocalToWorld())
            .Set(new MeshRenderer { MeshHandle = 0, MaterialHandle = texturedMaterial });
    }

    // ── 骨骼动画演示：2段手臂 ──────────────────────────────────────────────
    // Create a simple 2-bone arm mesh (upper and lower segments)
    {
        // Create skeleton data with 2 joints
        var skeletonData = new SkeletonData
        {
            Joints =
            [
                new JointInfo { Name = "UpperArm", ParentIndex = -1, InverseBindMatrix = Matrix4x4.Identity },
                new JointInfo { Name = "LowerArm", ParentIndex = 0, InverseBindMatrix = Matrix4x4.Identity },
            ]
        };

        // Create joint entities with LocalTransform + LocalToWorld
        // Joint 0: Upper arm at (0, 0, 0)
        var joint0Entity = world.Entity("Joint0_UpperArm")
            .Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One })
            .Set(new LocalToWorld());

        // Joint 1: Lower arm at (0, 2, 0) relative to joint 0
        var joint1Entity = world.Entity("Joint1_LowerArm")
            .Set(new LocalTransform { Position = new Vector3(0, 2, 0), Rotation = Quaternion.Identity, Scale = Vector3.One })
            .Set(new LocalToWorld())
            .Set(new Parent { Id = new EntityId(joint0Entity.Id) });

        // Create skeleton component
        var skeleton = new Skeleton
        {
            Data = skeletonData,
            JointEntities = new int[] { (int)(uint)joint0Entity.Id.Value, (int)(uint)joint1Entity.Id.Value }
        };

        // Create skinned mesh vertices (2 quad segments)
        // Upper arm (vertices 0-3): weighted to joint 0
        // Lower arm (vertices 4-7): weighted to joint 1
        // pos(3) + normal(3) + uv(2) + tangent(4) + joints(4 uint) + weights(4 float) = 80 bytes
        var skinnedVertices = new byte[8 * SkinnedMesh.BytesPerVertex]; // 8 vertices

        for (int v = 0; v < 8; v++)
        {
            int offset = v * SkinnedMesh.BytesPerVertex;

            // Determine which segment and joint weight
            bool isUpperArm = v < 4;
            uint jointIndex = isUpperArm ? 0u : 1u;
            float yBase = isUpperArm ? 0f : 2f;

            // Position (12 bytes) - create quad facing +Z
            int vertexIndex = v % 4;
            float x = (vertexIndex == 0 || vertexIndex == 3) ? -0.3f : 0.3f;
            float y = yBase + ((vertexIndex < 2) ? 0f : 2f);
            float z = 0f;

            BitConverter.GetBytes(x).CopyTo(skinnedVertices, offset);
            BitConverter.GetBytes(y).CopyTo(skinnedVertices, offset + 4);
            BitConverter.GetBytes(z).CopyTo(skinnedVertices, offset + 8);

            // Normal (12 bytes) - facing +Z
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 12);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 16);
            BitConverter.GetBytes(1f).CopyTo(skinnedVertices, offset + 20);

            // UV (8 bytes)
            float u = (vertexIndex == 0 || vertexIndex == 3) ? 0f : 1f;
            float vCoord = (vertexIndex < 2) ? 0f : 1f;
            BitConverter.GetBytes(u).CopyTo(skinnedVertices, offset + 24);
            BitConverter.GetBytes(vCoord).CopyTo(skinnedVertices, offset + 28);

            // Tangent (16 bytes) - +X for normal facing +Z, w=1 (handedness)
            BitConverter.GetBytes(1f).CopyTo(skinnedVertices, offset + 32);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 36);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 40);
            BitConverter.GetBytes(1f).CopyTo(skinnedVertices, offset + 44);

            // Joints (16 bytes) - 4 uint32
            BitConverter.GetBytes(jointIndex).CopyTo(skinnedVertices, offset + 48);
            BitConverter.GetBytes(0u).CopyTo(skinnedVertices, offset + 52);
            BitConverter.GetBytes(0u).CopyTo(skinnedVertices, offset + 56);
            BitConverter.GetBytes(0u).CopyTo(skinnedVertices, offset + 60);

            // Weights (16 bytes) - full weight to jointIndex
            BitConverter.GetBytes(1f).CopyTo(skinnedVertices, offset + 64);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 68);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 72);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 76);
        }

        // Indices for 2 quads (6 indices each)
        var skinnedIndices = new uint[]
        {
            // Upper arm quad
            0, 1, 2,  0, 2, 3,
            // Lower arm quad
            4, 5, 6,  4, 6, 7,
        };

        // Create GPU buffers
        var skinnedVertexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)skinnedVertices.Length,
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        skinnedVertexBuffer.UploadData<byte>(skinnedVertices);

        var skinnedIndexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(skinnedIndices.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        skinnedIndexBuffer.UploadData<uint>(skinnedIndices);

        // Create skinned mesh
        var skinnedMesh = new Mesh
        {
            VertexBuffer = skinnedVertexBuffer,
            IndexBuffer = skinnedIndexBuffer,
            IndexCount = (uint)skinnedIndices.Length,
            Layouts = [SkinnedMesh.Layout]
        };
        int skinnedMeshHandle = context.AddMesh(skinnedMesh);

        // Create skinned material pipeline
        var skinnedVs = context.ShaderCache.GetOrCreateShader(driver, SkinnedLitShaders.WGSL, "vs_main");
        var skinnedFs = context.ShaderCache.GetOrCreateShader(driver, SkinnedLitShaders.WGSL, "fs_main");

        var skinnedPipelineKey = new PipelineCacheKey
        {
            VertexShaderSource = SkinnedLitShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = SkinnedLitShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = [SkinnedMesh.Layout],
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA16Float }],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            }
        };

        var skinnedPipeline = context.PipelineCache.GetOrCreate(driver, skinnedPipelineKey, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = skinnedVs,
            FragmentShader = skinnedFs,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = skinnedPipelineKey.ColorTargets,
            VertexBuffers = skinnedPipelineKey.VertexBuffers,
            DepthStencil = skinnedPipelineKey.DepthStencil,
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 5));

        // Reuse camera/object/light binding sets from basic lit
        var cameraBindingSet = driver.CreateBindingSetForPipeline(skinnedPipeline, 0, [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);
        var objectBindingSet = driver.CreateDynamicUniformBindingSet(skinnedPipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        var lightBindingSet = driver.CreateBindingSetForPipeline(skinnedPipeline, 2, [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // Group 3: texture bindings — centralized via MaterialManager (single source of truth for group 3 layout)
        var defaultSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear, MagFilter = FilterMode.Linear, MipFilter = FilterMode.Linear,
            AddressModeU = WrapMode.Repeat, AddressModeV = WrapMode.Repeat, AddressModeW = WrapMode.Repeat,
        });
        var defaultTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1, Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });
        defaultTexture.UploadData<byte>([255, 255, 255, 255]);
        var defaultTextureView = driver.CreateTextureView(defaultTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm, Dimension = TextureViewDimension.View2D, MipLevelCount = 1,
        });

        var textureBindingSet = context.MaterialManager.CreateTextureBindingSet(driver, skinnedPipeline, scene, defaultTextureView, defaultTextureView);

        // Group 4: joint matrices
        var jointBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(SkeletonData.MaxJoints * 64),
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        var jointBindingSet = driver.CreateBindingSetForPipeline(skinnedPipeline, 4,
            [new UniformBufferBinding { Buffer = jointBuffer, Binding = 0 }]);

        // Create material
        var skinnedMaterial = new Material
        {
            Pipeline = skinnedPipeline,
            BindingSets = [cameraBindingSet, objectBindingSet, lightBindingSet, textureBindingSet, jointBindingSet],
            AlbedoTexture = defaultTexture,
            AlbedoSampler = defaultSampler,
        };
        int skinnedMaterialHandle = context.AddMaterial(skinnedMaterial);

        // Create main entity with skinned mesh
        var animatedArmEntity = world.Entity("AnimatedArm")
            .Set(new LocalTransform
            {
                Position = new Vector3(-6, 0, 0), // Position to the left of other cubes
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            })
            .Set(new LocalToWorld())
            .Set(skeleton)
            .Set(new AnimationPlayer { ClipIndex = 0, Time = 0f, IsPlaying = true, Loop = true })
            .Set(new SkinnedMeshRenderer
            {
                MeshHandle = skinnedMeshHandle,
                MaterialHandle = skinnedMaterialHandle,
                JointMatrixBuffer = jointBuffer,
                JointBindingSet = jointBindingSet,
            });
    }

    // ── 3 个 2D 精灵（覆盖在 3D 场景上方） ────────────────────────────────
    var spriteColors = new[]
    {
        new Vector4(1.0f, 0.25f, 0.25f, 0.85f), // 红色（半透明）
        new Vector4(0.25f, 1.0f, 0.25f, 0.85f), // 绿色
        new Vector4(0.25f, 0.25f, 1.0f, 0.85f), // 蓝色
    };
    var spriteNames = new[] { "RedSine", "GreenOrbit", "BlueFigure8" };

    for (int i = 0; i < 3; i++)
    {
        world.Entity(spriteNames[i])
            .Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One })
            .Set(new LocalToWorld())
            .Set(new Sprite { Tint = spriteColors[i], Size = new Vector2(1, 1), TextureHandle = -1, ZIndex = i });
    }

    // ── 粒子发射器 ──────────────────────────────────────────────────────────
    var fireEffect = new ParticleEffect
    {
        SpawnRate = 60f,
        MaxParticles = 500,
        Lifetime = 1.5f,
        LifetimeVariance = 0.5f,
        InitialVelocity = new Vector3(0, 2f, 0),
        SpeedVariance = 0.3f,
        Spread = 0.5f,
        BaseSize = 0.15f,
        Gravity = new Vector3(0, -1f, 0),
        Damping = 0.97f,
    };
    world.Entity("FireParticles")
        .Set(new LocalTransform
        {
            Position = new Vector3(5, 0, 0),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        })
        .Set(new LocalToWorld())
        .Set(new ParticleEmitter { Effect = fireEffect, Active = true });

    // ── 平行光 + 相机 ─────────────────────────────────────────────────────
    world.Entity("Sun")
        .Set(new DirectionalLight
        {
            Direction = new Vector3(-0.5f, -0.7f, -0.5f),
            Color = new Vector3(1.0f, 0.95f, 0.9f),
            Intensity = 3.0f
        });

    // ── 文字实体 ──────────────────────────────────────────────────────────
    world.Entity("HelloText")
        .Set(new LocalTransform
        {
            Position = new Vector3(-4f, 4f, 0),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        })
        .Set(new LocalToWorld())
        .Set(new TextRenderer
        {
            Text = "Hello Kilo!",
            Color = new Vector4(1f, 1f, 1f, 1f),
            FontSize = 24f
        });

    world.Entity("Camera")
        .Set(new LocalTransform
        {
            Position = new Vector3(0, 0, 12),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        })
        .Set(new Camera
        {
            FieldOfView = MathF.PI / 4,
            NearPlane = 0.1f,
            FarPlane = 100.0f,
            IsActive = true,
            RenderLayers = RenderLayers.Meshes | RenderLayers.Particles,
        });

    // ── UI 覆盖层相机（优先级高于主相机，后渲染） ──────────────────────────
    world.Entity("OverlayCamera")
        .Set(new LocalTransform
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        })
        .Set(new Camera
        {
            FieldOfView = MathF.PI / 4,
            NearPlane = 0.1f,
            FarPlane = 100.0f,
            IsActive = true,
            Priority = 2,
            CameraType = CameraType.UIOverlay,
            Target = CameraTarget.Screen,
            ClearSettings = CameraClearSettings.DontClear,
            PostProcessEnabled = false,
            RenderLayers = RenderLayers.Sprites | RenderLayers.Text,
        });

    // ── 俯视角小地图相机 ──────────────────────────────────────────────────
    var minimapRT = new RenderTexture(256, 256, driver.SwapchainFormat);
    world.Entity("MinimapCamera")
        .Set(new LocalTransform
        {
            Position = new Vector3(0, 15, 0),
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2f),
            Scale = Vector3.One
        })
        .Set(new Camera
        {
            FieldOfView = MathF.PI / 3,
            NearPlane = 0.1f,
            FarPlane = 100.0f,
            IsActive = true,
            Priority = 0,
            CameraType = CameraType.Scene,
            Target = CameraTarget.RenderTexture,
            RenderTexture = minimapRT,
            ClearSettings = CameraClearSettings.SolidColor(new Vector4(0.1f, 0.1f, 0.12f, 1f)),
            PostProcessEnabled = false,
            RenderLayers = RenderLayers.Meshes | RenderLayers.Particles,
        });
    RenderDemoPlugin.SetMinimapRenderTexture(minimapRT);
});

// ── 每帧更新：相机控制 + 立方体旋转 + 精灵动画 + 输入日志 ──────────────────
var _time = 0f;
var _lastTime = DateTime.Now;
app.AddSystem(KiloStage.Update, world =>
{
    var now = DateTime.Now;
    var deltaTime = (float)(now - _lastTime).TotalSeconds;
    _lastTime = now;
    _time += deltaTime;
    var input = world.GetResource<InputState>();

    // ── B 键切换 Compute Blur ─────────────────────────────────────────────
    if (input.IsKeyPressed((int)Key.B))
    {
        var settings = world.GetResource<RenderSettings>();
        settings.BloomEnabled = !settings.BloomEnabled;
        Console.WriteLine($"[RenderDemo] Bloom: {(settings.BloomEnabled ? "ON" : "OFF")}");
    }

    // ── P 键截图 ─────────────────────────────────────────────────────────
    if (input.IsKeyPressed((int)Key.P))
    {
        world.GetResource<RenderContext>().Screenshot.Requested = true;
        Console.WriteLine("[RenderDemo] Screenshot requested (P key)");
    }

    // ── N 键：动画播放/暂停 ───────────────────────────────────────────────
    if (input.IsKeyPressed((int)Key.N))
    {
        var animQuery = world.QueryBuilder().With<AnimationPlayer>().Build();
        var animIter = animQuery.Iter();
        while (animIter.Next())
        {
            var players = animIter.Data<AnimationPlayer>(animIter.GetColumnIndexOf<AnimationPlayer>());
            for (int i = 0; i < animIter.Count; i++)
            {
                players[i].IsPlaying = !players[i].IsPlaying;
                Console.WriteLine($"[RenderDemo] Animation: {(players[i].IsPlaying ? "PLAYING" : "PAUSED")}");
            }
        }
    }

    // ── M 键：动画循环切换 ─────────────────────────────────────────────────
    if (input.IsKeyPressed((int)Key.M))
    {
        var animQuery = world.QueryBuilder().With<AnimationPlayer>().Build();
        var animIter = animQuery.Iter();
        while (animIter.Next())
        {
            var players = animIter.Data<AnimationPlayer>(animIter.GetColumnIndexOf<AnimationPlayer>());
            for (int i = 0; i < animIter.Count; i++)
            {
                players[i].Loop = !players[i].Loop;
                Console.WriteLine($"[RenderDemo] Animation Loop: {(players[i].Loop ? "ON" : "OFF")}");
            }
        }
    }

    // ── G 键：切换 GLTF 动画片段 ───────────────────────────────────────────
    if (input.IsKeyPressed((int)Key.G))
    {
        var clipStore = world.GetResource<AnimationClipStore>();
        if (clipStore != null)
        {
            var gltfQuery = world.QueryBuilder().With<AnimationPlayer>().With<Skeleton>().Build();
            var gltfIter = gltfQuery.Iter();
            while (gltfIter.Next())
            {
                var players = gltfIter.Data<AnimationPlayer>(gltfIter.GetColumnIndexOf<AnimationPlayer>());
                var ents = gltfIter.Entities();
                for (int i = 0; i < gltfIter.Count; i++)
                {
                    var eid = (ulong)ents[i].ID;
                    if (!clipStore.EntityClips.TryGetValue(eid, out var clips) || clips.Count <= 1) continue;
                    var p = players[i];
                    p.ClipIndex = (p.ClipIndex + 1) % clips.Count;
                    p.Time = 0f;
                    p.IsPlaying = true;
                    players[i] = p;
                    Console.WriteLine($"[RenderDemo] Animation clip → {clips[p.ClipIndex].Name} ({p.ClipIndex + 1}/{clips.Count})");
                }
            }
        }
    }

    // ── 骨骼动画：旋转 AnimatedArm 下臂关节 (仅对无 clip 的手动动画实体) ──
    var animClipStore = world.GetResource<AnimationClipStore>();
    var playerQuery = world.QueryBuilder().With<AnimationPlayer>().With<Skeleton>().Build();
    var playerIter = playerQuery.Iter();
    while (playerIter.Next())
    {
        var players = playerIter.Data<AnimationPlayer>(playerIter.GetColumnIndexOf<AnimationPlayer>());
        var skeletons = playerIter.Data<Skeleton>(playerIter.GetColumnIndexOf<Skeleton>());
        var playerEntities = playerIter.Entities();
        for (int i = 0; i < playerIter.Count; i++)
        {
            // Skip entities that have GLTF clips (handled by AnimationUpdateSystem)
            if (animClipStore != null && animClipStore.EntityClips.ContainsKey((ulong)playerEntities[i].ID)) continue;

            var player = players[i];
            if (!player.IsPlaying) continue;

            player.Time += deltaTime;
            if (player.Loop && player.Time > MathF.PI * 2)
                player.Time -= MathF.PI * 2;
            players[i] = player;

            // Simple demo animation: rotate joint1 (lower arm) back and forth
            var skeleton = skeletons[i];
            if (skeleton.JointEntities.Length >= 2)
            {
                var joint1Id = new EntityId((ulong)skeleton.JointEntities[1]);
                if (world.Exists(joint1Id) && world.Has<LocalTransform>(joint1Id))
                {
                    float swingAngle = MathF.Sin(player.Time * 2f) * 0.8f;
                    var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, swingAngle);
                    world.Set(joint1Id, new LocalTransform
                    {
                        Position = new Vector3(0, 2, 0),
                        Rotation = rotation,
                        Scale = Vector3.One
                    });
                }
            }
        }
    }

    // ── 输入日志（按键/释放） ─────────────────────────────────────────────
    for (int k = 0; k < 512; k++)
    {
        if (input.KeysPressed[k])
            Console.WriteLine($"[Input] KeyDown: {(Key)k}");
        if (input.KeysReleased[k])
            Console.WriteLine($"[Input] KeyUp: {(Key)k}");
    }

    // ── WASD / 方向键 / QE 相机控制 ────────────────────────────────────────
    var camQuery = world.QueryBuilder()
        .With<LocalTransform>()
        .With<Camera>()
        .Build();

    var camIter = camQuery.Iter();
    while (camIter.Next())
    {
        var transforms = camIter.Data<LocalTransform>(camIter.GetColumnIndexOf<LocalTransform>());
        for (int i = 0; i < camIter.Count; i++)
        {
            var pos = transforms[i].Position;
            float speed = 0.15f;

            if (input.IsKeyDown((int)Key.W) || input.IsKeyDown((int)Key.Up)) pos.Z -= speed;
            if (input.IsKeyDown((int)Key.S) || input.IsKeyDown((int)Key.Down)) pos.Z += speed;
            if (input.IsKeyDown((int)Key.A) || input.IsKeyDown((int)Key.Left)) pos.X -= speed;
            if (input.IsKeyDown((int)Key.D) || input.IsKeyDown((int)Key.Right)) pos.X += speed;
            if (input.IsKeyDown((int)Key.Q)) pos.Y -= speed;
            if (input.IsKeyDown((int)Key.E)) pos.Y += speed;

            transforms[i].Position = pos;
        }
    }

    // ── 立方体自旋 ─────────────────────────────────────────────────────────
    var cubeQuery = world.QueryBuilder()
        .With<LocalTransform>()
        .With<MeshRenderer>()
        .Build();

    var cubeIter = cubeQuery.Iter();
    int index = 0;
    while (cubeIter.Next())
    {
        var transforms = cubeIter.Data<LocalTransform>(cubeIter.GetColumnIndexOf<LocalTransform>());
        for (int i = 0; i < cubeIter.Count; i++)
        {
            float spd = 1.0f + index * 0.3f;
            transforms[i].Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, _time * spd)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitX, _time * spd * 0.5f);
            index++;
        }
    }

    // ── 精灵动画（正弦波 / 圆周 / 8 字形） ──────────────────────────────────
    var spriteQuery = world.QueryBuilder()
        .With<LocalTransform>()
        .With<Sprite>()
        .Build();

    var spriteIter = spriteQuery.Iter();
    int si = 0;
    while (spriteIter.Next())
    {
        var transforms = spriteIter.Data<LocalTransform>(spriteIter.GetColumnIndexOf<LocalTransform>());
        for (int i = 0; i < spriteIter.Count; i++)
        {
            switch (si)
            {
                case 0: // 红色：垂直正弦波
                    transforms[i].Position.X = -2.5f;
                    transforms[i].Position.Y = MathF.Sin(_time * 2f) * 3f;
                    break;
                case 1: // 绿色：圆周轨道
                    transforms[i].Position.X = MathF.Cos(_time * 1.5f) * 3f;
                    transforms[i].Position.Y = MathF.Sin(_time * 1.5f) * 3f;
                    break;
                case 2: // 蓝色：8 字形
                    transforms[i].Position.X = 2.5f + MathF.Sin(_time * 2f) * 1f;
                    transforms[i].Position.Y = MathF.Sin(_time * 4f) * 2f;
                    break;
            }
            si++;
        }
    }
});

// ── 启动窗口主循环 ──────────────────────────────────────────────────────────
plugin.Run(app);

// =============================================================================
// RenderDemoPlugin
// =============================================================================
// 组合了 RenderingPlugin + ComputeBlur + InputPlugin 的完整插件。
// 使用 BlurRenderSystem 替代默认 RenderSystem，支持 3D + 后处理 + 2D 精灵。
// =============================================================================
public sealed class RenderDemoPlugin : IKiloPlugin
{
    private readonly RenderSettings _settings;
    private readonly string? _modelPath;
    private static RenderTexture? _minimapRT;

    public static void SetMinimapRenderTexture(RenderTexture rt) => _minimapRT = rt;

    public RenderDemoPlugin(RenderSettings? settings = null, string? modelPath = null)
    {
        _settings = settings ?? new RenderSettings();
        _modelPath = modelPath;
    }

    public void Build(KiloApp app)
    {
        // 资源
        app.AddResource(_settings);
        app.AddResource(new RenderContext
        {
            ShaderCache = new ShaderCache(),
            PipelineCache = new PipelineCache(),
        });
        app.AddResource(new WindowSize { Width = _settings.Width, Height = _settings.Height });
        app.AddResource(new GpuSceneData());
        app.AddResource(new ActiveCameraList());
        app.AddResource(new AnimationClipStore());
        // 输入
        app.AddResource(new InputState());
        app.AddResource(new InputSettings());

        // 系统
        app.AddSystem(KiloStage.Update, AnimationUpdateWrapper);
        app.AddSystem(KiloStage.PostUpdate, new LocalToWorldSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new SkinnedMeshPrepareSystem().Update);
        app.AddSystem(KiloStage.First, new CameraSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new FrustumCullingSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new CameraPrepareSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new ObjectPrepareSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new LightPrepareSystem().Update);

        app.AddSystem(KiloStage.Last, new BeginFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new ShadowMapSystem().Update);
        app.AddSystem(KiloStage.Last, new SkyboxRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new ParticleUpdateSystem().Update);
        app.AddSystem(KiloStage.Last, new CameraRenderLoopSystem().Update);
        app.AddSystem(KiloStage.Last, MinimapBlitSystem);
        app.AddSystem(KiloStage.Last, new EndFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new WindowResizeSystem().Update);
        // Reset per-frame input state AFTER all systems have read it
        app.AddSystem(KiloStage.Last, new InputPollSystem().Update);
    }

    private static DateTime _animLastTime = DateTime.Now;
    private static void AnimationUpdateWrapper(KiloWorld world)
    {
        var now = DateTime.Now;
        var deltaTime = (float)(now - _animLastTime).TotalSeconds;
        _animLastTime = now;
        new AnimationUpdateSystem().Update(world, deltaTime);
    }

    private static void MinimapBlitSystem(KiloWorld world)
    {
        var rt = _minimapRT;
        if (rt == null) return;

        var context = world.GetResource<RenderContext>();
        var driver = context.Driver!;
        var graph = context.RenderGraph;
        var ws = world.GetResource<WindowSize>();
        var pp = context.PostProcess;

        if (!pp.Initialized) return;

        rt.EnsureResources(driver);
        graph.RegisterExternalTexture("MinimapColor", rt.Texture!);

        int size = 200;
        int margin = 15;
        int x = ws.Width - size - margin;
        int y = ws.Height - size - margin;

        graph.AddPass("MinimapBlit", setup: pass =>
        {
            var minimapColor = pass.ImportTexture("MinimapColor", new TextureDescriptor
            {
                Width = rt.Width, Height = rt.Height,
                Format = rt.Format,
                Usage = TextureUsage.ShaderBinding,
            });
            pass.ReadTexture(minimapColor);

            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = ws.Width, Height = ws.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Load, DriverStoreAction.Store);
        }, execute: exeCtx =>
        {
            var minimapView = exeCtx.GetTextureView("MinimapColor");
            var bindingSet = driver.CreateBindingSetForPipeline(
                pp.BlitPipeline!, 0,
                textures: [new TextureBinding { Binding = 0, TextureView = minimapView }],
                samplers: [new SamplerBinding { Binding = 1, Sampler = pp.LinearSampler! }]);

            exeCtx.Encoder.SetViewport((uint)x, (uint)y, (uint)size, (uint)size);
            exeCtx.Encoder.SetScissor(x, y, (uint)size, (uint)size);
            exeCtx.Encoder.SetPipeline(pp.BlitPipeline!);
            exeCtx.Encoder.SetBindingSet(0, bindingSet);
            exeCtx.Encoder.Draw(3);
        });
    }

    // ── GLTF 默认模型搜索 ──────────────────────────────────────────────────
    private static string? FindDefaultModel()
    {
        var candidates = new[]
        {
            Path.Combine("docs-3rd", "bevy-main", "assets", "models", "animated", "Fox.glb"),
            Path.Combine("docs-3rd", "bevy-main", "assets", "models", "FlightHelmet", "FlightHelmet.gltf"),
            Path.Combine("docs-3rd", "bevy-main", "assets", "models", "CornellBox", "CornellBox.glb"),
        };

        // Try current working directory first
        foreach (var c in candidates)
            if (File.Exists(c)) return Path.GetFullPath(c);

        // Walk up from BaseDirectory to find project root
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            foreach (var c in candidates)
            {
                var path = Path.Combine(dir, c);
                if (File.Exists(path)) return path;
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == null) break;
            dir = parent;
        }

        return null;
    }

    // ── GLTF 实体创建（静态 + 骨骼动画） ────────────────────────────────────
    private static void SetupGltfEntities(
        KiloWorld world, GltfModel gltfModel,
        IRenderDriver driver, RenderContext context, GpuSceneData scene)
    {
        // Compute model scale from actual bounding box
        var extent = gltfModel.BBoxMax - gltfModel.BBoxMin;
        float maxExtent = Math.Max(Math.Max(extent.X, extent.Y), extent.Z);
        float modelScale = maxExtent > 0 ? 3.0f / maxExtent : 1f; // Fit model in ~3 units
        var center = (gltfModel.BBoxMin + gltfModel.BBoxMax) * 0.5f;
        var entityPosition = new Vector3(-center.X * modelScale, -gltfModel.BBoxMin.Y * modelScale + 0.5f, -center.Z * modelScale);

        Console.WriteLine($"[Kilo] Model BBox: [{gltfModel.BBoxMin.X:F1},{gltfModel.BBoxMin.Y:F1},{gltfModel.BBoxMin.Z:F1}] to [{gltfModel.BBoxMax.X:F1},{gltfModel.BBoxMax.Y:F1},{gltfModel.BBoxMax.Z:F1}], scale={modelScale:F4}");

        if (!gltfModel.IsSkinned || gltfModel.Skeleton == null)
        {
            // Static model: create one entity per primitive with MeshRenderer
            foreach (var (meshHandle, materialHandle) in gltfModel.Primitives)
            {
                world.Entity($"GltfMesh_{meshHandle}")
                    .Set(new LocalTransform
                    {
                        Position = entityPosition,
                        Rotation = Quaternion.Identity,
                        Scale = new Vector3(modelScale)
                    })
                    .Set(new LocalToWorld())
                    .Set(new MeshRenderer { MeshHandle = meshHandle, MaterialHandle = materialHandle });
            }
            return;
        }

        // ── Skinned model: create joint hierarchy + skinned renderer ────────
        var skeletonData = gltfModel.Skeleton;

        // 1) Create joint entities with hierarchy
        var jointEntities = new int[skeletonData.Joints.Length];
        for (int j = 0; j < skeletonData.Joints.Length; j++)
        {
            var joint = skeletonData.Joints[j];
            var jointEntity = world.Entity($"GltfJoint_{joint.Name}")
                .Set(new LocalTransform
                {
                    Position = joint.RestPosition,
                    Rotation = joint.RestRotation,
                    Scale = joint.RestScale
                })
                .Set(new LocalToWorld());

            if (joint.ParentIndex >= 0)
            {
                jointEntity.Set(new Parent { Id = new EntityId((ulong)jointEntities[joint.ParentIndex]) });
            }

            jointEntities[j] = (int)(uint)jointEntity.Id.Value;
        }
        gltfModel.JointEntityIds = jointEntities;

        // 2) Create skinned pipeline
        var skinnedVs = context.ShaderCache.GetOrCreateShader(driver, SkinnedLitShaders.WGSL, "vs_main");
        var skinnedFs = context.ShaderCache.GetOrCreateShader(driver, SkinnedLitShaders.WGSL, "fs_main");

        var skinnedPipeline = context.PipelineCache.GetOrCreate(driver, new PipelineCacheKey
        {
            VertexShaderSource = SkinnedLitShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = SkinnedLitShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = [SkinnedMesh.Layout],
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA16Float }],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            }
        }, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = skinnedVs,
            FragmentShader = skinnedFs,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA16Float }],
            VertexBuffers = [SkinnedMesh.Layout],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            }
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 5));

        // Shared binding sets (groups 0-3)
        var cameraBS = driver.CreateBindingSetForPipeline(skinnedPipeline, 0,
            [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);
        var objectBS = driver.CreateDynamicUniformBindingSet(skinnedPipeline, 1,
            scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        var lightBS = driver.CreateBindingSetForPipeline(skinnedPipeline, 2,
            [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // Texture + shadow binding set (group 3)
        var defaultSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear, MagFilter = FilterMode.Linear, MipFilter = FilterMode.Linear,
            AddressModeU = WrapMode.Repeat, AddressModeV = WrapMode.Repeat, AddressModeW = WrapMode.Repeat,
        });

        // 3) Create skinned material + joint buffer per primitive
        var gltfEntity = world.Entity("GltfSkinnedModel")
            .Set(new LocalTransform
            {
                Position = entityPosition,
                Rotation = Quaternion.Identity,
                Scale = new Vector3(modelScale)
            })
            .Set(new LocalToWorld())
            .Set(new Skeleton { Data = skeletonData, JointEntities = jointEntities })
            .Set(new AnimationPlayer { ClipIndex = 0, Time = 0f, IsPlaying = true, Loop = true });

        // Register animation clips in the store
        var clipStore = world.GetResource<AnimationClipStore>();
        if (clipStore != null && gltfModel.Animations.Count > 0)
        {
            clipStore.EntityClips[(ulong)gltfEntity.Id.Value] = gltfModel.Animations;
            Console.WriteLine($"[Kilo] Animations: {string.Join(", ", gltfModel.Animations.Select(a => $"{a.Name}({a.Duration:F2}s)"))}");
        }

        // For skinned meshes, we use the first primitive's mesh for the SkinnedMeshRenderer
        // (Multi-primitive skinned models would need multiple renderers, simplified here)
        var (firstMeshHandle, firstMatHandle) = gltfModel.Primitives[0];

        // Re-create material with skinned pipeline using GLTF texture if available
        var origMaterial = context.Materials[firstMatHandle];
        var albedoTexture = origMaterial.AlbedoTexture;
        var albedoSampler = origMaterial.AlbedoSampler ?? defaultSampler;

        ITextureView albedoView;
        if (albedoTexture != null)
        {
            albedoView = driver.CreateTextureView(albedoTexture, new TextureViewDescriptor
            {
                Format = DriverPixelFormat.RGBA8Unorm, Dimension = TextureViewDimension.View2D, MipLevelCount = 1,
            });
        }
        else
        {
            var fallback = driver.CreateTexture(new TextureDescriptor
            {
                Width = 1, Height = 1, Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
            });
            fallback.UploadData<byte>([255, 255, 255, 255]);
            albedoView = driver.CreateTextureView(fallback, new TextureViewDescriptor
            {
                Format = DriverPixelFormat.RGBA8Unorm, Dimension = TextureViewDimension.View2D, MipLevelCount = 1,
            });
        }

        // Joint matrix buffer (group 4)
        var jointBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(SkeletonData.MaxJoints * 64),
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        // Group 3: texture bindings — centralized via MaterialManager
        var textureBS = context.MaterialManager.CreateTextureBindingSet(driver, skinnedPipeline, scene, albedoView, albedoView);

        // Group 4: joint matrices
        var jointBS = driver.CreateBindingSetForPipeline(skinnedPipeline, 4,
            [new UniformBufferBinding { Buffer = jointBuffer, Binding = 0 }]);

        var skinnedMaterial = new Material
        {
            Pipeline = skinnedPipeline,
            BindingSets = [cameraBS, objectBS, lightBS, textureBS, jointBS],
            AlbedoTexture = albedoTexture,
            AlbedoSampler = albedoSampler,
        };
        int skinnedMatHandle = context.AddMaterial(skinnedMaterial);

        // Additional primitives as children of the main entity so they share the model transform
        for (int p = 0; p < gltfModel.Primitives.Count; p++)
        {
            var (meshH, _) = gltfModel.Primitives[p];
            var targetEntity = p == 0 ? gltfEntity : world.Entity($"GltfSkinnedPart_{p}")
                .Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One })
                .Set(new LocalToWorld())
                .Set(new Parent { Id = new EntityId(gltfEntity.Id.Value) });

            targetEntity.Set(new SkinnedMeshRenderer
            {
                MeshHandle = meshH,
                MaterialHandle = skinnedMatHandle,
                JointMatrixBuffer = jointBuffer,
                JointBindingSet = jointBS,
            });
        }
    }

    public void Run(KiloApp app)
    {
        Console.WriteLine("[Kilo] Creating window...");
        var window = WindowHelper.CreateWindow(_settings.Width, _settings.Height, _settings.Title, _settings.VSync);
        var context = app.World.GetResource<RenderContext>();
        var scene = app.World.GetResource<GpuSceneData>();

        window.Load += () =>
        {
            Console.WriteLine("[Kilo] Window loaded, initializing WebGPU...");
            var driver = WebGPUDriverFactory.Create(window, _settings);
            context.Driver = driver;
            SceneInitializer.Initialize(context, scene, driver);
            InputWiring.WireInputEvents(window, app.World);

            // Load GLTF model if path provided
            var modelPath = _modelPath ?? FindDefaultModel();
            if (modelPath != null && File.Exists(modelPath))
            {
                Console.WriteLine($"[Kilo] Loading GLTF model: {modelPath}");
                var gltfModel = GltfLoader.Load(modelPath, driver, context, scene);
                SetupGltfEntities(app.World, gltfModel, driver, context, scene);
                Console.WriteLine($"[Kilo] Loaded {gltfModel.Primitives.Count} primitives, " +
                    $"Skinned={gltfModel.IsSkinned}, Animations={gltfModel.Animations.Count}");
            }

            Console.WriteLine("[Kilo] WebGPU initialized. WASD=Move, QE=Up/Down, B=Toggle Blur, P=Screenshot, N=Play/Pause, M=Toggle Loop, G=Switch Animation");
        };

        var _frameCount = 0;
        window.Render += _ =>
        {
            app.Update();
            _frameCount++;

            // Auto-screenshot after 30 frames (skip first frames for initialization)
            if (_frameCount == 30)
            {
                context.Screenshot.Requested = true;
                Console.WriteLine($"[Kilo] Auto-screenshot at frame {_frameCount}");
            }

            // Process screenshot readback after render graph execution
            if (context.Screenshot.HasPending && context.Screenshot.Buffer != null)
            {
                context.Screenshot.HasPending = false;
                var driver = context.Driver;
                var width = context.Screenshot.Width;
                var height = context.Screenshot.Height;
                var alignedBytesPerRow = context.Screenshot.AlignedBytesPerRow;
                var requiredSize = (nuint)(alignedBytesPerRow * height);
                var pixelData = driver.ReadBufferSync(context.Screenshot.Buffer, 0, requiredSize);
                context.Screenshot.Buffer = null;
                SaveScreenshot(width, height, alignedBytesPerRow, pixelData);
            }
        };

        window.Resize += size =>
        {
            var ws = app.World.GetResource<WindowSize>();
            ws.Width = size.X;
            ws.Height = size.Y;
            context.WindowResized = true;
        };

        window.Closing += () =>
        {
            context.RenderGraph.Dispose();
            context.Driver?.Dispose();
        };

        window.Run();
        window.Dispose();
    }

    // ── 截图保存为 PNG ────────────────────────────────────────────────────────
    private static void SaveScreenshot(int width, int height, uint alignedBytesPerRow, byte[] pixelData)
    {
        var blurLabel = "pp";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"screenshot_{blurLabel}_{timestamp}.png";

        using var image = new Image<Rgba32>(width, height);

        // Source is the swapchain backbuffer (BGRA8UnormSrgb) — layout is B,G,R,A
        // WebGPU textures are top-down, matching PNG coordinate convention — no Y-flip needed
        for (int y = 0; y < height; y++)
        {
            var srcRow = y * alignedBytesPerRow;
            for (int x = 0; x < width; x++)
            {
                var pixelOffset = (int)(srcRow + x * 4);
                image[x, y] = new Rgba32(
                    pixelData[pixelOffset + 2], // R (from BGRA[2])
                    pixelData[pixelOffset + 1], // G (from BGRA[1])
                    pixelData[pixelOffset],     // B (from BGRA[0])
                    pixelData[pixelOffset + 3]  // A (from BGRA[3])
                );
            }
        }

        image.SaveAsPng(filename);
        Console.WriteLine($"[Screenshot] Saved: {filename} ({width}x{height})");
    }
}

