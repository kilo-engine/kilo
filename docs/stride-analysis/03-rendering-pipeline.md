# Stride 引擎渲染管线文档

> 深入解析 Stride 游戏引擎的渲染管线架构与实现细节  
> 基于 Stride 源码（`Stride.Rendering`、`Stride.Engine`、`Stride.Graphics`）分析整理

---

## 一、渲染架构概览

Stride 的渲染架构采用**高度模块化、多阶段并行**的设计，核心目标是在保证灵活性的同时最大化 CPU/GPU 利用率。整个架构可分为三个层次：

1. **底层图形 API 层** (`Stride.Graphics`)：`GraphicsDevice`、`CommandList`、`PipelineState` 等，抽象 D3D11/D3D12/Vulkan/OpenGL
2. **高层渲染框架层** (`Stride.Rendering`)：`RenderSystem`、`RenderFeature`、`RenderView`、`RenderStage`、`GraphicsCompositor`
3. **具体渲染实现层** (`Stride.Engine`)：`ForwardRenderer`、`MeshRenderFeature`、`ModelRenderProcessor` 等

### 1.1 核心设计哲学

- **数据驱动**：渲染逻辑按 `RenderFeature` 拆分，每种可渲染对象类型（Mesh、Sprite、Particle）对应一个 Feature
- **视图优先 (View-First)**：`RenderView` 是一等公民，支持多视图并行批处理（分屏、VR、Shadow Cascade）
- **阶段解耦**：`Collect` → `Extract` → `Prepare` → `Draw` 四阶段严格分离，便于并行化和优化
- **Effect/Shader 深度融合**：材质、光照、后处理均通过 Effect System 动态组合 Shader Permutation

---

## 二、核心类与数据流

### 2.1 核心类关系图

```
GraphicsCompositor
    └── RenderSystem
            ├── RenderFeatures[]      (RootRenderFeature)
            │       ├── MeshRenderFeature
            │       ├── SpriteRenderFeature
            │       ├── ParticleRenderFeature
            │       └── TransformRenderFeature
            ├── RenderStages[]        (Opaque, Transparent, ShadowCaster...)
            └── Views[]               (RenderView)
                    ├── RenderObjects[]
                    ├── RenderStages[]   (RenderViewStage)
                    │       ├── RenderNodes[]
                    │       └── SortedRenderNodes[]
                    └── Features[]       (RenderViewFeature)
```

### 2.2 节点层次结构（每帧、每对象）

对于每个可见对象，渲染框架会创建三层节点：

| 节点类型 | 作用域 | 说明 |
|----------|--------|------|
| **ObjectNode** | 每对象 | 对象级别的静态/动态数据 |
| **ViewObjectNode** | 每对象 × 每视图 | 视图相关的对象数据（如世界矩阵） |
| **RenderNode** | 每对象 × 每视图 × 每 Stage × 每 Effect | 真正的绘制单元，包含 PipelineState、ResourceGroup 等 |

源码位置：`RootRenderFeature.GetOrCreateObjectNode()`、`CreateViewObjectNode()`、`CreateRenderNode()`

### 2.3 RenderSystem — 渲染管线的中央调度器

```csharp
public class RenderSystem : ComponentBase
{
    public FastTrackingCollection<RenderStage> RenderStages { get; }
    public FastTrackingCollection<RootRenderFeature> RenderFeatures { get; }
    public FastTrackingCollection<RenderView> Views { get; }
    public GraphicsDevice GraphicsDevice { get; }
    public EffectSystem EffectSystem { get; }

    public void Collect(RenderContext context);
    public void Extract(RenderContext context);
    public void Prepare(RenderDrawContext context);
    public void Draw(RenderDrawContext context, RenderView renderView, RenderStage renderStage);
    public void Flush(RenderDrawContext context);
    public void Reset();
}
```

`RenderSystem` 维护所有视图、阶段和特性，负责按顺序驱动四个渲染阶段。

---

## 三、四阶段渲染管线详解

### 3.1 Collect 阶段 — 收集与配置

**触发点**：`GraphicsCompositor.DrawCore()` → `Game.Collect()` → `RenderSystem.Collect()`

**职责**：
- 由 `GraphicsCompositor` 驱动，创建并配置 `RenderView`
- 设置摄像机参数（View / Projection / ViewProjection / Frustum）
- 配置 `RenderStage` 的输出格式（Color/Depth Format、MSAA、Viewport）
- 执行视锥体剔除，填充 `RenderView.RenderObjects`

**关键代码流程**：

```csharp
// GraphicsCompositor.DrawCore()
Game.Collect(context);
RenderSystem.Collect(context);
visibilityGroup.TryCollect(view);  // 填充 view.RenderObjects
```

`SceneCameraRenderer.CollectCore()` 示例：
- 从 `CameraComponent` 提取 View/Projection 矩阵
- 计算 `ViewProjection` 和 `BoundingFrustum`
- 将 `RenderView` 压入 `RenderSystem.Views`

`ForwardRenderer.CollectCore()` 示例：
- 配置 `OpaqueRenderStage.Output`：颜色格式 `R16G16B16A16_Float`（HDR）或 BackBuffer 格式（LDR），深度格式 `D24_UNorm_S8_UInt`
- 验证 `OutputValidator`，若后处理需要法线/速度/材质索引，则自动追加 MRT Target
- 注册 `Opaque`、`Transparent`、`GBuffer` 等 Stage 到 `context.RenderView.RenderStages`
- VR 支持：计算双眼 View/Projection，创建公共 Culling View，然后注册两个 Eye View

### 3.2 Extract 阶段 — 数据提取

**触发点**：`RenderSystem.Extract(context)`

**设计目标**：
- **越快越好**，此阶段会阻塞游戏逻辑更新和脚本执行
- 只做数据拷贝，不做重计算，重计算推迟到 Prepare

**详细流程**（源码：`RenderSystem.cs:110`）：

#### 1) View 初始化
- 为每个 `RenderView` 确保其 `Features` 列表与 `RenderFeatures` 一一对应
- 为每个 `RenderView.RenderStages` 分配 `RenderNodes` 和 `SortedRenderNodes` 容器

#### 2) 并行对象处理

```csharp
Dispatcher.ForEach(Views, view =>
{
    // 按 RenderFeature.Index 排序 RenderObjects
    Dispatcher.Sort(view.RenderObjects, RenderObjectFeatureComparer.Default);

    // 并行遍历每个可见对象
    Dispatcher.ForEach(view.RenderObjects, () => extractThreadLocals.Value, (renderObject, batch) =>
    {
        var renderFeature = renderObject.RenderFeature;
        var viewFeature = view.Features[renderFeature.Index];

        // 创建 ObjectNode
        renderFeature.GetOrCreateObjectNode(renderObject);

        // 创建 ViewObjectNode
        var renderViewNode = renderFeature.CreateViewObjectNode(view, renderObject);
        viewFeature.ViewObjectNodes.Add(renderViewNode, batch.ViewFeatureObjectNodeCache);

        // 遍历该视图的所有 RenderStage
        foreach (var renderViewStage in view.RenderStages)
        {
            var stageIndex = renderViewStage.Index;
            if (!renderObject.ActiveRenderStages[stageIndex].Active)
                continue;

            // Stage Filter 过滤
            if (renderStage.Filter != null && !renderStage.Filter.IsVisible(renderObject, view, renderViewStage))
                continue;

            // 创建 RenderNode（真正的绘制单元）
            var renderNode = renderFeature.CreateRenderNode(renderObject, view, renderViewNode, renderStage);
            viewFeature.RenderNodes.Add(renderNode, batch.ViewFeatureRenderNodeCache);
            renderViewStage.RenderNodes.Add(new RenderNodeFeatureReference(renderFeature, renderNode, renderObject), batch.ViewStageRenderNodeCache);
        }
    }, batch => batch.Flush());
});
```

#### 3) 节点收集收尾
- 关闭各 `ConcurrentCollector`，确保数据完整
- 按 `RootRenderFeature.Index` 对 `RenderViewStage.RenderNodes` 排序

#### 4) Feature Extract
- 调用每个 `RootRenderFeature.Extract()`，让各 Feature 提取自身需要的数据
  - `MeshRenderFeature`：提取 MeshDraw、Material、WorldMatrix
  - `ForwardLightingRenderFeature`：收集场景光源

### 3.3 Prepare 阶段 — 资源准备与计算

**触发点**：`RenderSystem.Prepare(context)`

**设计目标**：
- 可以执行较重计算，因为此时游戏逻辑更新已经恢复运行
- 编译 Effect、生成 PipelineState、分配 GPU 资源、排序 RenderNode

**详细流程**（源码：`RenderSystem.cs:250`）：

#### 1) Effect Permutation 编译

```csharp
foreach (var renderFeature in RenderFeatures)
{
    renderFeature.PrepareEffectPermutations(context);
}
```

`RootEffectRenderFeature` 在此阶段：
- 遍历所有 `RenderNode`，标记本帧使用的 `RenderEffect`
- 子类（如 `ForwardLightingRenderFeature`）通过 `EffectValidator` 声明 Shader Permutation（如光照组、Render Target 扩展）
- 若 Effect 参数变化，调用 `EffectSystem.LoadEffect()` 异步编译
- 若仍在编译中，使用 Fallback Effect（通常为纯绿色着色器）
- 新编译完成的 Effect 更新 `RenderEffectReflection`：
  - 解析 bytecode 生成 `EffectDescriptorSetReflection`
  - 创建 `RootSignature`
  - 准备 `BufferUploader`（Constant Buffer 内存布局）
  - 创建 `PerDrawLayout`、`PerViewLayout`、`PerFrameLayout`

#### 2) Feature Prepare

```csharp
foreach (var renderFeature in RenderFeatures)
{
    renderFeature.Prepare(context);
}
```

`RootEffectRenderFeature.Prepare()`：
- 分配 `ResourceGroup`（Descriptor Set + Constant Buffer）：
  - **PerFrame**：每个 Effect 一个，跨视图共享
  - **PerView**：每个视图一个，该视图内所有对象共享
  - **PerDraw**：每个 `RenderNode` 一个，从 `ResourceGroupAllocator` 唯一分配
- 通过 `MutablePipelineState` 编译最终的 `PipelineState`

`MeshRenderFeature.Prepare()`：
- 构建 `InputElementDescription[]`：将 Shader 输入属性与 Mesh 的 Vertex Buffer 匹配
- 缺失的属性绑定到 dummy `emptyBuffer`

#### 3) RenderNode 排序

```csharp
Dispatcher.ForEach(Views, view =>
{
    Dispatcher.For(0, view.RenderStages.Count, () => prepareThreadLocals.Acquire(), (index, local) =>
    {
        var renderViewStage = view.RenderStages[index];
        var renderStage = RenderStages[renderViewStage.Index];
        var sortedRenderNodes = renderViewStage.SortedRenderNodes;

        if (renderStage.SortMode != null)
        {
            // 生成 SortKey
            renderStage.SortMode.GenerateSortKey(view, renderViewStage, sortKeysPtr);
            Dispatcher.Sort(local.SortKeys, 0, renderNodes.Count, Comparer<SortKey>.Default);

            // 按 SortKey 重排 SortedRenderNodes
            for (int i = 0; i < renderNodes.Count; ++i)
                sortedRenderNodes[i] = renderNodes[local.SortKeys[i].Index];
        }
        else
        {
            // 无排序，直接复制
            for (int i = 0; i < renderNodes.Count; ++i)
                sortedRenderNodes[i] = renderNodes[i];
        }
    }, state => prepareThreadLocals.Release(state));
});
```

**内置排序模式**：
- `FrontToBackSortMode`：从前到后，减少 Overdraw，适用于不透明物体
- `BackToFrontSortMode`：严格从后到前，适用于透明物体
- `StateChangeSortMode`：优先减少状态切换

#### 4) 资源上传

```csharp
context.ResourceGroupAllocator.Flush();
context.RenderContext.Flush();
```

将所有 Prepare 阶段生成的 Constant Buffer、Descriptor Set 等资源上传到 GPU。

### 3.4 Draw 阶段 — GPU 命令提交

**触发点**：`RenderSystem.Draw(renderDrawContext, renderView, renderStage)`

**职责**：填充 GPU Command List，执行实际绘制

#### 立即渲染路径 (`!GraphicsDevice.IsDeferred`)

```csharp
int currentStart, currentEnd;
for (currentStart = 0; currentStart < renderNodeCount; currentStart = currentEnd)
{
    var currentRenderFeature = renderNodes[currentStart].RootRenderFeature;
    currentEnd = currentStart + 1;
    while (currentEnd < renderNodeCount && renderNodes[currentEnd].RootRenderFeature == currentRenderFeature)
        currentEnd++;

    currentRenderFeature.Draw(renderDrawContext, renderView, renderViewStage, currentStart, currentEnd);
}
```

- `SortedRenderNodes` 已按 `RootRenderFeature` 分组
- 主线程依次调用各 `RootRenderFeature.Draw(start, end)`
- `MeshRenderFeature.Draw()` 内部：
  - 按 `MeshDraw` 批次合并，减少 `SetVertexBuffer` / `SetIndexBuffer` 调用
  - 计算 `resourceGroupOffset`
  - 应用 Constant Buffer 更新：`BufferUploader.Apply()`
  - 绑定 Descriptor Sets
  - 设置 `PipelineState`
  - 调用 `CommandList.DrawIndexed()` / `DrawInstanced()`

#### 延迟渲染 / 多线程 Command List 路径 (`GraphicsDevice.IsDeferred`)

```csharp
int batchCount = Math.Min(Dispatcher.MaxDegreeOfParallelism, renderNodeCount);
int batchSize = (renderNodeCount + (batchCount - 1)) / batchCount;

// 1. 关闭主 Command List 到当前进度
commandLists[0] = commandList.Close();

// 2. 并行录制各批次
Dispatcher.For(0, batchCount, () => renderDrawContext.RenderContext.GetThreadContext(), (batchIndex, threadContext) =>
{
    threadContext.CommandList.Reset();
    threadContext.CommandList.ClearState();

    // 复制主 CL 的 RenderTarget/Viewport/Scissor 状态
    threadContext.CommandList.SetRenderTargets(depthStencilBuffer, renderTargets);
    threadContext.CommandList.SetViewport(viewport);
    threadContext.CommandList.SetScissorRectangle(scissor);

    // 绘制该批次
    var currentStart = batchSize * batchIndex;
    var endExclusive = Math.Min(renderNodeCount, currentStart + batchSize);
    for (; currentStart < endExclusive; currentStart = currentEnd)
    {
        var currentRenderFeature = renderNodes[currentStart].RootRenderFeature;
        currentEnd = currentStart + 1;
        while (currentEnd < endExclusive && renderNodes[currentEnd].RootRenderFeature == currentRenderFeature)
            currentEnd++;

        currentRenderFeature.Draw(threadContext, renderView, renderViewStage, currentStart, currentEnd);
    }

    commandLists[batchIndex + 1] = threadContext.CommandList.Close();
});

// 3. 一次性提交所有 Command List
GraphicsDevice.ExecuteCommandLists(batchCount + 1, commandLists);

// 4. 重置主 Command List 并恢复状态
commandList.Reset();
commandList.ClearState();
commandList.SetRenderTargets(depthStencilBuffer, renderTargets);
commandList.SetViewport(viewport);
commandList.SetScissorRectangle(scissor);
```

- 为每个 CPU 核心分配一个 `CommandList`
- 多线程并行录制 GPU 命令
- 最后通过 `ExecuteCommandLists` 批量提交
- 对 D3D12/Vulkan 等现代 API 至关重要，可显著降低 Draw Call CPU 开销

---

## 四、GraphicsCompositor（图形合成器）

`GraphicsCompositor` 是渲染管线的**顶层编排器**，定义了每帧的渲染流程图（Frame Graph）。

### 4.1 核心结构

```csharp
public class GraphicsCompositor : RendererBase
{
    public RenderSystem RenderSystem { get; } = new RenderSystem();
    public SceneCameraSlotCollection Cameras { get; }
    public ISceneRenderer Game { get; set; }
    public ISceneRenderer SingleView { get; set; }
    public ISceneRenderer Editor { get; set; }
}
```

### 4.2 入口点 (Entry Points)

| 入口点 | 用途 |
|--------|------|
| **Game** | 游戏主渲染管线 |
| **Editor** | Game Studio 编辑器视口渲染 |
| **SingleView** | 单视图渲染（Light Probe、Cubemap、反射贴图） |

### 4.3 一帧的完整流程

```csharp
// GraphicsCompositor.DrawCore()
Game.Collect(context);
RenderSystem.Collect(context);
visibilityGroup.TryCollect(view);
RenderSystem.Extract(context);
RenderSystem.Prepare(context);
Game.Draw(context);
RenderSystem.Flush(context);
RenderSystem.Reset();
```

### 4.4 Compositor 节点类型

| 节点 | 功能 |
|------|------|
| `SceneCameraRenderer` | 从 CameraComponent 创建 RenderView |
| `ForwardRenderer` | 前向渲染主节点，处理阴影、不透明、透明、后处理 |
| `SingleStageRenderer` | 简单节点，只渲染一个 RenderStage |
| `ClearRenderer` | 清除 RenderTarget / DepthStencil |
| `PostProcessingEffects` | 后处理效果链 |
| `DebugRenderer` | 调试绘制（Gizmos、Wireframe） |

---

## 五、ForwardRenderer 详解

`ForwardRenderer` 是 Stride 默认的主渲染器，实现了完整的前向渲染流程。

### 5.1 包含的 RenderStage

```csharp
public RenderStage OpaqueRenderStage { get; set; }      // 不透明物体
public RenderStage TransparentRenderStage { get; set; }  // 透明物体
public RenderStage GBufferRenderStage { get; set; }      // Light Probe G-Buffer
public List<RenderStage> ShadowMapRenderStages { get; }  // 级联阴影贴图
```

### 5.2 DrawCore 流程

```csharp
// 1. 阴影贴图
shadowMapRenderer?.Draw(drawContext);

// 2. 主视图渲染（VR 会循环双眼）
PrepareRenderTargets();  // 分配临时 Color/Depth Buffer（含 MSAA 处理）
Clear.Draw(drawContext);
DrawView();

// 3. MSAA Resolve（若启用）
if (actualMultisampleCount != None) ResolveMSAA(drawContext);

// 4. 后处理
PostEffects.Draw(drawContext, OpaqueRenderStage.OutputValidator, renderTargets, depthStencil, viewOutputTarget);
```

### 5.3 DrawView 内部流程

```csharp
// 1. Light Probe 预烘培（若启用）
if (lightProbes) BakeLightProbes(context, drawContext);

// 2. 不透明通道
renderSystem.Draw(drawContext, context.RenderView, OpaqueRenderStage);

// 3. 次表面散射（SSS，若启用）
SubsurfaceScatteringBlurEffect.Draw(...);

// 4. 透明通道
// 可选：将 Depth Buffer 解析为 SRV 供软粒子使用
renderSystem.Draw(drawContext, context.RenderView, TransparentRenderStage);

// 5. 将结果复制到输出目标
```

### 5.4 深度/颜色解析为 SRV

某些效果（如软粒子、折射）需要读取当前帧的 Depth 或 Color：

```csharp
// 解析 Depth Buffer 为 Shader Resource View
ResolveDepthAsSRV();
RootRenderFeature.BindPerViewShaderResource("Depth", renderView, depthStencilSRV);

// 解析 Opaque Color 为 SRV
ResolveRenderTargetAsSRV();
RootRenderFeature.BindPerViewShaderResource("Opaque", renderView, opaqueSRV);
```

---

## 六、MeshRenderFeature 与 ModelRenderProcessor

### 6.1 ModelRenderProcessor

`ModelRenderProcessor` 是 ECS 中的 Processor，负责将 `ModelComponent` 转换为可渲染对象。

```csharp
public class ModelRenderProcessor : EntityProcessor<ModelComponent, RenderModel>
{
    public override void Draw(RenderContext context)
    {
        // 每帧检查模型/材质变化，重建 RenderMesh
        CheckMeshes();
        UpdateRenderModel();
        // 将 RenderMesh 注册到 VisibilityGroup
    }
}
```

### 6.2 RenderMesh

```csharp
public class RenderMesh : RenderObject
{
    public MeshDraw ActiveMeshDraw;      // 当前使用的 Draw Call 描述
    public RenderModel RenderModel;      // 模型级数据
    public Mesh Mesh;
    public MaterialPass MaterialPass;    // 材质 Pass
    public Matrix World;                 // 世界矩阵
    public Matrix[] BlendMatrices;       // 骨骼蒙皮矩阵
    public int InstanceCount;            // 实例化数量
    public bool IsShadowCaster;          // 是否投射阴影
}
```

### 6.3 MeshRenderFeature

`MeshRenderFeature` 继承自 `RootEffectRenderFeature`，专门处理 `RenderMesh`。

**支持的 SubRenderFeature**：
- `ForwardLightingRenderFeature`：前向光照计算
- `MaterialRenderFeature`：材质参数绑定
- `ShadowMapRenderFeature`：阴影贴图渲染

**PrepareEffectPermutationsImpl**：
- 设置 `renderMesh.ActiveMeshDraw = renderMesh.Mesh.Draw`

**ProcessPipelineState**：
- 匹配 Shader Input Layout 与 Mesh Vertex Buffer
- 缺失的属性绑定到 `emptyBuffer`

**Draw(startIndex, endIndex)**：
- 批次合并：连续相同的 `MeshDraw` 合并为一次 SetVertexBuffer/SetIndexBuffer
- 对每个 `RenderNode`：
  1. 计算 Resource Group 偏移
  2. `BufferUploader.Apply()` 更新 Constant Buffer
  3. 绑定 Descriptor Sets
  4. `commandList.SetPipelineState()`
  5. `commandList.DrawIndexed()` / `DrawInstanced()`

---

## 七、VisibilityGroup 与可见性裁剪

### 7.1 VisibilityGroup 结构

```csharp
public class VisibilityGroup
{
    public ConcurrentCollector<RenderObject> RenderObjects { get; }
    
    public bool TryCollect(RenderView renderView)
    {
        // 检查本帧是否已收集
        if (LastFrameCollected == RenderSystem.FrameCounter)
            return false;
        LastFrameCollected = RenderSystem.FrameCounter;

        // 并行视锥体剔除
        Dispatcher.For(0, RenderObjects.Count, (index) =>
        {
            var renderObject = RenderObjects[index];
            // Frustum Culling
            // Stage Mask 匹配
            // 计算 Min/Max Distance
        });
    }
}
```

### 7.2 裁剪流程

1. **Frustum Culling**：使用 `BoundingFrustum` 测试 AABB
2. **RenderGroup Masking**：`RenderObject.RenderGroup` & `RenderView.CullingMask`
3. **Stage Masking**：检查 `RenderObject.ActiveRenderStages`
4. **距离计算**：计算 AABB 到近平面的距离，用于 LOD 和排序

---

## 八、RenderStage 与 Effect Slot

### 8.1 RenderStage

`RenderStage` 定义了一个渲染通道的输出和对象排序方式：

```csharp
public class RenderStage
{
    public string Name { get; set; }
    public RenderOutput Output { get; set; }
    public SortMode SortMode { get; set; }
    public RenderStageFilter Filter { get; set; }
}
```

### 8.2 典型 Stage 配置

| Render Stage | Effect Slot | SortMode | 用途 |
|--------------|-------------|----------|------|
| Opaque | Main | FrontToBack | 不透明物体，减少 Overdraw |
| Transparent | Main | BackToFront | 透明物体，正确混合顺序 |
| ShadowCaster | ShadowCaster | FrontToBack | 阴影投射 |
| UI | Main | StateChange | 界面渲染 |

### 8.3 RenderStageSelector

`RenderStageSelector` 决定对象进入哪个 Stage 以及使用哪个 Effect：

```csharp
// 典型网格选择器
MeshTransparentRenderStageSelector:
    - 根据材质属性选择 Main 或 Transparent Stage
    - 默认 Effect: StrideForwardShadingEffect

ShadowMapRenderStageSelector:
    - 选择不透明且投射阴影的网格
    - 加入 ShadowMapCaster Stage
    - 默认 Effect: StrideForwardShadingEffect.ShadowMapCaster
```

---

## 九、Effect/Shader 系统集成

### 9.1 RootEffectRenderFeature

`RootEffectRenderFeature` 是渲染管线与着色器系统的桥梁。

**关键数据结构**：

```csharp
public abstract class RootEffectRenderFeature : RenderFeature
{
    public RenderDataHolder RenderData;
    public List<RenderObject> RenderObjects;
    public ConcurrentCollector<ObjectNodeReference> ObjectNodeReferences;
    public ConcurrentCollector<RenderNode> RenderNodes;
    public FastTrackingCollection<RenderStageSelector> RenderStageSelectors;
}
```

### 9.2 ResourceGroup 分配策略

| 资源组 | 分配策略 | 生命周期 |
|--------|----------|----------|
| **PerFrame** | 每 Effect 1 个 | 整帧共享 |
| **PerView** | 每 Effect × 每 View 1 个 | 视图内共享 |
| **PerDraw** | 每 RenderNode 1 个 | 每次绘制唯一 |

所有 `PerDraw` ResourceGroup 从 `ResourceGroupAllocator` 池中分配，按 `renderNode.Index * slotCount + slotIndex` 索引。

### 9.3 Constant Buffer 更新

```csharp
// BufferUploader 管理 Constant Buffer 的 CPU 端内存
bufferUploader.Apply(commandList, resourceGroupOffset);

// 内部通过 GraphicsDevice 的 Map/Unmap 或 UpdateSubresource 上传到 GPU
```

---

## 十、VR 与多视图渲染

### 10.1 VR 渲染流程

`ForwardRenderer` 对 VR 做了特殊处理：

1. **创建公共 Culling View**：基于双眼中间位置，避免重复裁剪
2. **创建左眼/右眼 RenderView**：
   - 分别计算 View/Projection 矩阵
   - `LightingView` 指向公共 Culling View，共享阴影贴图和光照结果
3. **DrawCore 循环绘制双眼**：
   - 先渲染公共的 Shadow Map（一次）
   - 然后分别绘制左眼和右眼的 Color/Depth

### 10.2 多视图批处理

由于 `RenderView` 是一等公民，Extract/Prepare 阶段可以跨多个 View 批处理对象，减少重复计算。

---

## 十一、渲染管线性能优化要点

### 11.1 并行化

- **Extract**：`Dispatcher.ForEach` 并行处理所有可见对象
- **Prepare**：`Dispatcher.For` 并行处理所有 RenderStage 的排序
- **Draw**：延迟模式下多线程并行录制 CommandList

### 11.2 内存优化

- `ConcurrentCollector`、`FastTrackingCollection` 减少每帧 GC
- `RenderNodePool`、`SortedRenderNodePool` 对象池复用
- `ThreadLocal<ExtractThreadLocals>` 避免线程竞争

### 11.3 状态排序

- `FrontToBackSortMode` 减少不透明物体的 Overdraw
- `StateChangeSortMode` 减少 Pipeline State 切换
- `MeshRenderFeature` 内部按 `MeshDraw` 合并批次

### 11.4 异步着色器编译

- `EffectSystem.LoadEffect()` 支持后台异步编译
- 编译完成前使用 Fallback Effect，避免主线程阻塞
- 编译结果磁盘缓存，下次启动秒加载

---

## 十二、总结

Stride 的渲染管线是一个设计精良、高度并行的现代渲染框架：

1. **四阶段分离**（Collect/Extract/Prepare/Draw）使得 CPU 与 GPU 工作重叠、逻辑与渲染解耦
2. **RenderFeature 插件化**架构让新增可渲染对象类型（如体素、毛发）只需实现一个 Feature
3. **View-First 设计**天然支持分屏、VR、Shadow Cascade 等多视图场景
4. **Effect System 深度融合**让材质、光照、后处理能够动态组合 Shader Permutation
5. **延迟 Command List** 在现代 API（D3D12/Vulkan）上充分发挥多核 CPU 优势
6. **GraphicsCompositor** 提供灵活的数据驱动式 Frame Graph 编排

从源码实现上看，Stride 的渲染管线既适合作为商业游戏引擎的底层框架，也极具学习价值，展示了如何在 C#/.NET 生态中构建接近原生 C++ 性能的现代渲染系统。
