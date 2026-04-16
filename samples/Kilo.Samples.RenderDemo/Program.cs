// =============================================================================
// Kilo.Samples.RenderDemo — Unified Rendering + Input Demo
// =============================================================================
// 本示例整合了所有渲染和输入功能：
//   1) 3D 前向渲染：5 个彩色旋转立方体 + 平行光照 + 相机控制
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
using Kilo.Input;
using Kilo.Rendering;
using Kilo.Rendering.Assets;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Driver.WebGPUImpl;
using Kilo.Rendering.RenderGraph;
using Kilo.Rendering.Resources;
using Kilo.Rendering.Shaders;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Parent = TinyEcs.Parent;
using JointInfo = Kilo.Rendering.Resources.JointInfo;

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

    // ── 5 个不同颜色的立方体 ──────────────────────────────────────────────
    var colors = new[]
    {
        new Vector4(1.0f, 0.3f, 0.3f, 1.0f), // Red
        new Vector4(0.3f, 1.0f, 0.3f, 1.0f), // Green
        new Vector4(0.3f, 0.3f, 1.0f, 1.0f), // Blue
        new Vector4(1.0f, 1.0f, 0.3f, 1.0f), // Yellow
        new Vector4(1.0f, 0.3f, 1.0f, 1.0f), // Magenta
    };

    var materialIds = new int[colors.Length];
    for (int i = 0; i < colors.Length; i++)
    {
        materialIds[i] = context.MaterialManager.CreateMaterial(context, scene, new MaterialDescriptor
        {
            BaseColor = colors[i],
        });
    }

    for (int i = 0; i < 5; i++)
    {
        float x = (i - 2) * 2.0f;
        world.Entity($"Cube{i}")
            .Set(new LocalTransform
            {
                Position = new Vector3(x, 0, 0),
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
            AlbedoTexturePath = tempPath
        });
        world.Entity("TexturedCube")
            .Set(new LocalTransform
            {
                Position = new Vector3(6, 0, 0),
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
        // Each vertex: pos(3) + normal(3) + uv(2) + joints(4 uint) + weights(4 float) = 64 bytes
        var skinnedVertices = new byte[8 * 64]; // 8 vertices

        for (int v = 0; v < 8; v++)
        {
            int offset = v * 64;

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

            // Joints (16 bytes) - 4 uint32
            BitConverter.GetBytes(jointIndex).CopyTo(skinnedVertices, offset + 32);
            BitConverter.GetBytes(0u).CopyTo(skinnedVertices, offset + 36);
            BitConverter.GetBytes(0u).CopyTo(skinnedVertices, offset + 40);
            BitConverter.GetBytes(0u).CopyTo(skinnedVertices, offset + 44);

            // Weights (16 bytes) - full weight to jointIndex
            BitConverter.GetBytes(1f).CopyTo(skinnedVertices, offset + 48);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 52);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 56);
            BitConverter.GetBytes(0f).CopyTo(skinnedVertices, offset + 60);
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
        int skinnedMeshHandle = context.Meshes.Count;
        context.Meshes.Add(skinnedMesh);

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
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA8Unorm }],
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

        // Reuse shadow bindings
        var shadowDataBuffer = context.ShadowDataBuffer;
        var shadowSampler = context.ShadowSampler;
        var placeholderDepthTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.Depth24Plus,
            Usage = TextureUsage.ShaderBinding | TextureUsage.RenderAttachment,
        });
        var placeholderDepthView = driver.CreateTextureView(placeholderDepthTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.Depth24Plus,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        // Group 3: texture bindings
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

        var textureBindingSet = driver.CreateBindingSetForPipeline(skinnedPipeline, 3,
            [new UniformBufferBinding { Buffer = shadowDataBuffer, Binding = 4 }],
            [
                new TextureBinding { Binding = 0, TextureView = defaultTextureView },
                new TextureBinding { Binding = 2, TextureView = placeholderDepthView },
            ],
            [
                new SamplerBinding { Binding = 1, Sampler = defaultSampler },
                new SamplerBinding { Binding = 3, Sampler = shadowSampler },
            ]);

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
        int skinnedMaterialHandle = context.Materials.Count;
        context.Materials.Add(skinnedMaterial);

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

    // ── 平行光 + 相机 ─────────────────────────────────────────────────────
    world.Entity("Sun")
        .Set(new DirectionalLight
        {
            Direction = new Vector3(0.0f, -0.5f, -1.0f),
            Color = new Vector3(1.0f, 0.95f, 0.9f),
            Intensity = 1.0f
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
            Position = new Vector3(0, 2, 10),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        })
        .Set(new Camera
        {
            FieldOfView = MathF.PI / 4,
            NearPlane = 0.1f,
            FarPlane = 100.0f,
            IsActive = true
        });
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
        BlurRenderSystem.BlurEnabled = !BlurRenderSystem.BlurEnabled;
        Console.WriteLine($"[RenderDemo] Blur: {(BlurRenderSystem.BlurEnabled ? "ON" : "OFF")}");
    }

    // ── P 键截图 ─────────────────────────────────────────────────────────
    if (input.IsKeyPressed((int)Key.P))
    {
        BlurRenderSystem.ScreenshotRequested = true;
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
        app.AddResource(new AnimationClipStore());
        // 输入
        app.AddResource(new InputState());
        app.AddResource(new InputSettings());

        // 系统
        app.AddSystem(KiloStage.Update, AnimationUpdateWrapper);
        app.AddSystem(KiloStage.PostUpdate, ComputeLocalToWorld);
        app.AddSystem(KiloStage.PostUpdate, new SkinnedMeshPrepareSystem().Update);
        app.AddSystem(KiloStage.First, new CameraSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new FrustumCullingSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new PrepareGpuSceneSystem().Update);

        app.AddSystem(KiloStage.Last, new BeginFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new ShadowMapSystem().Update);
        app.AddSystem(KiloStage.Last, new BlurRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new SpriteRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new TextRenderSystem().Update);
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
        // Calculate model bounding box from mesh data for auto-scaling
        float maxExtent = 0f;
        foreach (var (meshHandle, _) in gltfModel.Primitives)
        {
            if (meshHandle >= 0 && meshHandle < context.Meshes.Count)
            {
                var vb = context.Meshes[meshHandle].VertexBuffer;
                maxExtent = Math.Max(maxExtent, (float)vb.Size);
            }
        }

        // Compute a scale factor to fit the model in ~3 unit height for the camera view
        // We don't have vertex data on CPU anymore, so use a heuristic based on mesh buffer size
        // Fox.glb: 1728 verts * 64 bytes = 110592 bytes, ~79 units tall
        float modelScale = 1f / 40f; // Shrink to fit camera view
        var entityPosition = new Vector3(0, -1, 0);

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
                    Position = Vector3.Zero,
                    Rotation = Quaternion.Identity,
                    Scale = Vector3.One
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
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA8Unorm }],
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
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA8Unorm }],
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
        var shadowSampler = context.ShadowSampler!;
        var shadowDataBuffer = context.ShadowDataBuffer!;

        var placeholderDepth = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1, Format = DriverPixelFormat.Depth24Plus,
            Usage = TextureUsage.ShaderBinding | TextureUsage.RenderAttachment,
        });
        var placeholderDepthView = driver.CreateTextureView(placeholderDepth, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.Depth24Plus, Dimension = TextureViewDimension.View2D, MipLevelCount = 1,
        });

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
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI),
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

        var textureBS = driver.CreateBindingSetForPipeline(skinnedPipeline, 3,
            [new UniformBufferBinding { Buffer = shadowDataBuffer, Binding = 4 }],
            [
                new TextureBinding { Binding = 0, TextureView = albedoView },
                new TextureBinding { Binding = 2, TextureView = placeholderDepthView },
            ],
            [
                new SamplerBinding { Binding = 1, Sampler = albedoSampler },
                new SamplerBinding { Binding = 3, Sampler = shadowSampler },
            ]);

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
        int skinnedMatHandle = context.Materials.Count;
        context.Materials.Add(skinnedMaterial);

        // Additional primitives as separate entities sharing the skeleton
        for (int p = 0; p < gltfModel.Primitives.Count; p++)
        {
            var (meshH, _) = gltfModel.Primitives[p];
            var targetEntity = p == 0 ? gltfEntity : world.Entity($"GltfSkinnedPart_{p}")
                .Set(new LocalTransform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One })
                .Set(new LocalToWorld());

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
        var window = CreateWindow(_settings);
        var context = app.World.GetResource<RenderContext>();
        var scene = app.World.GetResource<GpuSceneData>();

        window.Load += () =>
        {
            Console.WriteLine("[Kilo] Window loaded, initializing WebGPU...");
            var driver = WebGPUDriverFactory.Create(window, _settings);
            context.Driver = driver;
            InitializeResources(context, scene, driver);
            WireInputEvents(window, app.World);

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

        window.Render += _ =>
        {
            app.Update();

            // Process screenshot readback after render graph execution
            if (BlurRenderSystem.HasPendingScreenshot && BlurRenderSystem.ScreenshotBuffer != null)
            {
                BlurRenderSystem.HasPendingScreenshot = false;
                var driver = context.Driver;
                var width = BlurRenderSystem.PendingScreenshotWidth;
                var height = BlurRenderSystem.PendingScreenshotHeight;
                var alignedBytesPerRow = BlurRenderSystem.ScreenshotAlignedBytesPerRow;
                var requiredSize = (nuint)(alignedBytesPerRow * height);
                var pixelData = driver.ReadBufferSync(BlurRenderSystem.ScreenshotBuffer, 0, requiredSize);
                BlurRenderSystem.ScreenshotBuffer = null;
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

    private static IWindow CreateWindow(RenderSettings settings)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(settings.Width, settings.Height);
        options.Title = settings.Title;
        options.VSync = settings.VSync;
        options.API = GraphicsAPI.None;
        options.IsContextControlDisabled = true;
        options.ShouldSwapAutomatically = false;
        return Window.Create(options);
    }

    // ── 输入事件连线 ─────────────────────────────────────────────────────────
    private static void WireInputEvents(IWindow window, KiloWorld world)
    {
        var inputState = world.GetResource<InputState>();
        var inputContext = window.CreateInput();

        foreach (var keyboard in inputContext.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) =>
            {
                int code = (int)key;
                if (code >= 0 && code < 512)
                {
                    inputState.KeysDown[code] = true;
                    inputState.KeysPressed[code] = true;
                }
            };
            keyboard.KeyUp += (_, key, _) =>
            {
                int code = (int)key;
                if (code >= 0 && code < 512)
                {
                    inputState.KeysDown[code] = false;
                    inputState.KeysReleased[code] = true;
                }
            };
        }

        foreach (var mouse in inputContext.Mice)
        {
            mouse.MouseMove += (_, position) =>
            {
                var newPos = new Vector2((float)position.X, (float)position.Y);
                inputState.MouseDelta += newPos - inputState.MousePosition;
                inputState.MousePosition = newPos;
            };
            mouse.MouseDown += (_, button) =>
            {
                int idx = (int)button;
                if (idx >= 0 && idx < 5) inputState.MouseButtonsDown[idx] = true;
            };
            mouse.MouseUp += (_, button) =>
            {
                int idx = (int)button;
                if (idx >= 0 && idx < 5) inputState.MouseButtonsDown[idx] = false;
            };
            mouse.Scroll += (_, offset) =>
            {
                inputState.ScrollDelta += (float)offset.Y;
            };
        }
    }

    // ── GPU 资源初始化：3D + 2D + 后处理 ──────────────────────────────────────
    private static void InitializeResources(RenderContext context, GpuSceneData scene, IRenderDriver driver)
    {
        // --- GPU 场景缓冲区 ---
        const int ObjectBufferSize = 64 * 1024;
        const int LightBufferSize = 4 * 1024;

        scene.CameraBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)CameraData.Size,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
        scene.ObjectDataBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)ObjectBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
        scene.LightBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)LightBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        // --- 默认白色 1x1 纹理 + 采样器 ---
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
        var defaultSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear, MagFilter = FilterMode.Linear, MipFilter = FilterMode.Linear,
            AddressModeU = WrapMode.Repeat, AddressModeV = WrapMode.Repeat, AddressModeW = WrapMode.Repeat,
        });

        // --- Cube 网格 (pos3 + normal3 + uv2 = 8 floats) ---
        float[] cubeVertices =
        [
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
        ];
        uint[] cubeIndices =
        [
            0, 1, 2,  0, 2, 3,
            4, 5, 6,  4, 6, 7,
            8, 9, 10,  8, 10, 11,
            12, 13, 14,  12, 14, 15,
            16, 17, 18,  16, 18, 19,
            20, 21, 22,  20, 22, 23,
        ];

        var cubeVertexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(cubeVertices.Length * sizeof(float)),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        cubeVertexBuffer.UploadData<float>(cubeVertices);
        var cubeIndexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(cubeIndices.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        cubeIndexBuffer.UploadData<uint>(cubeIndices);

        var cubeMesh = new Mesh
        {
            VertexBuffer = cubeVertexBuffer,
            IndexBuffer = cubeIndexBuffer,
            IndexCount = (uint)cubeIndices.Length,
            Layouts =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 8 * sizeof(float),
                    Attributes =
                    [
                        new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x3, Offset = 0 },
                        new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x3, Offset = (nuint)(3 * sizeof(float)) },
                        new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Float32x2, Offset = (nuint)(6 * sizeof(float)) },
                    ]
                }
            ]
        };
        context.Meshes.Add(cubeMesh);

        // --- BasicLit 材质管线 ---
        var basicLitVS = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "vs_main");
        var basicLitFS = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "fs_main");

        // 注意：3D 渲染目标是 RGBA8Unorm（离屏纹理），不是 swapchain 格式
        var basicLitPipelineKey = new PipelineCacheKey
        {
            VertexShaderSource = BasicLitShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = BasicLitShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = cubeMesh.Layouts,
            ColorTargets = [new ColorTargetDescriptor { Format = DriverPixelFormat.RGBA8Unorm }],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            }
        };

        var basicLitPipeline = context.PipelineCache.GetOrCreate(driver, basicLitPipelineKey, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = basicLitVS,
            FragmentShader = basicLitFS,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = basicLitPipelineKey.ColorTargets,
            VertexBuffers = cubeMesh.Layouts,
            DepthStencil = basicLitPipelineKey.DepthStencil,
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 4));

        var cameraBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 0, [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);
        var objectBindingSet = driver.CreateDynamicUniformBindingSet(basicLitPipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);
        var lightBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 2, [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // Shadow resources (merged into group 3)
        var shadowSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            AddressModeU = WrapMode.ClampToEdge,
            AddressModeV = WrapMode.ClampToEdge,
            Compare = true,
            CompareFunction = DriverCompareFunction.Less,
        });
        var shadowDataBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 256,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
        context.ShadowDataBuffer = shadowDataBuffer;
        context.ShadowSampler = shadowSampler;

        // Placeholder depth texture for shadow_map binding (must be Depth24Plus format)
        var placeholderDepthTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.Depth24Plus,
            Usage = TextureUsage.ShaderBinding | TextureUsage.RenderAttachment,
        });
        var placeholderDepthView = driver.CreateTextureView(placeholderDepthTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.Depth24Plus,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        // Group 3: albedo_texture(0) + albedo_sampler(1) + shadow_map(2) + shadow_sampler(3) + shadow_data(4)
        var textureBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 3,
            [new UniformBufferBinding { Buffer = shadowDataBuffer, Binding = 4 }],
            [
                new TextureBinding { Binding = 0, TextureView = defaultTextureView },
                new TextureBinding { Binding = 2, TextureView = placeholderDepthView },
            ],
            [
                new SamplerBinding { Binding = 1, Sampler = defaultSampler },
                new SamplerBinding { Binding = 3, Sampler = shadowSampler },
            ]);

        var basicLitMaterial = new Material
        {
            Pipeline = basicLitPipeline,
            BindingSets = [cameraBindingSet, objectBindingSet, lightBindingSet, textureBindingSet],
            AlbedoTexture = defaultTexture,
            AlbedoSampler = defaultSampler,
        };
        context.Materials.Add(basicLitMaterial);

        // --- 精灵管线 ---
        var spriteVS = context.ShaderCache.GetOrCreateShader(driver, SpriteShaders.WGSL, "vs_main");
        var spriteFS = context.ShaderCache.GetOrCreateShader(driver, SpriteShaders.WGSL, "fs_main");

        const int UniformStructSize = 144;
        const int UniformAlign = 256;
        const int MaxSprites = 64;
        const int UniformBufferSize = UniformAlign * MaxSprites;
        var swapchainFormat = driver.SwapchainFormat;

        var spritePipelineKey = new PipelineCacheKey
        {
            VertexShaderSource = SpriteShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = SpriteShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 2 * sizeof(float),
                    Attributes = [new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x2, Offset = 0 }]
                }
            ],
            ColorTargets =
            [
                new ColorTargetDescriptor
                {
                    Format = swapchainFormat,
                    Blend = new BlendStateDescriptor
                    {
                        Color = new BlendComponentDescriptor
                        {
                            SrcFactor = DriverBlendFactor.SrcAlpha,
                            DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                        },
                        Alpha = new BlendComponentDescriptor
                        {
                            SrcFactor = DriverBlendFactor.One,
                            DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                        }
                    }
                }
            ],
            DepthStencil = null,
        };

        context.SpritePipeline = context.PipelineCache.GetOrCreate(driver, spritePipelineKey, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = spriteVS,
            FragmentShader = spriteFS,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = spritePipelineKey.ColorTargets,
            VertexBuffers = spritePipelineKey.VertexBuffers,
        }, (nuint)UniformStructSize, groupIndex: 0, bindGroupCount: 1));

        float[] quadVertices = [-0.5f, 0.5f, 0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f];
        uint[] quadIndices = [0u, 1, 2, 2, 1, 3];

        context.QuadVertexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(quadVertices.Length * sizeof(float)),
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        context.QuadVertexBuffer.UploadData<float>(quadVertices);
        context.QuadIndexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)(quadIndices.Length * sizeof(uint)),
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });
        context.QuadIndexBuffer.UploadData<uint>(quadIndices);
        context.UniformBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = (nuint)UniformBufferSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });
        context.BindingSet = driver.CreateDynamicUniformBindingSet(
            context.SpritePipeline, 0, context.UniformBuffer, UniformStructSize);
    }

    // ── Sprite 着色器 ────────────────────────────────────────────────────────
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

    // ── 截图保存为 PNG ────────────────────────────────────────────────────────
    private static void SaveScreenshot(int width, int height, uint alignedBytesPerRow, byte[] pixelData)
    {
        var blurLabel = BlurRenderSystem.BlurEnabled ? "blur_on" : "blur_off";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"screenshot_{blurLabel}_{timestamp}.png";

        using var image = new Image<Rgba32>(width, height);

        // Copy pixel data from aligned buffer rows to the image
        // Source texture is RGBA8Unorm (offscreen) — 4 bytes per pixel
        // Flip vertically: GPU textures are top-down, image coordinates are bottom-up
        for (int y = 0; y < height; y++)
        {
            var srcRow = y * alignedBytesPerRow;
            var dstY = height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                var pixelOffset = (int)(srcRow + x * 4);
                image[x, dstY] = new Rgba32(
                    pixelData[pixelOffset],     // R
                    pixelData[pixelOffset + 1], // G
                    pixelData[pixelOffset + 2], // B
                    pixelData[pixelOffset + 3]  // A
                );
            }
        }

        image.SaveAsPng(filename);
        Console.WriteLine($"[Screenshot] Saved: {filename} ({width}x{height}, blur={BlurRenderSystem.BlurEnabled})");
    }

    private static void ComputeLocalToWorld(KiloWorld world)
    {
        var query = world.QueryBuilder()
            .With<LocalTransform>()
            .With<LocalToWorld>()
            .Build();

        var iter = query.Iter();
        while (iter.Next())
        {
            var transforms = iter.Data<LocalTransform>(iter.GetColumnIndexOf<LocalTransform>());
            var worlds = iter.Data<LocalToWorld>(iter.GetColumnIndexOf<LocalToWorld>());
            var entities = iter.Entities();

            for (int i = 0; i < iter.Count; i++)
            {
                ref readonly var t = ref transforms[i];
                var localMatrix =
                    Matrix4x4.CreateScale(t.Scale)
                    * Matrix4x4.CreateFromQuaternion(t.Rotation)
                    * Matrix4x4.CreateTranslation(t.Position);

                var entityId = new EntityId(entities[i].ID);
                if (world.Has<Parent>(entityId))
                {
                    var parentId = new EntityId(world.Get<Parent>(entityId).Id);
                    if (world.Exists(parentId) && world.Has<LocalToWorld>(parentId))
                    {
                        ref readonly var parentWorld = ref world.Get<LocalToWorld>(parentId);
                        worlds[i].Value = parentWorld.Value * localMatrix;
                        continue;
                    }
                }

                worlds[i].Value = localMatrix;
            }
        }
    }
}

// =============================================================================
// BlurRenderSystem
// =============================================================================
// 带 Blur 开关的渲染系统。3D 场景渲染到离屏纹理，可选计算模糊后 Blit 到 Backbuffer。
// SpriteRenderSystem 随后在 Backbuffer 上绘制 2D 精灵。
// =============================================================================
public sealed class BlurRenderSystem
{
    private IComputePipeline? _blurHPipeline;
    private IComputePipeline? _blurVPipeline;
    private IRenderPipeline? _blitPipeline;
    private ISampler? _blitSampler;
    private IBuffer? _screenshotBuffer;
    private int _bufferWidth;
    private int _bufferHeight;

    public static bool BlurEnabled { get; set; } = false;
    public static bool ScreenshotRequested { get; set; } = false;
    public static bool HasPendingScreenshot { get; set; } = false;
    public static IBuffer? ScreenshotBuffer { get; set; }
    public static uint ScreenshotAlignedBytesPerRow { get; private set; }
    public static int PendingScreenshotWidth { get; private set; }
    public static int PendingScreenshotHeight { get; private set; }

    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;
        var scene = world.GetResource<GpuSceneData>();
        var ws = world.GetResource<WindowSize>();
        var graph = context.RenderGraph;

        // ── 延迟创建管线（仅首次） ──────────────────────────────────────────
        if (_blurHPipeline == null)
        {
            var hShader = context.ShaderCache.GetOrCreateComputeShader(driver, ComputeBlurShaders.BlurHorizontalWGSL, "main");
            _blurHPipeline = driver.CreateComputePipeline(hShader, "main");
            var vShader = context.ShaderCache.GetOrCreateComputeShader(driver, ComputeBlurShaders.BlurVerticalWGSL, "main");
            _blurVPipeline = driver.CreateComputePipeline(vShader, "main");
        }
        if (_blitPipeline == null)
        {
            var blitVs = context.ShaderCache.GetOrCreateShader(driver, ComputeBlurShaders.FullscreenBlitWGSL, "vs_main");
            var blitFs = context.ShaderCache.GetOrCreateShader(driver, ComputeBlurShaders.FullscreenBlitWGSL, "fs_main");

            var swapchainFormat = driver.SwapchainFormat;
            var blitKey = new PipelineCacheKey
            {
                VertexShaderSource = ComputeBlurShaders.FullscreenBlitWGSL,
                VertexShaderEntryPoint = "vs_main",
                FragmentShaderSource = ComputeBlurShaders.FullscreenBlitWGSL,
                FragmentShaderEntryPoint = "fs_main",
                Topology = DriverPrimitiveTopology.TriangleList,
                SampleCount = 1,
                VertexBuffers = [],
                ColorTargets = [new ColorTargetDescriptor { Format = swapchainFormat }],
                DepthStencil = null,
            };
            _blitPipeline = context.PipelineCache.GetOrCreate(driver, blitKey, () => driver.CreateRenderPipeline(new RenderPipelineDescriptor
            {
                VertexShader = blitVs,
                FragmentShader = blitFs,
                Topology = DriverPrimitiveTopology.TriangleList,
                ColorTargets = blitKey.ColorTargets,
                VertexBuffers = [],
            }));
        }
        if (_blitSampler == null)
        {
            _blitSampler = driver.CreateSampler(new SamplerDescriptor
            {
                MinFilter = FilterMode.Linear,
                MagFilter = FilterMode.Linear,
            });
        }

        // ── 构建渲染通道 ───────────────────────────────────────────────────
        RenderResourceHandle offscreenColor = default;
        RenderResourceHandle offscreenDepth = default;
        RenderResourceHandle blurredColor = default;
        IBindingSet? blurBindings = null;
        IBindingSet? blitBindings = null;

        // Pass 1: 3D 前向渲染 → 离屏纹理
        graph.AddPass("SceneForward", setup: pass =>
        {
            offscreenColor = pass.CreateTexture(new TextureDescriptor
            {
                Width = ws.Width,
                Height = ws.Height,
                Format = DriverPixelFormat.RGBA8Unorm,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding | TextureUsage.CopySrc,
            });
            pass.WriteTexture(offscreenColor);
            pass.ColorAttachment(offscreenColor, DriverLoadAction.Clear, DriverStoreAction.Store, clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));

            offscreenDepth = pass.CreateTexture(new TextureDescriptor
            {
                Width = ws.Width,
                Height = ws.Height,
                Format = DriverPixelFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(offscreenDepth);
            pass.DepthStencilAttachment(offscreenDepth, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);

            var cameraBufferHandle = pass.ImportBuffer("CameraBuffer", new BufferDescriptor
            {
                Size = scene.CameraBuffer.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            var objectBufferHandle = pass.ImportBuffer("ObjectDataBuffer", new BufferDescriptor
            {
                Size = scene.ObjectDataBuffer.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            var lightBufferHandle = pass.ImportBuffer("LightBuffer", new BufferDescriptor
            {
                Size = scene.LightBuffer.Size,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            });
            pass.ReadBuffer(cameraBufferHandle);
            pass.ReadBuffer(objectBufferHandle);
            pass.ReadBuffer(lightBufferHandle);
        }, execute: ctx =>
        {
            var encoder = ctx.Encoder;
            encoder.SetViewport(0, 0, ws.Width, ws.Height);

            for (int i = 0; i < scene.DrawCount; i++)
            {
                var draw = scene.DrawData[i];
                if (draw.MeshHandle < 0 || draw.MeshHandle >= context.Meshes.Count) continue;
                if (draw.MaterialId < 0 || draw.MaterialId >= context.Materials.Count) continue;

                var mesh = context.Meshes[draw.MeshHandle];
                var material = context.Materials[draw.MaterialId];

                encoder.SetPipeline(material.Pipeline);
                encoder.SetVertexBuffer(0, mesh.VertexBuffer);
                encoder.SetIndexBuffer(mesh.IndexBuffer);
                encoder.SetBindingSet(0, material.BindingSets[0]);
                encoder.SetBindingSet(1, material.BindingSets[1], (uint)(i * 256));
                encoder.SetBindingSet(2, material.BindingSets[2]);
                if (material.BindingSets.Length > 3)
                    encoder.SetBindingSet(3, material.BindingSets[3]);
                if (draw.IsSkinned && draw.JointBindingSet != null)
                    encoder.SetBindingSet(4, draw.JointBindingSet);
                encoder.DrawIndexed((int)mesh.IndexCount);
            }
        });

        // Pass 2 (可选): Separable Gaussian Blur (H + V)
        if (BlurEnabled)
        {
            RenderResourceHandle tempColor = default;
            IBindingSet? blurHBindings = null;
            IBindingSet? blurVBindings = null;

            // Pass 2a: Horizontal blur: offscreenColor → tempColor
            graph.AddComputePass("BlurH", setup: pass =>
            {
                pass.ReadTexture(offscreenColor);
                tempColor = pass.CreateTexture(new TextureDescriptor
                {
                    Width = ws.Width,
                    Height = ws.Height,
                    Format = DriverPixelFormat.RGBA8Unorm,
                    Usage = TextureUsage.Storage | TextureUsage.ShaderBinding,
                });
                pass.WriteTexture(tempColor);
            }, execute: ctx =>
            {
                Console.WriteLine("[Blur] H-pass executing");
                var encoder = ctx.Encoder;
                encoder.SetComputePipeline(_blurHPipeline!);

                var srcView = ctx.GetTextureView(offscreenColor);
                var dstView = ctx.GetTextureView(tempColor);

                blurHBindings = driver.CreateBindingSetForComputePipeline(
                    _blurHPipeline!, 0,
                    [new TextureBinding { TextureView = srcView, Binding = 0 }],
                    [new StorageTextureBinding { TextureView = dstView, Binding = 1, Format = DriverPixelFormat.RGBA8Unorm }]);

                encoder.SetComputeBindingSet(0, blurHBindings);
                uint groupsX = (uint)((ws.Width + 15) / 16);
                uint groupsY = (uint)((ws.Height + 15) / 16);
                encoder.Dispatch(groupsX, groupsY, 1);
            });

            // Pass 2b: Vertical blur: tempColor → blurredColor
            graph.AddComputePass("BlurV", setup: pass =>
            {
                pass.ReadTexture(tempColor);
                blurredColor = pass.CreateTexture(new TextureDescriptor
                {
                    Width = ws.Width,
                    Height = ws.Height,
                    Format = DriverPixelFormat.RGBA8Unorm,
                    Usage = TextureUsage.Storage | TextureUsage.ShaderBinding | TextureUsage.CopySrc,
                });
                pass.WriteTexture(blurredColor);
            }, execute: ctx =>
            {
                Console.WriteLine("[Blur] V-pass executing");
                var encoder = ctx.Encoder;
                encoder.SetComputePipeline(_blurVPipeline!);

                var srcView = ctx.GetTextureView(tempColor);
                var dstView = ctx.GetTextureView(blurredColor);

                blurVBindings = driver.CreateBindingSetForComputePipeline(
                    _blurVPipeline!, 0,
                    [new TextureBinding { TextureView = srcView, Binding = 0 }],
                    [new StorageTextureBinding { TextureView = dstView, Binding = 1, Format = DriverPixelFormat.RGBA8Unorm }]);

                encoder.SetComputeBindingSet(0, blurVBindings);
                uint groupsX = (uint)((ws.Width + 15) / 16);
                uint groupsY = (uint)((ws.Height + 15) / 16);
                encoder.Dispatch(groupsX, groupsY, 1);
            });

            blurBindings = blurHBindings; // for cleanup below
        }

        // Pass 3: Blit → Backbuffer
        // 注意：setup/execute 在 Compile() 时按序执行，此时 offscreenColor/blurredColor 已被前面的 setup 赋值
        graph.AddPass("BlitToBackbuffer", setup: pass =>
        {
            var source = BlurEnabled ? blurredColor : offscreenColor;
            pass.ReadTexture(source);
            var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
            {
                Width = ws.Width,
                Height = ws.Height,
                Format = driver.SwapchainFormat,
                Usage = TextureUsage.RenderAttachment,
            });
            pass.WriteTexture(backbuffer);
            pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store, clearColor: new Vector4(0, 0, 0, 1));
        }, execute: ctx =>
        {
            Console.WriteLine($"[Blur] Blit executing — BlurEnabled={BlurEnabled}");
            var encoder = ctx.Encoder;
            encoder.SetPipeline(_blitPipeline!);

            var source = BlurEnabled ? blurredColor : offscreenColor;
            var srcView = ctx.GetTextureView(source);
            blitBindings = driver.CreateBindingSetForPipeline(
                _blitPipeline!, 0,
                [new TextureBinding { TextureView = srcView, Binding = 0 }],
                [new SamplerBinding { Sampler = _blitSampler!, Binding = 1 }]);

            encoder.SetBindingSet(0, blitBindings);
            encoder.Draw(3);
        });

        // Pass 4 (可选): Screenshot — Copy source texture to readback buffer
        if (ScreenshotRequested)
        {
            // Ensure readback buffer exists and matches current size
            var alignedBytesPerRow = (uint)(((ws.Width * 4) + 255) & ~255);
            var requiredSize = (nuint)(alignedBytesPerRow * ws.Height);
            if (_screenshotBuffer == null || _bufferWidth != ws.Width || _bufferHeight != ws.Height)
            {
                _screenshotBuffer?.Dispose();
                _screenshotBuffer = driver.CreateBuffer(new BufferDescriptor
                {
                    Size = requiredSize,
                    Usage = BufferUsage.CopyDst | BufferUsage.MapRead,
                });
                _bufferWidth = ws.Width;
                _bufferHeight = ws.Height;
            }
            var copySource = BlurEnabled ? blurredColor : offscreenColor;

            graph.AddPass("ScreenshotCopy", setup: pass =>
            {
                // Resolve source inside setup — offscreenColor/blurredColor are assigned
                // by earlier setup callbacks that run before this one during Compile.
                var src = BlurEnabled ? blurredColor : offscreenColor;
                pass.ReadTexture(src);
            }, execute: ctx =>
            {
                var src = BlurEnabled ? blurredColor : offscreenColor;
                var texture = ctx.GetTexture(src);
                ctx.Encoder.CopyTextureToBuffer(texture, new TextureCopyRegion
                {
                    Width = texture.Width,
                    Height = texture.Height,
                }, _screenshotBuffer!, 0);
            });

            ScreenshotRequested = false;
            HasPendingScreenshot = true;
            ScreenshotBuffer = _screenshotBuffer;
            ScreenshotAlignedBytesPerRow = alignedBytesPerRow;
            PendingScreenshotWidth = ws.Width;
            PendingScreenshotHeight = ws.Height;
        }

        // 清理每帧的临时 BindingSet
        blurBindings?.Dispose();
        blitBindings?.Dispose();
    }
}
