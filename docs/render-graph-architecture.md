# Kilo RenderGraph 架构文档

## 目录

1. [概述](#1-概述)
2. [设计理念](#2-设计理念)
3. [文件结构](#3-文件结构)
4. [核心类型](#4-核心类型)
5. [编译流程](#5-编译流程)
6. [执行流程](#6-执行流程)
7. [资源管理](#7-资源管理)
8. [PassBuilder API 详解](#8-passbuilder-api-详解)
9. [ComputePassBuilder API 详解](#9-computepassbuilder-api-详解)
10. [渲染图编译器](#10-渲染图编译器)
11. [资源池](#11-资源池)
12. [使用示例](#12-使用示例)
13. [系统整体架构](#13-系统整体架构)
14. [已知问题与改进方向](#14-已知问题与改进方向)

---

## 1. 概述

Kilo 的 RenderGraph 是一个**帧级渲染图（Frame Render Graph）**系统，负责自动管理 GPU 资源的生命周期、自动推导 Pass 的执行顺序，以及自动管理 transient（瞬时）资源的创建与回收。

其核心思想是：用户只需声明每个 Pass **需要什么资源**（读/写/创建），以及**如何执行绘制**，RenderGraph 会自动完成以下工作：

- 根据资源依赖关系进行**拓扑排序**，确定 Pass 执行顺序
- 检测**循环依赖**并在编译时报错
- 通过**资源池**复用跨帧 GPU 资源，减少分配开销
- 在窗口尺寸变化时自动**失效**不再匹配的资源

---

## 2. 设计理念

```
                    用户声明侧                              引擎执行侧
            ┌─────────────────────┐                ┌─────────────────────┐
            │  "我需要一张深度纹理"  │                │  分配/复用 GPU 纹理   │
            │  "我写 Backbuffer"   │    ──────>     │  拓扑排序 Pass       │
            │  "我读 CameraBuffer" │                │  自动管理 RenderPass  │
            └─────────────────────┘                └─────────────────────┘
```

关键原则：

- **声明式**：Pass 只声明依赖，不关心执行顺序
- **自动排序**：编译器根据读写关系推导执行顺序
- **资源复用**：通过池化机制复用跨帧 GPU 资源
- **无冗余清除**：LoadAction/StoreAction 由用户在 Pass 声明中显式指定

---

## 3. 文件结构

所有 RenderGraph 相关代码位于 `src/Kilo.Rendering/RenderGraph/` 目录下：

```
RenderGraph/
  ├── RenderGraph.cs                -- 核心图类：编译、执行、资源管理
  ├── RenderGraphBuilder.cs         -- Builder 模式封装
  ├── RenderPass.cs                 -- Pass 定义与附件配置结构体
  ├── PassBuilder.cs                -- 图形 Pass 的声明式 API
  ├── ComputePassBuilder.cs         -- 计算 Pass 的声明式 API
  ├── RenderGraphCompiler.cs        -- 拓扑排序编译器
  ├── RenderGraphResourcePool.cs    -- GPU 资源池
  ├── RenderPassExecutionContext.cs -- 执行时上下文
  ├── RenderResourceHandle.cs       -- 资源句柄类型
  └── ResourceDescriptor.cs         -- 纹理/缓冲区描述符
```

---

## 4. 核心类型

### 4.1 RenderResourceHandle

资源的弱引用句柄，在 setup 阶段分配，在 execute 阶段通过 context 解析为真实 GPU 资源。

```csharp
public readonly struct RenderResourceHandle : IEquatable<RenderResourceHandle>
{
    internal readonly int Id;
    internal readonly RenderResourceType Type;  // Texture 或 Buffer
}
```

- `Id`：全局递增的唯一标识符，由 `RenderGraph._nextHandleId` 分配
- `Type`：枚举 `RenderResourceType { Texture, Buffer }`
- 实现了 `IEquatable`，可通过 `==`/`!=` 比较，用作字典键

### 4.2 RenderResourceType

```csharp
public enum RenderResourceType { Texture, Buffer }
```

### 4.3 RenderPass

一个渲染或计算 Pass 的完整定义：

```
RenderPass
  ├── Name: string                    -- Pass 名称（用于调试）
  ├── IsCompute: bool                 -- 是否为计算 Pass
  ├── ReadResources: List<Handle>     -- 读取的资源列表
  ├── WrittenResources: List<Handle>  -- 写入的资源列表
  ├── CreatedResources: List<Handle>  -- 创建的资源列表
  ├── ColorAttachments: List<Config>  -- 颜色附件配置
  ├── DepthStencilAttachment: Config? -- 深度模板附件配置
  ├── Viewport: Vector4?              -- 视口覆盖
  ├── Scissor: Vector4Int?            -- 裁剪矩形覆盖
  ├── _setup: Action<PassBuilder>     -- setup 回调
  └── _execute: Action<Context>       -- execute 回调
```

### 4.4 资源描述符

#### TextureDescriptor

```csharp
public sealed class TextureDescriptor : IEquatable<TextureDescriptor>
{
    public int Width;
    public int Height;
    public DriverPixelFormat Format;       // 默认 BGRA8Unorm
    public int MipLevelCount;              // 默认 1
    public int SampleCount;                // 默认 1
    public TextureUsage Usage;             // 默认 RenderAttachment
}
```

#### BufferDescriptor

```csharp
public sealed class BufferDescriptor : IEquatable<BufferDescriptor>
{
    public nuint Size;
    public BufferUsage Usage;              // 默认 Vertex
}
```

两者都实现了 `IEquatable`，用于资源池中的精确匹配。

#### TextureUsage / BufferUsage

```csharp
[Flags] public enum TextureUsage
{
    RenderAttachment = 1,
    ShaderBinding    = 2,
    CopyDst          = 4,
    CopySrc          = 8,
    Storage          = 16,
}

[Flags] public enum BufferUsage
{
    Vertex   = 1,
    Index    = 2,
    Uniform  = 4,
    CopyDst  = 8,
    Storage  = 16,
}
```

### 4.5 附件配置结构

#### ColorAttachmentConfig

```csharp
public sealed class ColorAttachmentConfig
{
    public RenderResourceHandle Target;
    public DriverLoadAction LoadAction;      // 默认 Clear
    public DriverStoreAction StoreAction;    // 默认 Store
    public Vector4? ClearColor;
}
```

#### DepthStencilAttachmentConfig

```csharp
public sealed class DepthStencilAttachmentConfig
{
    public RenderResourceHandle Target;
    public DriverLoadAction DepthLoadAction;   // 默认 Clear
    public DriverStoreAction DepthStoreAction; // 默认 Store
    public float? ClearDepth;                  // 默认 1.0f
}
```

### 4.6 RenderPassExecutionContext

Execute 阶段提供给用户回调的上下文对象：

```csharp
public sealed class RenderPassExecutionContext
{
    public IRenderCommandEncoder Encoder { get; }

    public ITexture GetTexture(RenderResourceHandle handle);
    public IBuffer GetBuffer(RenderResourceHandle handle);
    public ITextureView GetTextureView(RenderResourceHandle handle);
}
```

通过 `GetTexture`/`GetBuffer`/`GetTextureView` 方法，用户可以将 setup 阶段获得的 handle 解析为真实的 GPU 资源。

---

## 5. 编译流程

编译由 `RenderGraph.Compile(IRenderDriver driver)` 触发，分为三个阶段：

```
┌────────────────────────────────────────────────────────────────────┐
│                        Compile()                                   │
│                                                                    │
│  Phase 1: Setup                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  foreach pass:                                               │  │
│  │    清除 pass 的所有资源列表和附件配置                              │  │
│  │    创建 PassBuilder(graph, pass)                              │  │
│  │    调用 pass.RunSetup(builder)                                │  │
│  │      └─> 执行用户 setup 回调                                   │  │
│  │          └─> PassBuilder 方法注册资源依赖                        │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                     │
│                              ▼                                     │
│  Phase 2: Topological Sort                                         │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  RenderGraphCompiler.Compile(passes)                         │  │
│  │    1. 构建 resourceFirstWriter: resourceId -> firstWriterIdx │  │
│  │    2. 构建 passReads: passIdx -> Set<resourceId>             │  │
│  │    3. 构建邻接表: writerIdx -> readerIdx                      │  │
│  │    4. Kahn's 算法拓扑排序                                     │  │
│  │    5. 检测循环依赖                                            │  │
│  │    6. 用排序后的列表替换 _passes                               │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                     │
│                              ▼                                     │
│  Phase 3: GPU Resource Allocation                                  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  检查 swapchain 尺寸是否变化                                   │  │
│  │    └─> 变化则调用 _resourcePool.InvalidateForSize()           │  │
│  │  foreach descriptor in _resourceDescriptors:                 │  │
│  │    跳过已解析和已导入的资源                                      │  │
│  │    根据 descriptor 类型:                                       │  │
│  │      TextureDescriptor -> _resourcePool.GetTexture()          │  │
│  │      BufferDescriptor  -> _resourcePool.GetBuffer()           │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                    │
│  标记 _isCompiled = true                                           │
└────────────────────────────────────────────────────────────────────┘
```

### 增量编译机制

编译系统包含**增量编译**优化：

```csharp
private int _structureVersion;          // 每次 AddPass/Reset 时递增
private int _lastCompiledStructureVersion;
private bool _isCompiled;
```

- 当 `_isCompiled == true` 且 `_structureVersion == _lastCompiledStructureVersion` 时，`Compile()` 直接返回
- 任何 Pass 的添加/重置都会递增 `_structureVersion`，使下次编译重新执行

### 注意

`Execute()` 方法会在内部调用 `Compile(driver)`，因此用户不需要手动调用 `Compile()`。

---

## 6. 执行流程

```
┌────────────────────────────────────────────────────────────────────┐
│                      Execute(IRenderDriver)                        │
│                                                                    │
│  1. Compile(driver)                                                │
│                                                                    │
│  2. 确保 "Backbuffer" 导入资源存在                                   │
│     ┌─────────────────────────────────────────────────────────┐    │
│     │  if (!_importedResources.ContainsKey("Backbuffer"))     │    │
│     │    ImportResource("Backbuffer", ...)                     │    │
│     │  // 每帧解析 swapchain 纹理                               │    │
│     │  _resolvedResources[backbufferHandle.Id] =              │    │
│     │    driver.GetCurrentSwapchainTexture()                   │    │
│     └─────────────────────────────────────────────────────────┘    │
│                                                                    │
│  3. 创建命令编码器                                                   │
│     using var encoder = driver.BeginCommandEncoding();             │
│                                                                    │
│  4. 逐 Pass 执行                                                    │
│     ┌─────────────────────────────────────────────────────────┐    │
│     │  foreach pass in _passes:                               │    │
│     │                                                         │    │
│     │    if (pass.IsCompute):                                 │    │
│     │      encoder.BeginComputePass()                         │    │
│     │    else:                                                │    │
│     │      BeginRenderPassForPass(driver, encoder, pass)      │    │
│     │        ├─> 设置 Viewport (如有)                          │    │
│     │        ├─> 设置 Scissor (如有)                           │    │
│     │        └─> encoder.BeginRenderPass(descriptor)          │    │
│     │            (如果 ColorAttachments 非空)                  │    │
│     │                                                         │    │
│     │    var ctx = new RenderPassExecutionContext(...)         │    │
│     │    pass.RunExecute(ctx)                                 │    │
│     │                                                         │    │
│     │    if (pass.IsCompute):                                 │    │
│     │      encoder.EndComputePass()                           │    │
│     │    else:                                                │    │
│     │      encoder.EndRenderPass()                            │    │
│     └─────────────────────────────────────────────────────────┘    │
│                                                                    │
│  5. 提交命令                                                       │
│     encoder.Submit()                                               │
│                                                                    │
│  6. 清理瞬时资源                                                    │
│     ClearTransientResources()                                      │
└────────────────────────────────────────────────────────────────────┘
```

### BeginRenderPassForPass 详解

对于非 Compute Pass，`BeginRenderPassForPass` 会：

1. 如果 Pass 声明了 Viewport 覆盖，调用 `encoder.SetViewport()`
2. 如果 Pass 声明了 Scissor 覆盖，调用 `encoder.SetScissor()`
3. 如果 Pass 声明了 ColorAttachment，构建 `RenderPassDescriptor` 并调用 `encoder.BeginRenderPass(descriptor)`

```
BeginRenderPassForPass
  │
  ├── pass.Viewport 有值?
  │     └── YES: encoder.SetViewport(vp.X, vp.Y, vp.Z, vp.W)
  │
  ├── pass.Scissor 有值?
  │     └── YES: encoder.SetScissor(sc.X, sc.Y, sc.Z, sc.W)
  │
  └── pass.ColorAttachments.Count > 0?
        │
        ├── YES:
        │     foreach ColorAttachment:
        │       texture = GetResolvedTexture(handle)
        │       view = GetOrCreateTextureView(handle, texture)
        │       构建 ColorAttachmentDescriptor
        │
        │     if DepthStencilAttachment:
        │       同上处理深度附件
        │
        │     encoder.BeginRenderPass(RenderPassDescriptor)
        │
        └── NO:
              // 跳过 render pass 设置
              // Pass 可以在 execute 回调中手动调用 BeginRenderPass
```

---

## 7. 资源管理

### 7.1 资源分类

```
                    ┌───────────────────────┐
                    │    RenderGraph 资源     │
                    └───────────┬───────────┘
                                │
                    ┌───────────┴───────────┐
                    │                       │
              ┌─────┴─────┐          ┌──────┴──────┐
              │ Transient │          │  Imported   │
              │  瞬时资源  │          │  导入资源    │
              └─────┬─────┘          └──────┬──────┘
                    │                       │
          通过 CreateTexture/        通过 ImportTexture/
          CreateBuffer 创建          ImportBuffer 导入
                    │                       │
          Compile 阶段通过池          Execute 阶段每帧
          分配 GPU 资源              解析为外部 GPU 资源
                    │                       │
          Execute 后返回池中          不参与池化管理
          复用或释放                  不随图释放
```

### 7.2 Transient（瞬时）资源生命周期

```
     Compile 阶段               Execute 阶段              Execute 之后
    ┌──────────┐            ┌──────────────┐          ┌──────────────┐
    │ 分配 Handle │  ────>   │ 解析为 GPU 资源 │  ────>  │ 返回到资源池   │
    │ (setup 中) │           │ (execute 中)  │          │ (等待下帧复用) │
    └──────────┘            └──────────────┘          └──────────────┘
         │                                                  │
         │  TextureDescriptor                               │
         │  ┌──────────────────┐                            │
         │  │ Width: 1920      │                            │
         │  │ Height: 1080     │       同一描述符匹配 ────>  │  池中查找
         │  │ Format: Depth24  │       直接复用，不重新分配    │  命中则复用
         │  │ Usage: Render    │                            │  未命中则新建
         │  └──────────────────┘                            │
```

### 7.3 Imported（导入）资源

导入资源用于引用 RenderGraph 外部管理的 GPU 资源。通过名称唯一标识。

**当前使用到的导入资源：**

| 名称 | 类型 | 来源 | 用途 |
|------|------|------|------|
| `Backbuffer` | Texture | `driver.GetCurrentSwapchainTexture()` | 交换链颜色输出 |
| `CameraBuffer` | Buffer | `scene.CameraBuffer` | 相机 uniform 数据 |
| `ObjectDataBuffer` | Buffer | `scene.ObjectDataBuffer` | 物体 uniform 数据 |
| `LightBuffer` | Buffer | `scene.LightBuffer` | 光照 uniform 数据 |
| `SpriteUniformBuffer` | Buffer | `context.UniformBuffer` | 精灵实例 uniform |

导入资源的特殊处理：

- 在 `Compile()` 阶段**跳过** GPU 资源创建
- 在 `Execute()` 阶段通过名称解析为真实 GPU 资源
- 在 `ClearTransientResources()` 中**跳过**回收
- 同一名称重复导入返回**同一 handle**

### 7.4 Backbuffer 的特殊处理

`Execute()` 中对 "Backbuffer" 有自动处理逻辑：

```csharp
// 如果没有手动导入，自动创建
if (!_importedResources.ContainsKey("Backbuffer"))
{
    var swapchain = driver.GetCurrentSwapchainTexture();
    ImportResource("Backbuffer", RenderResourceType.Texture, new TextureDescriptor { ... });
}

// 每帧重新解析（swapchain 纹理可能每帧不同）
_resolvedResources[backbufferHandle.Id] = driver.GetCurrentSwapchainTexture();
```

### 7.5 资源句柄 ID 管理

```csharp
private int _nextHandleId;  // 下一个可用的 handle ID

// 分配后自增
internal RenderResourceHandle AllocateHandle(...)
{
    int id = _nextHandleId++;
    ...
}

// 清理瞬态资源后，将 _nextHandleId 重置为导入资源中最大 ID + 1
_nextHandleId = importedIds.Count > 0 ? importedIds.Max() + 1 : 0;
```

### 7.6 TextureView 缓存

TextureView 是按 handle 缓存的，避免重复创建：

```csharp
private readonly Dictionary<int, ITextureView> _textureViews = [];

internal ITextureView GetOrCreateTextureView(driver, handle, texture)
{
    if (_textureViews.TryGetValue(handle.Id, out var view))
        return view;
    view = driver.CreateTextureView(texture, ...);
    _textureViews[handle.Id] = view;
    return view;
}
```

TextureView 在 `ClearTransientResources()` 中随瞬态资源一起释放。

---

## 8. PassBuilder API 详解

`PassBuilder` 是图形 Pass 的声明式构建 API，在 setup 回调中使用。

### 8.1 方法一览

```
PassBuilder
  │
  ├── 资源创建
  │     ├── CreateTexture(TextureDescriptor) → RenderResourceHandle
  │     └── CreateBuffer(BufferDescriptor)   → RenderResourceHandle
  │
  ├── 依赖声明
  │     ├── Read(RenderResourceHandle)       → PassBuilder
  │     ├── Write(RenderResourceHandle)      → PassBuilder
  │     ├── ReadTexture(Handle)              → PassBuilder    (Read 的别名)
  │     ├── WriteTexture(Handle)             → PassBuilder    (Write 的别名)
  │     ├── ReadBuffer(Handle)               → PassBuilder    (Read 的别名)
  │     └── WriteBuffer(Handle)              → PassBuilder    (Write 的别名)
  │
  ├── 附件配置
  │     ├── ColorAttachment(handle, load, store, clear) → PassBuilder
  │     └── DepthStencilAttachment(handle, load, store, clear) → PassBuilder
  │
  ├── 外部资源导入
  │     ├── ImportTexture(name, descriptor)  → RenderResourceHandle
  │     └── ImportBuffer(name, descriptor)   → RenderResourceHandle
  │
  └── 渲染状态覆盖
        ├── SetViewport(x, y, w, h)         → PassBuilder
        └── SetScissor(x, y, w, h)          → PassBuilder
```

### 8.2 方法详细说明

#### CreateTexture / CreateBuffer

在 Pass 的 `CreatedResources` 列表中注册一个新瞬时资源。handle 分配后，在 Compile 的 Phase 3 中会通过资源池创建对应的 GPU 资源。

```csharp
var depth = pass.CreateTexture(new TextureDescriptor
{
    Width = 1920,
    Height = 1080,
    Format = DriverPixelFormat.Depth24Plus,
    Usage = TextureUsage.RenderAttachment,
});
```

#### Read / Write（及其别名）

将资源句柄添加到 Pass 的 `ReadResources` 或 `WrittenResources` 列表。这些信息用于编译器构建依赖图。

注意：`ReadTexture`/`WriteTexture`/`ReadBuffer`/`WriteBuffer` 只是语义化的别名，底层调用的是同一个 `Read`/`Write` 方法。

```csharp
pass.WriteTexture(depth);      // 标记写入
pass.ReadBuffer(cameraBuffer); // 标记读取
```

#### ColorAttachment

将一个纹理 handle 配置为颜色输出附件。支持配置 LoadAction、StoreAction 和清除颜色。

```csharp
pass.ColorAttachment(backbuffer,
    DriverLoadAction.Clear,
    DriverStoreAction.Store,
    clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));
```

一个 Pass 可以有**多个** ColorAttachment（MRT）。

#### DepthStencilAttachment

将一个纹理 handle 配置为深度模板附件。每个 Pass 最多一个深度附件。

```csharp
pass.DepthStencilAttachment(depth,
    DriverLoadAction.Clear,
    DriverStoreAction.Store,
    clearDepth: 1.0f);
```

#### ImportTexture / ImportBuffer

通过名称导入外部资源。同一名称在同一图中只导入一次（幂等）。

```csharp
var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
{
    Width = ws.Width,
    Height = ws.Height,
    Format = DriverPixelFormat.BGRA8Unorm,
    Usage = TextureUsage.RenderAttachment,
});
```

#### SetViewport / SetScissor

覆盖该 Pass 的视口和裁剪矩形。如果不设置，Pass 的 execute 回调中可以手动调用 encoder 的对应方法。

```csharp
pass.SetViewport(0, 0, 1920, 1080);
pass.SetScissor(0, 0, 1920, 1080);
```

---

## 9. ComputePassBuilder API 详解

`ComputePassBuilder` 是计算 Pass 的声明式构建 API，是 `PassBuilder` 的子集包装。

### 9.1 可用方法

```
ComputePassBuilder
  │
  ├── 资源创建（委托给 PassBuilder）
  │     ├── CreateTexture(TextureDescriptor) → RenderResourceHandle
  │     └── CreateBuffer(BufferDescriptor)   → RenderResourceHandle
  │
  ├── 依赖声明
  │     ├── ReadTexture(Handle)   → ComputePassBuilder
  │     ├── WriteTexture(Handle)  → ComputePassBuilder
  │     ├── ReadBuffer(Handle)    → ComputePassBuilder
  │     └── WriteBuffer(Handle)   → ComputePassBuilder
  │
  └── 外部资源导入
        ├── ImportTexture(name, desc) → RenderResourceHandle
        └── ImportBuffer(name, desc)  → RenderResourceHandle
```

### 9.2 与 PassBuilder 的差异

ComputePassBuilder **不支持**以下操作（计算 Pass 不需要）：

- `ColorAttachment` -- 无颜色输出
- `DepthStencilAttachment` -- 无深度测试
- `SetViewport` / `SetScissor` -- 无光栅化

ComputePassBuilder 内部持有一个 `PassBuilder` 引用，所有方法都委托给它执行，但返回类型是 `ComputePassBuilder` 以保持链式调用的流畅性。

---

## 10. 渲染图编译器

`RenderGraphCompiler` 使用 Kahn's 算法实现拓扑排序。

### 10.1 算法步骤

```
输入: List<RenderPass> passes (按用户添加顺序)

Step 1: 构建资源-写入者映射
┌──────────────────────────────────────────────────┐
│  resourceFirstWriter: Dictionary<int, int>       │
│    key:   resourceId (handle.Id)                 │
│    value: 第一个写入该资源的 pass 索引              │
│                                                  │
│  遍历所有 pass:                                   │
│    foreach resource in pass.WrittenResources:     │
│      if (resource.Id not in resourceFirstWriter) │
│        resourceFirstWriter[resource.Id] = passIdx│
└──────────────────────────────────────────────────┘

Step 2: 构建邻接表
┌──────────────────────────────────────────────────┐
│  规则: passA -> passB 如果 A 写入了 B 读取的资源   │
│                                                  │
│  遍历所有 pass:                                   │
│    foreach resourceId in pass.ReadResources:      │
│      if (resourceFirstWriter has resourceId):     │
│        writerIdx = resourceFirstWriter[resourceId]│
│        if (writerIdx != currentPassIdx):          │
│          adj[writerIdx].Add(currentPassIdx)       │
│          inDegree[currentPassIdx]++               │
└──────────────────────────────────────────────────┘

Step 3: Kahn's 拓扑排序
┌──────────────────────────────────────────────────┐
│  queue ← 所有 inDegree == 0 的 pass 索引          │
│  result ← []                                     │
│                                                  │
│  while (queue 非空):                              │
│    idx ← queue.Dequeue()                         │
│    result.Add(passes[idx])                        │
│    foreach next in adj[idx]:                      │
│      inDegree[next]--                             │
│      if (inDegree[next] == 0):                    │
│        queue.Enqueue(next)                        │
│                                                  │
│  if (result.Count != passes.Count):              │
│    throw: "RenderGraph contains a cycle."         │
└──────────────────────────────────────────────────┘
```

### 10.2 依赖图示例

假设有以下三个 Pass：

```
Pass "ShadowMap":
  Creates: shadowDepth
  Writes:  shadowDepth

Pass "ForwardOpaque":
  Reads:  shadowDepth, cameraBuffer, objectBuffer, lightBuffer
  Creates: sceneDepth
  Writes:  sceneDepth, backbuffer

Pass "PostProcess":
  Reads:  backbuffer
  Writes: backbuffer
```

生成的依赖图：

```
  ShadowMap ──writes shadowDepth──> ForwardOpaque
                                    │
                           writes backbuffer
                                    │
                                    ▼
                                PostProcess
```

拓扑排序结果：`[ShadowMap, ForwardOpaque, PostProcess]`

### 10.3 局限性

当前编译器使用 `resourceFirstWriter`（第一个写入者），这意味着：

- 如果多个 Pass 写入同一资源，只有第一个写入者被记录
- 排序只考虑"第一个写入者 -> 读取者"的边
- 后续写入者之间的顺序可能不被正确约束

这是一个简化实现，对于典型的线性渲染管线足够，但对于复杂的 DAG 结构可能需要增强。

---

## 11. 资源池

`RenderGraphResourcePool` 通过描述符精确匹配来复用 GPU 资源。

### 11.1 数据结构

```csharp
// 纹理池：精确匹配描述符
List<(TextureDescriptor Descriptor, ITexture Texture)> _texturePool;

// 缓冲区池：精确匹配描述符
List<(BufferDescriptor Descriptor, IBuffer Buffer)> _bufferPool;
```

### 11.2 工作流程

```
     GetTexture(driver, descriptor)
              │
              ├── 在 _texturePool 中查找
              │     Descriptor.Equals(descriptor)?
              │
              ├── 命中: 从池中移除并返回
              │
              └── 未命中: driver.CreateTexture(descriptor)
                       创建新的 GPU 纹理


     ReturnTexture(texture, descriptor)
              │
              └── _texturePool.Add((descriptor, texture))
                  放回池中等待复用
```

### 11.3 尺寸失效

当窗口尺寸变化时，调用 `InvalidateForSize(width, height)`：

```csharp
// 遍历池中所有纹理，释放尺寸不匹配的
for (int i = _texturePool.Count - 1; i >= 0; i--)
{
    var d = _texturePool[i].Descriptor;
    if (d.Width != width || d.Height != height)
    {
        _texturePool[i].Texture.Dispose();
        _texturePool.RemoveAt(i);
    }
}
```

缓冲区不受窗口尺寸影响，不会被失效。

### 11.4 资源描述符匹配规则

匹配使用 `Equals` 方法，要求**所有字段完全相同**：

- `TextureDescriptor`：Width、Height、Format、MipLevelCount、SampleCount、Usage
- `BufferDescriptor`：Size、Usage

---

## 12. 使用示例

### 12.1 RenderGraphBuilder + 基本使用模式

```csharp
// 创建图（当前用法：每帧创建新图）
using var builder = new RenderGraphBuilder();
using var graph = builder.GetGraph();

// 添加图形 Pass
builder.AddRenderPass("ForwardOpaque", setup: pass =>
{
    // 1. 创建瞬时资源
    var depth = pass.CreateTexture(new TextureDescriptor
    {
        Width = ws.Width,
        Height = ws.Height,
        Format = DriverPixelFormat.Depth24Plus,
        Usage = TextureUsage.RenderAttachment,
    });
    pass.WriteTexture(depth);
    pass.DepthStencilAttachment(depth,
        DriverLoadAction.Clear, DriverStoreAction.Store, clearDepth: 1.0f);

    // 2. 导入外部资源
    var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
    {
        Width = ws.Width,
        Height = ws.Height,
        Format = DriverPixelFormat.BGRA8Unorm,
        Usage = TextureUsage.RenderAttachment,
    });
    pass.WriteTexture(backbuffer);
    pass.ColorAttachment(backbuffer,
        DriverLoadAction.Clear, DriverStoreAction.Store,
        clearColor: new Vector4(0.1f, 0.1f, 0.12f, 1f));

    // 3. 声明依赖
    var cameraBuffer = pass.ImportBuffer("CameraBuffer", new BufferDescriptor
    {
        Size = scene.CameraBuffer.Size,
        Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
    });
    pass.ReadBuffer(cameraBuffer);

}, execute: ctx =>
{
    // 4. 执行绘制命令
    var encoder = ctx.Encoder;
    encoder.SetViewport(0, 0, ws.Width, ws.Height);

    for (int i = 0; i < scene.DrawCount; i++)
    {
        var draw = scene.DrawData[i];
        var mesh = context.Meshes[draw.MeshHandle];
        var material = context.Materials[draw.MaterialId];

        encoder.SetPipeline(material.Pipeline);
        encoder.SetVertexBuffer(0, mesh.VertexBuffer);
        encoder.SetIndexBuffer(mesh.IndexBuffer);
        encoder.SetBindingSet(0, driver.CreateBindingSetForPipeline(...));
        encoder.DrawIndexed((int)mesh.IndexCount);
    }
});

// 执行（内部会先编译再执行）
graph.Execute(driver);
```

### 12.2 带 Load 的 Pass（Sprite 示例）

```csharp
builder.AddRenderPass("SpritePass", setup: pass =>
{
    var backbuffer = pass.ImportTexture("Backbuffer", new TextureDescriptor
    {
        Width = windowSize.Width,
        Height = windowSize.Height,
        Format = DriverPixelFormat.BGRA8Unorm,
        Usage = TextureUsage.RenderAttachment,
    });
    pass.WriteTexture(backbuffer);
    // 使用 LoadAction.Load 保留之前 Pass 绘制的内容
    pass.ColorAttachment(backbuffer, DriverLoadAction.Load, DriverStoreAction.Store);

}, execute: ctx =>
{
    var encoder = ctx.Encoder;
    encoder.SetPipeline(context.SpritePipeline);
    encoder.SetVertexBuffer(0, context.QuadVertexBuffer);
    encoder.SetIndexBuffer(context.QuadIndexBuffer);

    for (int i = 0; i < drawCount; i++)
    {
        encoder.SetBindingSet(0, context.BindingSet, (uint)(i * UniformAlign));
        encoder.DrawIndexed(6);
    }
});
```

### 12.3 无附件 Pass

如果 Pass 没有声明 `ColorAttachment`，`BeginRenderPassForPass` 会跳过 render pass 设置。此时 Pass 可以在 execute 回调中手动管理 render pass：

```csharp
builder.AddRenderPass("CustomPass", setup: pass =>
{
    // 不声明任何附件
    // Pass 可以完全在 execute 中手动控制
}, execute: ctx =>
{
    var encoder = ctx.Encoder;
    // 手动调用 encoder.BeginRenderPass(...)
    // 绘制命令
    // encoder.EndRenderPass()
});
```

---

## 13. 系统整体架构

### 13.1 RenderingPlugin 中的系统集成

```
┌─────────────────────────────────────────────────────────────────┐
│                      RenderingPlugin                            │
│                                                                 │
│  Resources:                                                     │
│    RenderSettings, RenderContext, WindowSize, GpuSceneData      │
│                                                                 │
│  System 调度顺序:                                                │
│    KiloStage.First:    CameraSystem.Update                      │
│    KiloStage.PostUpdate: ComputeLocalToWorld                    │
│    KiloStage.PostUpdate: PrepareGpuSceneSystem.Update           │
│    KiloStage.Last:     BeginFrameSystem.Update                  │
│    KiloStage.Last:     RenderSystem.Update     ← RenderGraph    │
│    KiloStage.Last:     SpriteRenderSystem.Update ← RenderGraph  │
│    KiloStage.Last:     EndFrameSystem.Update                    │
│    KiloStage.Last:     WindowResizeSystem.Update                │
└─────────────────────────────────────────────────────────────────┘
```

### 13.2 RenderSystem 与 SpriteRenderSystem 的关系

当前架构中，两个渲染系统各自独立构建并执行自己的 RenderGraph：

```
每帧执行流程:

  RenderSystem.Update()
    │
    ├── new RenderGraphBuilder()
    ├── AddRenderPass("ForwardOpaque", ...)
    │     ├── 创建深度纹理 (Clear + Store)
    │     ├── 导入 Backbuffer (Clear + Store)
    │     └── 导入 CameraBuffer, ObjectBuffer, LightBuffer
    ├── graph.Execute(driver)
    │     └── 内部: Compile → 排序 → 创建资源 → 执行 → 清理
    └── using 释放 graph (Dispose → pool.Clear)


  SpriteRenderSystem.Update()
    │
    ├── new RenderGraphBuilder()
    ├── AddRenderPass("SpritePass", ...)
    │     ├── 导入 Backbuffer (Load + Store) ← 保留 ForwardOpaque 的结果
    │     └── 导入 SpriteUniformBuffer
    ├── graph.Execute(driver)
    │     └── 内部: Compile → 排序 → 创建资源 → 执行 → 清理
    └── using 释放 graph (Dispose → pool.Clear)
```

### 13.3 数据流图

```
┌──────────────┐    ┌──────────────┐    ┌──────────────────┐
│ ECS World     │    │ PrepareGpu   │    │ GPU Resources    │
│               │───>│ SceneSystem  │───>│                  │
│ Transform     │    │              │    │ CameraBuffer     │
│ MeshRenderer  │    │ Upload       │    │ ObjectDataBuffer │
│ Sprite        │    │ GPU data     │    │ LightBuffer      │
│ Camera        │    │ to buffers   │    │ UniformBuffer    │
└──────────────┘    └──────────────┘    └────────┬─────────┘
                                                  │
                                                  │ ImportBuffer
                                                  ▼
                                         ┌──────────────────┐
                                         │   RenderGraph     │
                                         │                   │
                                         │  ForwardOpaque    │
                                         │  ───────────────> │──> Backbuffer
                                         │  SpritePass       │    (swapchain)
                                         │  ───────────────> │
                                         └──────────────────┘
```

---

## 14. 已知问题与改进方向

### 14.1 资源池每帧失效

**问题**：当前使用模式是每帧创建新的 `RenderGraphBuilder` 并在帧结束时通过 `using` 释放。由于 `RenderGraphBuilder.Dispose()` 调用 `_graph.Dispose()`，而 `Dispose()` 调用 `_resourcePool.Clear()`，资源池中的所有 GPU 资源在每帧结束时都被销毁，池化机制实际上没有发挥作用。

```csharp
// RenderSystem.cs 中的当前模式
using var builder = new RenderGraphBuilder();   // 创建新图
using var graph = builder.GetGraph();
// ... 添加 Pass ...
graph.Execute(driver);
// ── using 退出 ──
// graph.Dispose() → _resourcePool.Clear() → 所有池中资源被释放
```

**影响**：每帧都重新创建 GPU 纹理和缓冲区，产生不必要的 GPU 分配开销。

**建议改进方案**：

- 方案 A：将 `RenderGraph` 作为类的成员变量持久化，每帧调用 `Reset()` 清除 Pass 但保留资源池
- 方案 B：将 `RenderGraphResourcePool` 提取为独立于 `RenderGraph` 的生命周期对象，在渲染系统间共享
- 方案 C：将多个渲染系统合并到同一个 RenderGraph 中，通过统一的图执行所有 Pass

### 14.2 Viewport 设置的重复

**问题**：`PassBuilder` 提供了 `SetViewport()` 方法，但当前代码在 execute 回调中手动调用 `encoder.SetViewport()`，而不是在 setup 中使用 PassBuilder 的方法。

```csharp
// 当前：在 execute 中手动设置
builder.AddRenderPass("ForwardOpaque", setup: pass => {
    // 没有使用 pass.SetViewport(...)
}, execute: ctx => {
    var encoder = ctx.Encoder;
    encoder.SetViewport(0, 0, ws.Width, ws.Height);  // 手动设置
    // ...
});

// 更好的做法：在 setup 中声明
builder.AddRenderPass("ForwardOpaque", setup: pass => {
    pass.SetViewport(0, 0, ws.Width, ws.Height);
    // ...
}, execute: ctx => {
    // Viewport 已由 RenderGraph 自动设置
});
```

### 14.3 Imported Buffer 未在 Execute 中使用

**问题**：`RenderSystem` 在 setup 中导入了 `CameraBuffer`、`ObjectDataBuffer`、`LightBuffer`，但在 execute 回调中并没有通过 `ctx.GetBuffer(handle)` 获取它们，而是直接使用 `scene.CameraBuffer` 等外部引用。导入声明的依赖关系虽然正确参与了拓扑排序，但运行时并未通过 RenderGraph 的资源解析机制来使用。

```csharp
// setup 中导入
var cameraBufferHandle = pass.ImportBuffer("CameraBuffer", ...);
pass.ReadBuffer(cameraBufferHandle);

// execute 中未使用 handle
execute: ctx => {
    // 应该是: var cam = ctx.GetBuffer(cameraBufferHandle);
    // 实际是: 直接使用 scene.CameraBuffer
    encoder.SetBindingSet(0, driver.CreateBindingSetForPipeline(
        material.Pipeline, 0, [scene.CameraBuffer]));  // 直接引用
}
```

**影响**：导入资源的声明变成了纯粹的依赖声明，失去了通过句柄间接访问资源的一致性。

**建议**：在 setup 回调中通过闭包捕获 handle，然后在 execute 中通过 `ctx.GetBuffer(handle)` 获取资源。

### 14.4 多 Pass 跨系统的资源依赖

**问题**：`RenderSystem` 和 `SpriteRenderSystem` 各自构建独立的 RenderGraph。SpritePass 的 `LoadAction.Load` 依赖于 ForwardOpaque 先绘制到 Backbuffer，但这个跨系统的依赖完全依赖系统调度顺序来保证，而非由 RenderGraph 的依赖分析来管理。

**建议**：将所有渲染 Pass 合并到同一个 `RenderGraph` 实例中，让编译器统一管理所有 Pass 的执行顺序和资源依赖。

### 14.5 编译器的 first-writer 局限

**问题**：`RenderGraphCompiler` 使用 `resourceFirstWriter` 映射，只记录每个资源的第一个写入者。如果多个 Pass 写入同一资源，只有第一个写入者被用于构建依赖边。

**示例**：

```
Pass A: writes resource X
Pass B: writes resource X  (不会被记录)
Pass C: reads  resource X

// 只有 A → C 的边，缺少 B → C 的约束
```

**建议**：改为记录所有写入者，或者使用更完善的屏障（barrier）系统。

### 14.6 BindingSet 每帧重建

**问题**：在 `RenderSystem` 的 execute 回调中，每个绘制调用都通过 `driver.CreateBindingSetForPipeline(...)` 创建新的 `IBindingSet`，这会产生大量每帧 GPU 对象分配。

```csharp
// RenderSystem.cs execute 回调中
for (int i = 0; i < scene.DrawCount; i++)
{
    // 每帧每个物体都创建新的 BindingSet
    var freshCameraSet = driver.CreateBindingSetForPipeline(
        material.Pipeline, 0, [scene.CameraBuffer]);
    // ...
}
```

**建议**：缓存 BindingSet，只在资源变化时重建。

### 14.7 Compute Pass 支持不完整

**问题**：虽然 `ComputePassBuilder` 和 `IRenderCommandEncoder` 提供了 compute pass 的基础支持（`BeginComputePass`/`EndComputePass`/`Dispatch`），但目前没有实际使用计算 Pass 的场景。

---

## 附录 A：RenderGraph 类完整字段一览

```csharp
public sealed class RenderGraph : IDisposable
{
    // Pass 管理
    private readonly List<RenderPass> _passes;

    // 资源注册表
    private readonly Dictionary<int, object> _resourceDescriptors;   // handleId -> descriptor
    private readonly Dictionary<int, object> _resolvedResources;     // handleId -> GPU resource
    private readonly Dictionary<string, RenderResourceHandle> _importedResources;  // name -> handle

    // TextureView 缓存
    private readonly Dictionary<int, ITextureView> _textureViews;    // handleId -> view

    // 资源池
    private readonly RenderGraphResourcePool _resourcePool;

    // Handle 分配
    private int _nextHandleId;

    // 增量编译
    private int _structureVersion;
    private int _lastCompiledStructureVersion;
    private bool _isCompiled;

    // 窗口尺寸追踪
    private int _lastWindowWidth;
    private int _lastWindowHeight;
}
```

## 附录 B：Driver 接口依赖

RenderGraph 依赖 `IRenderDriver` 的以下方法：

| 方法 | 用途 |
|------|------|
| `GetCurrentSwapchainTexture()` | 获取 Backbuffer 和检测尺寸变化 |
| `CreateTexture(descriptor)` | 通过池创建纹理 |
| `CreateTextureView(texture, descriptor)` | 为附件创建纹理视图 |
| `CreateBuffer(descriptor)` | 通过池创建缓冲区 |
| `BeginCommandEncoding()` | 创建命令编码器 |

依赖 `IRenderCommandEncoder` 的以下方法：

| 方法 | 用途 |
|------|------|
| `SetViewport(...)` | 设置 Pass 视口 |
| `SetScissor(...)` | 设置 Pass 裁剪矩形 |
| `BeginRenderPass(descriptor)` | 开始渲染 Pass |
| `EndRenderPass()` | 结束渲染 Pass |
| `BeginComputePass()` | 开始计算 Pass |
| `EndComputePass()` | 结束计算 Pass |
| `Submit()` | 提交命令缓冲区 |
