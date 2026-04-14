using System.Numerics;
using Kilo.ECS;
using Kilo.Input;
using Kilo.Rendering.Driver;
using Kilo.Rendering.Driver.WebGPUImpl;
using Kilo.Rendering.Resources;
using Kilo.Rendering.Shaders;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Parent = TinyEcs.Parent;

namespace Kilo.Rendering;

public sealed class RenderingPlugin : IKiloPlugin
{
    private readonly RenderSettings _settings;

    public RenderingPlugin(RenderSettings? settings = null)
    {
        _settings = settings ?? new RenderSettings();
    }

    public void Build(KiloApp app)
    {
        app.AddResource(_settings);
        app.AddResource(new RenderContext
        {
            ShaderCache = new ShaderCache(),
            PipelineCache = new PipelineCache(),
        });
        app.AddResource(new WindowSize { Width = _settings.Width, Height = _settings.Height });
        app.AddResource(new GpuSceneData());

        app.AddSystem(KiloStage.PostUpdate, ComputeLocalToWorld);
        app.AddSystem(KiloStage.First, new CameraSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new FrustumCullingSystem().Update);
        app.AddSystem(KiloStage.PostUpdate, new PrepareGpuSceneSystem().Update);

        app.AddSystem(KiloStage.Last, new BeginFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new ShadowMapSystem().Update);
        app.AddSystem(KiloStage.Last, new RenderSystem().Update);
        app.AddSystem(KiloStage.Last, new SpriteRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new TextRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new EndFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new WindowResizeSystem().Update);
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

            // Wire input events if InputState is registered
            WireInputEvents(window, app.World);

            Console.WriteLine("[Kilo] WebGPU initialized successfully.");
        };

        window.Render += _ =>
        {
            app.Update();
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

    private static void WireInputEvents(IWindow window, KiloWorld world)
    {
        if (!world.HasResource<InputState>()) return;

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
                if (idx >= 0 && idx < 5)
                    inputState.MouseButtonsDown[idx] = true;
            };
            mouse.MouseUp += (_, button) =>
            {
                int idx = (int)button;
                if (idx >= 0 && idx < 5)
                    inputState.MouseButtonsDown[idx] = false;
            };
            mouse.Scroll += (_, offset) =>
            {
                inputState.ScrollDelta += (float)offset.Y;
            };
        }
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

    private static void InitializeResources(RenderContext context, GpuSceneData scene, IRenderDriver driver)
    {
        // --- GPU Scene Buffers ---
        const int ObjectBufferSize = 64 * 1024; // 64KB
        const int LightBufferSize = 4 * 1024;   // 4KB

        scene.CameraBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)CameraData.Size,
            Usage = RenderGraph.BufferUsage.Uniform | RenderGraph.BufferUsage.CopyDst,
        });

        scene.ObjectDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)ObjectBufferSize,
            Usage = RenderGraph.BufferUsage.Uniform | RenderGraph.BufferUsage.CopyDst,
        });

        scene.LightBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)LightBufferSize,
            Usage = RenderGraph.BufferUsage.Uniform | RenderGraph.BufferUsage.CopyDst,
        });

        // --- Sprite Resources ---
        var spriteVertexShader = context.ShaderCache.GetOrCreateShader(driver, SpriteShaders.WGSL, "vs_main");
        var spriteFragmentShader = context.ShaderCache.GetOrCreateShader(driver, SpriteShaders.WGSL, "fs_main");

        const int UniformStructSize = 144; // model(64) + projection(64) + color(16)
        const int UniformAlign = 256;      // WebGPU minUniformBufferOffsetAlignment
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
                    Attributes =
                    [
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 0,
                            Format = VertexFormat.Float32x2,
                            Offset = 0,
                        }
                    ]
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
            VertexShader = spriteVertexShader,
            FragmentShader = spriteFragmentShader,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = spritePipelineKey.ColorTargets,
            VertexBuffers = spritePipelineKey.VertexBuffers,
        }, (nuint)UniformStructSize, groupIndex: 0, bindGroupCount: 1));

        float[] quadVertices = [-0.5f, 0.5f, 0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f];
        uint[] quadIndices = [0u, 1, 2, 2, 1, 3];

        context.QuadVertexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)(quadVertices.Length * sizeof(float)),
            Usage = RenderGraph.BufferUsage.Vertex | RenderGraph.BufferUsage.CopyDst,
        });
        context.QuadVertexBuffer.UploadData<float>(quadVertices);

        context.QuadIndexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)(quadIndices.Length * sizeof(uint)),
            Usage = RenderGraph.BufferUsage.Index | RenderGraph.BufferUsage.CopyDst,
        });
        context.QuadIndexBuffer.UploadData<uint>(quadIndices);

        context.UniformBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)UniformBufferSize,
            Usage = RenderGraph.BufferUsage.Uniform | RenderGraph.BufferUsage.CopyDst,
        });

        context.BindingSet = driver.CreateDynamicUniformBindingSet(
            context.SpritePipeline, 0, context.UniformBuffer, UniformStructSize);

        // --- Default Cube Mesh (position xyz + normal xyz + uv xy = 8 floats) ---
        float[] cubeVertices =
        [
            // Front face
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,
            // Back face
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
            // Top face
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
            // Bottom face
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
            // Right face
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            // Left face
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
        ];

        uint[] cubeIndices =
        [
            // Front
            0, 1, 2,  0, 2, 3,
            // Back
            4, 5, 6,  4, 6, 7,
            // Top
            8, 9, 10,  8, 10, 11,
            // Bottom
            12, 13, 14,  12, 14, 15,
            // Right
            16, 17, 18,  16, 18, 19,
            // Left
            20, 21, 22,  20, 22, 23,
        ];

        var cubeVertexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)(cubeVertices.Length * sizeof(float)),
            Usage = RenderGraph.BufferUsage.Vertex | RenderGraph.BufferUsage.CopyDst,
        });
        cubeVertexBuffer.UploadData<float>(cubeVertices);

        var cubeIndexBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = (nuint)(cubeIndices.Length * sizeof(uint)),
            Usage = RenderGraph.BufferUsage.Index | RenderGraph.BufferUsage.CopyDst,
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
                    ArrayStride = 8 * sizeof(float), // pos(3) + normal(3) + uv(2)
                    Attributes =
                    [
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 0,
                            Format = VertexFormat.Float32x3,
                            Offset = 0,
                        },
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 1,
                            Format = VertexFormat.Float32x3,
                            Offset = (nuint)(3 * sizeof(float)),
                        },
                        new VertexAttributeDescriptor
                        {
                            ShaderLocation = 2,
                            Format = VertexFormat.Float32x2,
                            Offset = (nuint)(6 * sizeof(float)),
                        }
                    ]
                }
            ]
        };

        context.Meshes.Add(cubeMesh);

        // --- Default BasicLit Material ---
        var basicLitVertexShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "vs_main");
        var basicLitFragmentShader = context.ShaderCache.GetOrCreateShader(driver, BasicLitShaders.WGSL, "fs_main");

        var basicLitPipelineKey = new PipelineCacheKey
        {
            VertexShaderSource = BasicLitShaders.WGSL,
            VertexShaderEntryPoint = "vs_main",
            FragmentShaderSource = BasicLitShaders.WGSL,
            FragmentShaderEntryPoint = "fs_main",
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers = cubeMesh.Layouts,
            ColorTargets =
            [
                new ColorTargetDescriptor
                {
                    Format = swapchainFormat,
                }
            ],
            DepthStencil = new DepthStencilStateDescriptor
            {
                Format = DriverPixelFormat.Depth24Plus,
                DepthCompare = DriverCompareFunction.Less,
                DepthWriteEnabled = true,
            }
        };

        var basicLitPipeline = context.PipelineCache.GetOrCreate(driver, basicLitPipelineKey, () => driver.CreateRenderPipelineWithDynamicUniforms(new RenderPipelineDescriptor
        {
            VertexShader = basicLitVertexShader,
            FragmentShader = basicLitFragmentShader,
            Topology = DriverPrimitiveTopology.TriangleList,
            ColorTargets = basicLitPipelineKey.ColorTargets,
            VertexBuffers = cubeMesh.Layouts,
            DepthStencil = basicLitPipelineKey.DepthStencil,
        }, (nuint)ObjectData.Size, groupIndex: 1, bindGroupCount: 4));

        var cameraBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 0, [new UniformBufferBinding { Buffer = scene.CameraBuffer, Binding = 0 }]);

        var objectBindingSet = driver.CreateDynamicUniformBindingSet(
            basicLitPipeline, 1, scene.ObjectDataBuffer, (nuint)ObjectData.Size);

        var lightBindingSet = driver.CreateBindingSetForPipeline(basicLitPipeline, 2, [new UniformBufferBinding { Buffer = scene.LightBuffer, Binding = 0 }]);

        // --- Default white 1x1 texture ---
        var defaultTexture = driver.CreateTexture(new RenderGraph.TextureDescriptor
        {
            Width = 1,
            Height = 1,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = RenderGraph.TextureUsage.CopyDst | RenderGraph.TextureUsage.ShaderBinding,
            MipLevelCount = 1,
            SampleCount = 1,
        });
        defaultTexture.UploadData<byte>([255, 255, 255, 255]);

        var defaultTextureView = driver.CreateTextureView(defaultTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        var defaultSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
            AddressModeU = WrapMode.Repeat,
            AddressModeV = WrapMode.Repeat,
            AddressModeW = WrapMode.Repeat,
        });

        // --- Shadow resources (merged into group 3: bindings 2,3,4) ---
        var shadowSampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            AddressModeU = WrapMode.ClampToEdge,
            AddressModeV = WrapMode.ClampToEdge,
            Compare = true,
            CompareFunction = DriverCompareFunction.Less,
        });

        var shadowDataBuffer = driver.CreateBuffer(new RenderGraph.BufferDescriptor
        {
            Size = 256,
            Usage = RenderGraph.BufferUsage.Uniform | RenderGraph.BufferUsage.CopyDst,
        });
        context.ShadowDataBuffer = shadowDataBuffer;
        context.ShadowSampler = shadowSampler;

        // Placeholder depth texture for shadow_map binding (must be Depth24Plus format)
        var placeholderDepthTexture = driver.CreateTexture(new RenderGraph.TextureDescriptor
        {
            Width = 1, Height = 1,
            Format = DriverPixelFormat.Depth24Plus,
            Usage = RenderGraph.TextureUsage.ShaderBinding,
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

                // Check for parent entity (TinyEcs built-in Parent component)
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
