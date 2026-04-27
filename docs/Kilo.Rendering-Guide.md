# Kilo.Rendering 使用指南

> **版本**：Kilo 渲染插件（WebGPU 后端，FrameGraph 架构）  
> **目标读者**：需要使用 Kilo.Rendering 进行 2D/3D 渲染的开发者

---

## 1. 概述

`Kilo.Rendering` 是 Kilo 引擎的渲染插件，基于 **Silk.NET.WebGPU** 构建，采用 **FrameGraph 风格**的渲染管线设计，并与 **ECS（Entity-Component-System）**深度集成。

### 它能做什么？

| 功能 | 说明 |
|------|------|
| **2D 精灵渲染** | 批量渲染带颜色/纹理的 2D 四边形，支持 Z-index 排序 |
| **3D 前向渲染** | 支持深度测试的 Forward 渲染，内置 Blinn-Phong 光照模型 |
| **计算着色器** | 在 RenderGraph 中插入 Compute Pass，执行通用 GPU 计算 |
| **后处理 Blit** | 将纹理结果通过全屏着色器绘制到 Backbuffer |
| **资源管理** | 自动分配和回收瞬态纹理/缓冲区，减少 GPU 内存碎片 |
| **管线缓存** | 自动复用 `PipelineCache` 和 `ShaderCache`，避免重复编译 |

### 架构特点

- **声明式 RenderGraph**：开发者声明 Pass 和资源的读写关系，Graph 自动处理依赖排序和资源生命周期。
- **ECS 驱动**：渲染所需的数据（相机、灯光、变换、网格、材质）全部以 ECS 组件/资源的形式存在。
- **最小侵入**：通过 `RenderingPlugin` 一行代码即可启动窗口和默认渲染管线。

---

## 2. 快速开始

以下是一个最小可运行的 2D 精灵渲染示例：打开窗口并显示一个红色方块。

```csharp
using System.Numerics;
using Kilo.ECS;
using Kilo.Rendering;

var app = new KiloApp();
var settings = new RenderSettings
{
    Width = 1280,
    Height = 720,
    Title = "Kilo Quick Start"
};

var plugin = new RenderingPlugin(settings);
app.AddPlugin(plugin);

// 启动时创建一个红色精灵
app.AddSystem(KiloStage.Startup, world =>
{
    world.Entity("RedSprite")
        .Set(new LocalTransform { Position = Vector3.Zero, Scale = Vector3.One })
        .Set(new LocalToWorld())
        .Set(new Sprite
        {
            Tint = new Vector4(1, 0, 0, 1),
            Size = new Vector2(1, 1),
            TextureHandle = -1, // -1 表示不使用纹理，渲染纯色
            ZIndex = 0
        });
});

plugin.Run(app);
```

**运行命令：**

```bash
dotnet run --project YourProject
```

`RenderingPlugin` 会自动完成以下工作：
- 创建 Silk.NET 窗口
- 初始化 WebGPU 设备和交换链
- 创建默认的精灵管线（着色器、顶点缓冲区、Uniform 缓冲区）
- 注册相机系统、场景准备系统、渲染系统和窗口大小调整系统

---

## 3. 核心概念

### 3.1 RenderGraph

`RenderGraph` 是 `Kilo.Rendering` 的核心调度器。开发者通过 `RenderGraphBuilder` 添加渲染通道（RenderPass）和计算通道（ComputePass），声明每个通道**创建**、**读取**、**写入**哪些资源。Graph 在 `Execute` 时自动完成：

1. **拓扑排序**：根据资源依赖关系确定 Pass 执行顺序
2. **资源分配**：从内部资源池获取或创建瞬态纹理/缓冲区
3. **命令编码**：按顺序调用每个 Pass 的 `execute` 回调

```csharp
var builder = new RenderGraphBuilder();
var graph = builder.GetGraph();

builder.AddRenderPass("MyPass", setup: pass =>
{
    // 导入 Backbuffer（外部资源）
    var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor { ... });
    pass.WriteTexture(backbuffer);
    pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store,
        clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));
}, execute: ctx =>
{
    var encoder = ctx.Encoder;
    // 在这里提交绘制命令
});

graph.Execute(driver);
```

### 3.2 RenderPass

渲染通道用于执行图形绘制命令。一个 `RenderPass` 可以配置：

- **Color Attachments**：颜色输出目标，支持 Load/Store 动作和清除颜色
- **Depth Stencil Attachment**：深度/模板缓冲区，支持深度清除值和比较函数
- **Viewport / Scissor**：可选的视口和裁剪矩形

```csharp
builder.AddRenderPass("ForwardOpaque", setup: pass =>
{
    var depth = pass.CreateTexture(new TextureDescriptor
    {
        Width = ws.Width,
        Height = ws.Height,
        Format = DriverPixelFormat.Depth24Plus,
        Usage = TextureUsage.RenderAttachment,
    });
    pass.WriteTexture(depth);
    pass.DepthStencilAttachment(depth, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);

    var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor { ... });
    pass.WriteTexture(backbuffer);
    pass.ColorAttachment(backbuffer, DriverLoadAction.Clear, DriverStoreAction.Store,
        clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));
}, execute: ctx =>
{
    ctx.Encoder.SetViewport(0, 0, ws.Width, ws.Height);
    ctx.Encoder.DrawIndexed(36);
});
```

### 3.3 ComputePass

计算通道用于执行 WGSL 计算着色器。它不能配置 Color/Depth Attachment，但可以读写纹理和缓冲区。

```csharp
builder.AddComputePass("BlurCompute", setup: pass =>
{
    pass.ReadTexture(inputTexture);
    pass.WriteTexture(outputTexture);
}, execute: ctx =>
{
    ctx.Encoder.SetComputePipeline(computePipeline);
    ctx.Encoder.SetComputeBindingSet(0, bindings);
    ctx.Encoder.Dispatch(groupsX, groupsY, 1);
});
```

### 3.4 Imported vs Transient 资源

| 类型 | 创建方式 | 生命周期 | 用途 |
|------|---------|---------|------|
| **Imported** | `pass.ImportTexture(name, desc)` 或 `pass.ImportBuffer(name, desc)` | 由调用方管理 | Backbuffer、GPU 场景 Uniform 缓冲区、持久化纹理 |
| **Transient** | `pass.CreateTexture(desc)` 或 `pass.CreateBuffer(desc)` | RenderGraph 自动分配和回收 | 临时深度纹理、离屏渲染目标、后处理中间纹理 |

**Imported 资源的特点：**
- 按名称缓存，同一次 `Execute` 内多次导入返回相同句柄
- Backbuffer 是特殊的 Imported 纹理，名称固定为 `"Backbuffer"`，Graph 会自动将其绑定到当前帧的交换链纹理

**Transient 资源的特点：**
- 每次 `Execute` 后返回内部资源池，供下次复用
- 描述符相同时优先复用已有 GPU 资源，减少分配开销

### 3.5 GpuSceneData

`GpuSceneData` 是存储每帧 GPU 场景数据的资源，包含三个 Uniform 缓冲区：

- **CameraBuffer**：相机视图矩阵、投影矩阵、相机位置、灯光数量
- **ObjectDataBuffer**：每个可见物体的模型矩阵和材质 ID
- **LightBuffer**：所有平行光和点光源的数据

这些数据由 `PrepareGpuSceneSystem` 在 `KiloStage.Last` 阶段自动填充：

```csharp
// PrepareGpuSceneSystem 的工作流程（内部实现）：
// 1. 查询所有 Camera + LocalTransform，填充 CameraBuffer
// 2. 查询所有 MeshRenderer + LocalToWorld，填充 ObjectDataBuffer
// 3. 查询所有 DirectionalLight / PointLight，填充 LightBuffer
```

普通开发者**不需要手动调用** `PrepareGpuSceneSystem`，只需要确保场景中存在对应的 ECS 组件即可。

### 3.6 Mesh & Material

#### Mesh（网格）

```csharp
var mesh = new Mesh
{
    VertexBuffer = vertexBuffer,   // IBuffer，Usage 包含 Vertex
    IndexBuffer = indexBuffer,     // IBuffer，Usage 包含 Index
    IndexCount = 36,
    Layouts =
    [
        new VertexBufferLayout
        {
            ArrayStride = 6 * sizeof(float),
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
                }
            ]
        }
    ]
};

context.Meshes.Add(mesh); // 返回索引句柄（从 0 开始）
```

#### Material（材质）

```csharp
var material = new Material
{
    Pipeline = pipeline,           // IRenderPipeline
    BindingSets = [set0, set1, set2] // IBindingSet[]
};

context.Materials.Add(material); // 返回索引句柄（从 0 开始）
```

#### MaterialInstance（材质实例）

用于在不重新创建管线的情况下复用父材质，并支持动态偏移或纹理覆盖：

```csharp
var instance = new MaterialInstance(material)
{
    DynamicOffsetIndex = 2, // 使用不同的动态 uniform 偏移
};
```

### 3.7 PipelineCache & ShaderCache

`Kilo.Rendering` 内部使用两个缓存来避免重复编译：

- **ShaderCache**：按 `(WGSL 源码, EntryPoint)` 缓存编译后的 `IShaderModule`
- **PipelineCache**：按 `PipelineCacheKey`（包含着色器、顶点布局、颜色目标、深度状态等）缓存编译后的 `IRenderPipeline`

**使用方式（自动）：**

```csharp
var vs = context.ShaderCache.GetOrCreateShader(driver, wgslSource, "vs_main");
var fs = context.ShaderCache.GetOrCreateShader(driver, wgslSource, "fs_main");

var pipeline = context.PipelineCache.GetOrCreate(driver, cacheKey, () =>
{
    return driver.CreateRenderPipeline(new RenderPipelineDescriptor { ... });
});
```

普通开发者通常不需要直接与缓存交互，因为 `RenderingPlugin` 已经自动将 `ShaderCache` 和 `PipelineCache` 注册为资源。

---

## 4. 3D 前向渲染

### 4.1 步骤概览

要渲染一个 3D 物体，你需要：

1. **创建 Transform 和 Renderer 组件**
2. **确保 RenderContext 中有对应的 Mesh 和 Material**
3. **添加光源**
4. **添加相机实体**

### 4.2 完整示例

```csharp
app.AddSystem(KiloStage.Startup, world =>
{
    // 1. 创建立方体实体
    world.Entity("Cube")
        .Set(new LocalTransform
        {
            Position = new Vector3(0, 0, 0),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        })
        .Set(new LocalToWorld())
        // MeshHandle = 0 和 MaterialHandle = 0 引用 RenderingPlugin
        // 自动创建的默认立方体和 BasicLit 材质
        .Set(new MeshRenderer { MeshHandle = 0, MaterialHandle = 0 });

    // 2. 添加平行光
    world.Entity("Sun")
        .Set(new DirectionalLight
        {
            Direction = new Vector3(0.5f, -1.0f, 0.5f),
            Color = new Vector3(1.0f, 0.95f, 0.9f),
            Intensity = 1.0f
        });

    // 3. 添加相机
    world.Entity("Camera")
        .Set(new LocalTransform
        {
            Position = new Vector3(0, 0, 10),
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
```

### 4.3 默认着色器与 Uniform 布局

`RenderingPlugin` 内置了 `BasicLit` 着色器，其 WGSL 布局如下：

```wgsl
@group(0) @binding(0) var<uniform> camera: CameraData;
@group(1) @binding(0) var<uniform> object: ObjectData;
@group(2) @binding(0) var<uniform> lights: Lights;
```

| Group | 内容 | 绑定方式 |
|-------|------|---------|
| 0 | CameraBuffer | 固定 Uniform 缓冲区 |
| 1 | ObjectDataBuffer | **动态 Uniform 偏移**（每个物体 256 字节对齐） |
| 2 | LightBuffer | 固定 Uniform 缓冲区 |

**为什么动态偏移？**

WebGPU 要求 `minUniformBufferOffsetAlignment` 至少为 256 字节。`Kilo.Rendering` 将每个 `ObjectData` 填充到 256 字节，从而可以在同一个 Uniform 缓冲区中存储多个物体的数据，通过偏移量切换，减少缓冲区分配。

---

## 5. 2D 精灵渲染

### 5.1 Sprite 组件

```csharp
public struct Sprite
{
    public Vector4 Tint;        // RGBA 颜色/色调
    public Vector2 Size;        // 世界单位大小
    public int TextureHandle;   // 纹理索引，-1 表示纯色
    public float ZIndex;        // 绘制顺序，值越大越靠前
}
```

### 5.2 精灵渲染通道

`SpriteRenderSystem` 内部构建了一个简单的 RenderGraph Pass：

```csharp
builder.AddRenderPass("SpritePass", setup: pass =>
{
    var backbuffer = pass.ImportTexture("Backbuffer", ...);
    pass.WriteTexture(backbuffer);
    pass.ColorAttachment(backbuffer, DriverLoadAction.Load, DriverStoreAction.Store);
    // ... 导入 Uniform 缓冲区
}, execute: ctx =>
{
    // 批量上传所有精灵实例数据到 Uniform 缓冲区
    // 循环绘制每个精灵，使用动态偏移切换 uniform
});
```

### 5.3 正交投影

精灵使用正交投影矩阵，默认视口高度为 10 个世界单位，宽度根据窗口宽高比自动调整：

```csharp
float aspect = (float)windowSize.Width / windowSize.Height;
var projection = Matrix4x4.CreateOrthographicOffCenter(
    -5f * aspect, 5f * aspect, -5f, 5f, -1f, 1f);
```

这意味着 `Size = (1, 1)` 的精灵在屏幕上显示为一个正方形（不考虑窗口拉伸）。

---

## 6. 计算着色器与后处理

### 6.1 编写 WGSL 计算着色器

```wgsl
@group(0) @binding(0) var src: texture_2d<f32>;
@group(0) @binding(1) var dst: texture_storage_2d<rgba8unorm, write>;

@compute @workgroup_size(16, 16)
fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let dims = textureDimensions(src);
    if (global_id.x >= u32(dims.x) || global_id.y >= u32(dims.y)) { return; }

    let coord = vec2<i32>(i32(global_id.x), i32(global_id.y));
    let color = textureLoad(src, coord, 0);
    textureStore(dst, coord, color);
}
```

### 6.2 在 RenderGraph 中添加 Compute Pass

```csharp
builder.AddComputePass("BlurCompute", setup: pass =>
{
    pass.ReadTexture(offscreenColor);
    pass.WriteTexture(blurredColor);
}, execute: ctx =>
{
    ctx.Encoder.SetComputePipeline(computePipeline);
    ctx.Encoder.SetComputeBindingSet(0, bindings);

    uint groupsX = (uint)((width + 15) / 16);
    uint groupsY = (uint)((height + 15) / 16);
    ctx.Encoder.Dispatch(groupsX, groupsY, 1);
});
```

### 6.3 Blit 到 Backbuffer

后处理通常需要将计算结果通过全屏三角形绘制到屏幕。`Kilo.Rendering` 的示例中使用了无顶点数据的全屏着色器：

```wgsl
@vertex
fn vs_main(@builtin(vertex_index) vertex_index: u32) -> VertexOutput {
    let x = f32(vertex_index % 2u) * 2.0 - 1.0;
    let y = f32(vertex_index / 2u) * 2.0 - 1.0;
    // vertex_index: 0 -> (-1,-1), 1 -> (1,-1), 2 -> (-1,1)
    // Draw(3) 即可覆盖全屏
    ...
}
```

完整的三阶段后处理流程请参考 `samples/Kilo.Samples.ComputeBlur`。

---

## 7. 自定义渲染通道（高级）

如果你需要实现阴影贴图、延迟 G-Buffer、体积光等高级效果，可以编写自己的渲染系统，直接操作 `RenderGraphBuilder`。

### 7.1 典型模式

```csharp
public sealed class MyCustomRenderSystem
{
    public void Update(KiloWorld world)
    {
        var context = world.GetResource<RenderContext>();
        var driver = context.Driver;

        var builder = new RenderGraphBuilder();
        var graph = builder.GetGraph();

        // Pass 1: 生成阴影贴图
        builder.AddRenderPass("ShadowMap", setup: pass =>
        {
            var shadowTex = pass.CreateTexture(new TextureDescriptor
            {
                Width = 2048,
                Height = 2048,
                Format = DriverPixelFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment | TextureUsage.ShaderBinding,
            });
            pass.WriteTexture(shadowTex);
            pass.DepthStencilAttachment(shadowTex, DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);
        }, execute: ctx =>
        {
            // 绘制阴影投射器
        });

        // Pass 2: 主场景 Forward 渲染（读取阴影贴图）
        builder.AddRenderPass("MainScene", setup: pass =>
        {
            // ...
        }, execute: ctx =>
        {
            // ...
        });

        graph.Execute(driver);
    }
}
```

### 7.2 在自定义插件中注册

```csharp
public sealed class MyPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddResource(new RenderContext { ... });
        // ... 其他系统
        app.AddSystem(KiloStage.Last, new MyCustomRenderSystem().Update);
    }
}
```

---

## 8. 性能建议

1. **批量上传 per-object 数据**  
   `PrepareGpuSceneSystem` 和 `SpriteRenderSystem` 都会先将所有实例数据批量上传到 GPU，再循环绘制。切勿在每次 `Draw` 前单独上传数据。

2. **复用 RenderGraph 结构以命中编译缓存**  
   `RenderGraph.Compile` 会检测结构版本（`_structureVersion`）。如果 Pass 数量和连接关系不变，则跳过拓扑排序和资源重新分配。尽量保持 Graph 结构稳定，只在数据变化时重新构建。

3. **利用 PipelineCache 和 ShaderCache**  
   这些缓存由 `RenderingPlugin` 自动管理。只要 `PipelineCacheKey` 不变，就不会重复创建渲染管线。

4. **复用瞬态资源**  
   相同描述符的 Transient 纹理/缓冲区会从 `RenderGraphResourcePool` 中复用。保持描述符一致有助于减少实际的 GPU 资源分配。

5. **动态 Uniform 偏移代替多缓冲区**  
   对于大量相同材质的物体，使用动态 uniform 偏移（Dynamic Offset）从同一大缓冲区中读取数据，比为每个物体创建独立缓冲区更高效。

---

## 9. API 参考（简明版）

### 核心类型

| 类型 | 说明 |
|------|------|
| `RenderGraph` | 渲染图实例，管理 Pass、资源和执行 |
| `RenderGraphBuilder` | 构建器 API，用于链式添加 Pass |
| `PassBuilder` | 渲染通道构建器，配置 Color/Depth Attachment、创建/导入资源 |
| `ComputePassBuilder` | 计算通道构建器，配置读写纹理和缓冲区 |
| `RenderPassExecutionContext` | Pass 执行上下文，提供 `Encoder` 和资源视图查询 |

### 资源类型

| 类型 | 说明 |
|------|------|
| `GpuSceneData` | 每帧 GPU 场景数据（Camera/Object/Light Buffer） |
| `Mesh` | 网格资源，包含顶点/索引缓冲区和布局描述 |
| `Material` | 材质资源，包含管线和绑定组 |
| `MaterialInstance` | 材质实例，支持动态偏移和纹理覆盖 |
| `ShaderCache` | WGSL 着色器模块缓存 |
| `PipelineCache` | 渲染管线缓存 |

### 驱动抽象

| 类型 | 说明 |
|------|------|
| `IRenderDriver` | 渲染驱动接口（WebGPU 实现） |
| `IRenderCommandEncoder` | 命令编码器，提交 Draw/Dispatch/SetPipeline 等命令 |
| `IBuffer` | GPU 缓冲区抽象 |
| `ITexture` / `ITextureView` | GPU 纹理和视图抽象 |
| `IRenderPipeline` / `IComputePipeline` | 渲染/计算管线抽象 |
| `IBindingSet` / `ISampler` | 绑定组和采样器抽象 |

### ECS 组件

| 组件 | 说明 |
|------|------|
| `Camera` | 相机参数（FOV、近远裁剪面、激活状态） |
| `MeshRenderer` | 网格和材质句柄 |
| `Sprite` | 2D 精灵数据（颜色、大小、纹理、ZIndex） |
| `LocalTransform` | 本地变换（位置、旋转、缩放） |
| `LocalToWorld` | 世界变换矩阵（由系统自动计算） |
| `DirectionalLight` | 平行光（方向、颜色、强度） |
| `PointLight` | 点光源（位置、颜色、强度、范围） |

---

*文档结束*
