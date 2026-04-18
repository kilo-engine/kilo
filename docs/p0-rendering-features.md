# P0 渲染功能实现计划

## 项目概述

Kilo 是一个基于 .NET 10 的游戏引擎，使用 WebGPU（通过 Silk.NET）进行渲染，采用 ECS 架构（基于 TinyEcs.Bevy）。
仓库根目录：`C:\code\kilo`

## 当前已实现的功能

- RenderGraph（资源池化、依赖追踪、拓扑排序）
- 3D 前向渲染（Blinn-Phong 光照）
- Compute Shader 后处理
- 2D 精灵渲染（Alpha 混合）
- 材质系统（BaseColor + 纹理 + Pipeline 缓存）
- 输入系统（键盘/鼠标）
- 相机系统（透视/正交）
- 纹理上传（ImageSharp → GPU）

## P0 任务清单

### 任务 1：模型加载（GLTF）

**目标**：支持加载 .gltf/.glb 模型文件，自动创建 Mesh + Material 资源。

**实现步骤**：

1. **添加依赖**：在 `src/Kilo.Rendering/Kilo.Rendering.csproj` 中添加 gltf 加载库：
   ```
   <PackageReference Include="SharpGLTF.Core" Version="1.0.*" />
   <PackageReference Include="SharpGLTF.Runtime" Version="1.0.*" />
   ```
   或考虑使用 `Assimp` 通过 Silk.NET.Assimp（`Silk.NET.Assimp` 2.23.0）。

2. **创建 MeshData 类** `src/Kilo.Rendering/Resources/MeshData.cs`：
   ```csharp
   public sealed class MeshData
   {
       public float[] Positions;    // xyz interleaved or separate
       public float[] Normals;
       public float[] UVs;
       public uint[] Indices;
       // 从 MeshData 创建 GPU Mesh（上传到 driver）
       public Mesh ToGpuMesh(IRenderDriver driver);
   }
   ```

3. **创建 ModelLoader** `src/Kilo.Rendering/Assets/ModelLoader.cs`：
   - 解析 GLTF 文件，提取所有 mesh primitive
   - 为每个 primitive 创建 MeshData（positions + normals + uvs + indices）
   - 返回 `List<(MeshData mesh, int materialIndex)>`

4. **创建 Model 组件** `src/Kilo.Rendering/Components/ModelRenderer.cs`：
   ```csharp
   public struct ModelRenderer
   {
       public string ModelPath;
       public bool IsLoaded;
       // 加载完成后填充：
       public int FirstMeshHandle;
       public int MeshCount;
       public int FirstMaterialHandle;
   }
   ```

5. **创建 ModelLoadSystem** `src/Kilo.Rendering/Systems/ModelLoadSystem.cs`：
   - 在 Update 中查询未加载的 ModelRenderer 实体
   - 调用 ModelLoader 加载模型
   - 创建 GPU Mesh + Material 资源，注册到 RenderContext
   - 更新实体的 MeshRenderer 组件

6. **在 RenderingPlugin.Build() 中注册 ModelLoadSystem**。

**关键文件**：
- `src/Kilo.Rendering/Kilo.Rendering.csproj` — 添加依赖
- `src/Kilo.Rendering/Resources/MeshData.cs` — 新建
- `src/Kilo.Rendering/Assets/ModelLoader.cs` — 新建
- `src/Kilo.Rendering/Components/ModelRenderer.cs` — 新建
- `src/Kilo.Rendering/Systems/ModelLoadSystem.cs` — 新建
- `src/Kilo.Rendering/RenderingPlugin.cs` — 注册系统

**参考文件**（读取这些文件了解现有模式）：
- `src/Kilo.Rendering/Resources/Material.cs` — Material 结构
- `src/Kilo.Rendering/Resources/MaterialManager.cs` — Material 创建流程
- `src/Kilo.Rendering/RenderingPlugin.cs` — InitializeResources 中的 cube 网格创建代码（了解顶点格式和 buffer 创建）
- `src/Kilo.Rendering/Systems/PrepareGpuSceneSystem.cs` — 了解 draw data 如何准备
- `src/Kilo.Rendering/Resources/RenderContext.cs` — Meshes 和 Materials 列表

**验证**：在 Forward3D sample 中替换硬编码立方体为加载的 GLTF 模型。

---

### 任务 2：阴影映射（Shadow Mapping）

**目标**：为平行光生成阴影贴图，在着色器中采样阴影贴图实现阴影。

**实现步骤**：

1. **创建 ShadowMapPass**：在 RenderGraph 中添加一个新 pass，位于 ForwardOpaque 之前：
   - 从光源视角渲染场景到深度纹理（1024x1024 或 2048x2048）
   - 需要 light space ViewProjection 矩阵

2. **创建 ShadowMapSystem** `src/Kilo.Rendering/Systems/ShadowMapSystem.cs`：
   - 计算光源的 ViewProjection 矩阵
   - 将矩阵上传到 GPU（添加到 CameraData 或新建 uniform buffer）

3. **添加 ShadowMap 资源**：
   - 在 RenderContext 或 GpuSceneData 中添加 `ShadowMapTexture`、`ShadowMapView`、`ShadowMapSampler`
   - 在 RenderingPlugin.InitializeResources 中创建深度纹理用于阴影

4. **修改 BasicLitShaders**：
   - 添加 `@group(4)` 用于 shadow map（depth texture + sampler + light VP matrix）
   - 在 fragment shader 中计算 shadow coordinates，采样阴影贴图
   - 根据 shadow 值调整光照

5. **修改 RenderSystem**：在 ForwardOpaque pass 之前添加 ShadowPass：
   ```csharp
   graph.AddPass("ShadowMap", setup: pass => {
       // 创建/引用 shadow depth texture
       // 配置深度附件（Clear depth = 1.0）
   }, execute: ctx => {
       // 用 light space VP 绘制所有物体（无需 fragment shader）
   });
   ```

6. **为 shadow pipeline 创建无 fragment 的 shader**（depth-only pass）。

**关键文件**：
- `src/Kilo.Rendering/Shaders/BasicLitShaders.cs` — 修改现有着色器添加阴影采样
- `src/Kilo.Rendering/Systems/RenderSystem.cs` — 添加 ShadowMap pass
- `src/Kilo.Rendering/Resources/GpuSceneData.cs` — 添加 shadow 相关数据
- `src/Kilo.Rendering/RenderingPlugin.cs` — 初始化 shadow 资源
- `src/Kilo.Rendering/RenderGraph/RenderGraph.cs` — 了解如何添加 pass

**着色器伪代码**：
```wgsl
// 在 fragment shader 中：
let lightSpacePos = lightVP * worldPos;
let shadowCoord = lightSpacePos.xy / lightSpacePos.w * 0.5 + 0.5;
let shadowDepth = textureSample(shadowMap, shadowSampler, shadowCoord).r;
let shadow = select(1.0, 0.0, shadowCoord.z - 0.005 > shadowDepth);
let lighting = ambient + (1.0 - shadow) * (diffuse + specular);
```

**验证**：在 Forward3D sample 中观察物体阴影是否正确投射到地面。

---

### 任务 3：场景层级（Parent-Child Transforms）

**目标**：支持实体间的父子关系，子实体的 LocalToWorld 矩阵 = Parent.WorldMatrix × Child.LocalMatrix。

**实现步骤**：

1. **创建 Parent 和 Children 组件** `src/Kilo.ECS/Components/`：
   ```csharp
   public struct Parent { public EntityReference Entity; }
   public struct Children { public List<EntityReference> List; }
   ```

2. **创建 TransformHierarchySystem** `src/Kilo.ECS/Systems/TransformHierarchySystem.cs`：
   - 在 KiloStage.PostUpdate 中运行，在 ComputeLocalToWorld 之前
   - 遍历所有有 Parent 组件的实体
   - 按层级深度排序（先处理深度小的）
   - 对于有父节点的实体：`worldMatrix = parentWorldMatrix × localMatrix`
   - 对于没有父节点的实体：`worldMatrix = localMatrix`（保持当前行为）

3. **修改 ComputeLocalToWorld**：
   - 如果实体有 Parent 组件，使用 `parent.LocalToWorld.Value × localMatrix`
   - 如果没有 Parent，使用 `localMatrix`（保持当前行为不变）

4. **添加辅助方法到 KiloWorld**：
   ```csharp
   // 设置父节点
   public static void SetParent(KiloWorld world, Entity child, Entity parent);
   // 取消父节点
   public static void RemoveParent(KiloWorld world, Entity child);
   ```

5. **在 RenderingPlugin.Build() 和 ComputeBlurPlugin 中注册 TransformHierarchySystem**：
   - 在 `KiloStage.PostUpdate` 中，在 `ComputeLocalToWorld` 之前注册

**关键文件**：
- `src/Kilo.ECS/` — 添加组件和系统
- `src/Kilo.Rendering/RenderingPlugin.cs` — ComputeLocalToWorld 方法需要修改
- `src/Kilo.ECS/App/KiloWorld.cs` — 了解 Entity API

**参考 TinyEcs API**：
- 查看 `3rd-party/TinyEcs/` 了解 Entity 和 World 的 API
- Entity reference 可能是 `EntityReference` 或直接使用 `Entity` struct

**验证**：在 RenderDemo sample 中创建一个父实体，挂载多个子立方体，旋转父实体时子实体跟随旋转。

---

### 任务 4：视锥裁剪（Frustum Culling）

**目标**：在 CPU 端剔除视锥外的物体，不发送到 GPU 渲染。

**实现步骤**：

1. **创建 BoundingBox 组件** `src/Kilo.Rendering/Components/BoundingBox.cs`：
   ```csharp
   public struct BoundingBoxLocal
   {
       public Vector3 Min; // 本地空间 AABB 最小点
       public Vector3 Max; // 本地空间 AABB 最大点
   }
   ```

2. **创建 Frustum 结构** `src/Kilo.Rendering/Resources/Frustum.cs`：
   ```csharp
   public struct Frustum
   {
       public Plane[] Planes; // 6 个裁剪面
       public static Frustum FromViewProjection(Matrix4x4 viewProjection);
       public bool IntersectsAABB(Vector3 worldMin, Vector3 worldMax);
   }
   ```

3. **创建 FrustumCullingSystem** `src/Kilo.Rendering/Systems/FrustumCullingSystem.cs`：
   - 在 KiloStage.PostUpdate 中运行，在 PrepareGpuSceneSystem 之前
   - 从相机 ViewProjection 矩阵构建 Frustum
   - 遍历所有有 MeshRenderer + LocalToWorld + BoundingBoxLocal 的实体
   - 将 Local AABB 变换到世界空间（取 AABB 的 8 个顶点 × world matrix，重新算 world AABB）
   - 检测是否在 Frustum 内
   - 添加/移除 `Culled` tag component 或设置 MeshRenderer 中的 Visible 标志

4. **修改 PrepareGpuSceneSystem**：
   - 跳过被裁剪的实体（检查 Culled 标志或 Visible 字段）

5. **在 MeshRenderer 中添加 Visible 字段**：
   ```csharp
   public struct MeshRenderer
   {
       public int MeshHandle;
       public int MaterialHandle;
       public bool Visible = true; // 默认可见
   }
   ```

6. **在 ModelLoader 加载模型时自动计算 BoundingBox**。

**关键文件**：
- `src/Kilo.Rendering/Components/` — 添加 BoundingBox
- `src/Kilo.Rendering/Resources/` — 添加 Frustum
- `src/Kilo.Rendering/Systems/PrepareGpuSceneSystem.cs` — 添加裁剪检查
- `src/Kilo.Rendering/Resources/GpuSceneData.cs` — MeshRenderer 定义

**验证**：创建 100 个立方体分布在大范围空间中，移动相机观察控制台输出的 DrawCount 是否正确变化。

---

### 任务 5：文字渲染（Text/Font Rendering）

**目标**：支持在屏幕上渲染 UTF-8 文本（用于 UI、调试信息）。

**实现步骤**：

1. **添加依赖**：在 `src/Kilo.Rendering/Kilo.Rendering.csproj` 中添加：
   ```
   <PackageReference Include="SixLabors.Fonts" Version="1.0.*" />
   ```
   SixLabors.Fonts 与已有的 SixLabors.ImageSharp 是同一生态系统。

2. **创建 FontAtlas** `src/Kilo.Rendering/Resources/FontAtlas.cs`：
   - 使用 SixLabors.Fonts 将字符光栅化为像素
   - 为每个需要的字符生成字形位图
   - 打包到一张大纹理（Font Atlas）中
   - 记录每个字符的 UV 坐标和尺寸信息
   ```csharp
   public sealed class FontAtlas
   {
       public ITexture Texture;
       public Dictionary<char, GlyphInfo> Glyphs; // char → (uv, size, offset, advance)
       public int AtlasWidth, AtlasHeight;
   }
   public struct GlyphInfo
   {
       public Vector2 UVMin, UVMax;
       public Vector2 Size;
       public Vector2 Offset;
       public float Advance;
   }
   ```

3. **创建 TextRenderer 组件** `src/Kilo.Rendering/Components/TextRenderer.cs`：
   ```csharp
   public struct TextRenderer
   {
       public string Text;
       public Vector4 Color;
       public float FontSize;
       public int FontAtlasHandle;
   }
   ```

4. **创建 TextRenderSystem** `src/Kilo.Rendering/Systems/TextRenderSystem.cs`：
   - 查询所有 TextRenderer 实体
   - 为每个字符生成一个带 UV 的四边形
   - 使用动态 vertex buffer 上传四边形数据
   - 添加 TextPass 到共享 RenderGraph（在 SpritePass 之后或合并）

5. **创建文本着色器**：
   ```wgsl
   struct VertexInput {
       @location(0) position: vec2<f32>,
       @location(1) uv: vec2<f32>,
   };
   struct VertexOutput {
       @builtin(position) clip_pos: vec4<f32>,
       @location(0) uv: vec2<f32>,
       @location(1) color: vec4<f32>,
   };
   @group(0) @binding(0) var<uniform> uniforms: TextUniforms;
   @group(0) @binding(1) var fontAtlas: texture_2d<f32>;
   @group(0) @binding(2) var fontSampler: sampler;

   @vertex fn vs_main(in: VertexInput) -> VertexOutput { ... }
   @fragment fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
       let glyph = textureSample(fontAtlas, fontSampler, in.uv);
       return vec4<f32>(in.color.rgb, in.color.a * glyph.r);
   }
   ```

6. **在 RenderingPlugin 中初始化 Font 资源**：
   - 创建默认 FontAtlas（使用系统字体或内置 TTF）
   - 创建文本渲染 pipeline 和 quad buffer

**关键文件**：
- `src/Kilo.Rendering/Kilo.Rendering.csproj` — 添加 SixLabors.Fonts
- `src/Kilo.Rendering/Resources/FontAtlas.cs` — 新建
- `src/Kilo.Rendering/Components/TextRenderer.cs` — 新建
- `src/Kilo.Rendering/Systems/TextRenderSystem.cs` — 新建
- `src/Kilo.Rendering/Shaders/TextShaders.cs` — 新建
- `src/Kilo.Rendering/RenderingPlugin.cs` — 初始化 Font 资源

**验证**：在 RenderDemo 中添加文字实体显示 "Hello Kilo!" 和 FPS 计数器。

---

## 实现顺序

建议按以下顺序实现，每个任务独立可验证：

1. **场景层级** — 不需要新依赖，是其他功能的基础
2. **视锥裁剪** — 依赖场景层级（世界空间变换），改善性能
3. **模型加载** — 需要添加第三方依赖，复杂度最高
4. **阴影映射** — 修改着色器和渲染流程，需要模型加载后的场景来充分验证
5. **文字渲染** — 独立功能，需要添加 SixLabors.Fonts 依赖

## 架构要点

### ECS 组件注册模式

所有组件都是 struct，放在 `src/Kilo.Rendering/Components/` 或 `src/Kilo.ECS/Components/`：
```csharp
public struct MyComponent
{
    public int Field;
}
```

### System 注册模式

在 `IKiloPlugin.Build()` 中注册：
```csharp
app.AddSystem(KiloStage.PostUpdate, new MySystem().Update);
```

System 类的方法签名：
```csharp
public void Update(KiloWorld world) { ... }
```

### RenderGraph Pass 添加模式

使用共享的 `context.RenderGraph`：
```csharp
var graph = context.RenderGraph;
graph.AddPass("PassName", setup: pass => {
    var texture = pass.CreateTexture(descriptor);
    pass.WriteTexture(texture);
    pass.ColorAttachment(texture, loadAction, storeAction, clearColor);
}, execute: ctx => {
    var encoder = ctx.Encoder;
    encoder.SetPipeline(pipeline);
    // ... draw calls
});
```

### 着色器模式

WGSL 着色器以 C# string 常量存储在 `src/Kilo.Rendering/Shaders/`：
```csharp
public static class MyShaders
{
    public const string WGSL = """
        @group(0) @binding(0) var<uniform> data: MyData;
        // ...
        """;
}
```

### GPU 资源创建模式

通过 IRenderDriver 创建：
```csharp
var buffer = driver.CreateBuffer(new BufferDescriptor {
    Size = ..., Usage = BufferUsage.Vertex | BufferUsage.CopyDst
});
buffer.UploadData<float>(vertexData);

var texture = driver.CreateTexture(new TextureDescriptor {
    Width = ..., Height = ..., Format = DriverPixelFormat.RGBA8Unorm,
    Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding
});
texture.UploadData<byte>(pixelData);
```

### Material 创建模式

通过 MaterialManager（带缓存）：
```csharp
int materialId = context.MaterialManager.CreateMaterial(context, scene, new MaterialDescriptor {
    BaseColor = new Vector4(1, 0, 0, 1),
    AlbedoTexturePath = "assets/texture.png",
});
```

### 项目文件结构

```
src/
├── Kilo.ECS/           — 核心 ECS（KiloApp, KiloWorld, KiloStage）
├── Kilo.Rendering/     — 渲染引擎
│   ├── Assets/         — 资源加载器
│   ├── Components/     — ECS 渲染组件
│   ├── Driver/         — GPU 抽象层（WebGPU 实现）
│   ├── RenderGraph/    — RenderGraph 核心
│   ├── Resources/      — 渲染资源（Material, Mesh, CameraData 等）
│   ├── Shaders/        — WGSL 着色器源码
│   └── Systems/        — ECS 渲染系统
├── Kilo.Input/         — 输入系统
└── Kilo.Assets/        — 资源管理框架

samples/
├── Kilo.Samples.Forward3D/      — 3D 前向渲染示例
├── Kilo.Samples.ComputeBlur/    — Compute 后处理示例
├── Kilo.Samples.Rendering/      — 2D 精灵示例
├── Kilo.Samples.Input/          — 输入示例
└── Kilo.Samples.RenderDemo/     — 统合示例
```

## 验证总则

每个任务完成后：
1. `dotnet build` — 零错误
2. 运行 `dotnet run --project samples/Kilo.Samples.RenderDemo` — 现有功能不受影响
3. 运行对应验证场景 — 新功能正常工作
