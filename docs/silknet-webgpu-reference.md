# Kilo 引擎 Silk.NET WebGPU 驱动参考文档

本文档是 Kilo 引擎项目中 WebGPU 渲染后端的完整技术参考。涵盖驱动架构、资源管理、命令录制、GPU 数据布局、着色器管线以及所有枚举映射的详细信息。

---

## 目录

1. [项目结构](#1-项目结构)
2. [驱动架构概览](#2-驱动架构概览)
3. [初始化流程 (WebGPUDriverFactory)](#3-初始化流程-webgpudriverfactory)
4. [核心驱动 (WebGPURenderDriver)](#4-核心驱动-webgpurenderdriver)
   - 4.1 [资源创建](#41-资源创建)
   - 4.2 [管线创建](#42-管线创建)
   - 4.3 [绑定集创建](#43-绑定集创建)
   - 4.4 [帧生命周期](#44-帧生命周期)
   - 4.5 [枚举映射表](#45-枚举映射表)
5. [命令编码器 (WebGPUCommandEncoder)](#5-命令编码器-webgpucommandencoder)
6. [GPU 数据布局](#6-gpu-数据布局)
7. [着色器管线 (WGSL)](#7-着色器管线-wgsl)
8. [内存管理模式](#8-内存管理模式)
9. [WebGPU 特定约束与注意事项](#9-webgpu-特定约束与注意事项)

---

## 1. 项目结构

```
src/Kilo.Rendering/
├── Driver/
│   ├── IRenderDriver.cs            # 渲染驱动抽象接口
│   ├── ICommandEncoder.cs          # 命令编码器抽象接口
│   └── WebGPU/                     # WebGPU 驱动实现
│       ├── WebGPUDriverFactory.cs  # 驱动工厂，负责初始化
│       ├── WebGPURenderDriver.cs   # 核心驱动，资源与管线创建
│       ├── WebGPUCommandEncoder.cs # 命令录制
│       ├── WebGPUTexture.cs        # 纹理封装
│       ├── WebGPUTextureView.cs    # 纹理视图封装
│       ├── WebGPUSampler.cs        # 采样器封装
│       ├── WebGPUBuffer.cs         # 缓冲区封装
│       ├── WebGPUShaderModule.cs   # 着色器模块封装
│       └── WebGPUBindingSet.cs     # 绑定集封装
├── Shaders/
│   ├── BasicLitShaders.cs         # 基础光照着色器 (WGSL)
│   └── SpriteShaders.cs           # 精灵着色器 (WGSL)
└── RenderingPlugin.cs             # 插件入口，初始化所有资源
```

---

## 2. 驱动架构概览

Kilo 引擎的渲染系统采用**抽象驱动接口 + 具体后端实现**的架构：

- `IRenderDriver` / `ICommandEncoder`：平台无关的抽象接口，定义了资源创建、管线构建、命令录制等操作
- `WebGPURenderDriver` / `WebGPUCommandEncoder`：基于 Silk.NET WebGPU 绑定的具体实现
- 所有原生 WebGPU 对象（Texture、Buffer、Pipeline 等）被封装在实现了 `IDisposable` 的安全包装类中

核心设计原则：

- **安全封装**：原生指针不暴露给上层，所有操作通过托管类进行
- **统一生命周期**：所有 GPU 资源实现 `IDisposable`，通过 `using` 模式管理
- **动态偏移支持**：通过 `HasDynamicOffset = true` 实现实例化渲染的 uniform buffer 复用

---

## 3. 初始化流程 (WebGPUDriverFactory)

`WebGPUDriverFactory` 负责完整的 WebGPU 初始化链路。以下是逐步说明：

### 3.1 创建 API 实例

```csharp
var wgpu = WgpuApi.GetApi(); // 获取 Silk.NET WebGPU C API 绑定
```

`WgpuApi` 是 Silk.NET 对 WebGPU C API 的完整绑定，包含所有 `wgpu*` 函数。

### 3.2 创建 Instance

```csharp
Instance instance;
wgpu.CreateInstance(null, &instance); // 使用默认描述符创建实例
```

`Instance` 是 WebGPU 的顶层对象，代表一个 GPU 连接会话。

### 3.3 创建 Surface

```csharp
Surface surface;
window.CreateWebGPUSurface(wgpu, instance, &surface);
```

通过 Silk.NET 窗口的原生方法直接从窗口句柄创建 WebGPU Surface。这是渲染输出的目标。

### 3.4 请求 Adapter

```csharp
// 使用异步回调模式请求 Adapter
wgpu.InstanceRequestAdapter(instance, new RequestAdapterOptions
{
    CompatibleSurface = surface,
    PowerPreference = PowerPreference.HighPerformance
}, (status, adapter, message, userdata) =>
{
    // 回调中获取 adapter
}, null);
```

关键要点：
- `CompatibleSurface` 确保适配器支持当前 Surface
- `PowerPreference.HighPerformance` 优先选择独显
- WebGPU 的异步操作采用回调模式，不是 async/await

### 3.5 获取 Surface 格式

```csharp
wgpu.SurfaceGetCapabilities(surface, adapter, out var capabilities);
var format = capabilities.Formats[0]; // 通常为 Bgra8Unorm
```

获取 Surface 支持的格式列表，取第一个作为渲染格式（通常为 `Bgra8Unorm`）。

### 3.6 请求 Device

```csharp
wgpu.AdapterRequestDevice(adapter, null, (status, device, message, userdata) =>
{
    // 回调中获取 device
}, null);
```

Device 是 WebGPU 中资源创建和命令提交的核心对象。

### 3.7 设置错误回调

```csharp
wgpu.DeviceSetUncapturedErrorCallback(device, (type, message, userdata) =>
{
    Console.WriteLine($"[WebGPU Error] {type}: {message}");
}, null);
```

`UncapturedError` 回调用于捕获未处理的 GPU 错误，是调试的重要手段。

### 3.8 配置 Surface

```csharp
wgpu.SurfaceConfigure(surface, new SurfaceConfiguration
{
    Device = device,
    Format = format,
    Usage = TextureUsage.RenderAttachment,
    PresentMode = PresentMode.Fifo, // VSync
    Width = width,
    Height = height
});
```

- `PresentMode.Fifo`：垂直同步模式，帧率不超过显示器刷新率
- `TextureUsage.RenderAttachment`：Surface 纹理用作渲染附件

### 3.9 创建 RenderDriver

```csharp
var driver = new WebGPURenderDriver(wgpu, device, surface, format);
```

将所有初始化获得的核心对象传入 `WebGPURenderDriver`。

---

## 4. 核心驱动 (WebGPURenderDriver)

`WebGPURenderDriver` 是 WebGPU 后端的核心类，负责所有 GPU 资源的创建和管理。

### 4.1 资源创建

#### 4.1.1 纹理 (Texture)

```csharp
ITexture CreateTexture(uint width, uint height, DriverPixelFormat format,
    TextureUsageFlags usage, string label = "")
```

底层调用链：

```
CreateTextureDescriptor (size, format, usage, label)
    → wgpu.DeviceCreateTexture(device, &descriptor)
    → 封装为 WebGPUTexture
```

关键参数：
- `width` / `height`：纹理尺寸
- `format`：像素格式（通过枚举映射转换为 WebGPU `TextureFormat`）
- `usage`：`TextureUsage` 标志的组合（如 `TextureUsage.TextureBinding | TextureUsage.CopyDst`）

#### 4.1.2 纹理视图 (TextureView)

```csharp
ITextureView CreateTextureView(ITexture texture, string label = "")
```

纹理视图是纹理的"窗口"，定义了如何访问纹理数据：

```
CreateTextureViewDescriptor (format, dimension=2D, mipLevelCount=1)
    → wgpu.TextureCreateView(nativeTexture, &descriptor)
    → 封装为 WebGPUTextureView
```

#### 4.1.3 采样器 (Sampler)

```csharp
ISampler CreateSampler(FilterMode magFilter, FilterMode minFilter,
    AddressMode addressModeU, AddressMode addressModeV)
```

采样器定义了纹理的过滤和寻址模式：

```
CreateSamplerDescriptor (magFilter, minFilter, addressModeU, addressModeV)
    → wgpu.DeviceCreateSampler(device, &descriptor)
    → 封装为 WebGPUSampler
```

#### 4.1.4 缓冲区 (Buffer)

```csharp
IBuffer CreateBuffer(ulong size, BufferUsageFlags usage, string label = "")
```

缓冲区是 GPU 内存的基本单元：

```
CreateBufferDescriptor (size, usage, mappedAtCreation=false)
    → wgpu.DeviceCreateBuffer(device, &descriptor)
    → 封装为 WebGPUBuffer
```

常见用法：
- `BufferUsage.Uniform`：Uniform 缓冲区（传递常量数据给着色器）
- `BufferUsage.Vertex`：顶点缓冲区
- `BufferUsage.Index`：索引缓冲区
- `BufferUsage.CopyDst`：允许从 CPU 复制数据到缓冲区

数据上传：

```csharp
wgpu.QueueWriteBuffer(queue, nativeBuffer, offset, dataPtr, dataSize);
```

#### 4.1.5 着色器模块 (ShaderModule)

```csharp
IShaderModule CreateShaderModule(string wgslSource, string label = "")
```

从 WGSL 源代码创建着色器模块：

```
ShaderModuleWGSLDescriptor { code = wgslSource }
    → 封装为 ShaderModuleDescriptor 的 nextInChain
    → wgpu.DeviceCreateShaderModule(device, &descriptor)
    → 封装为 WebGPUShaderModule
```

注意：Silk.NET 使用 `nextInChain` 链式结构来传递 WGSL 描述符。

#### 4.1.6 计算着色器模块

```csharp
IComputeShaderModule CreateComputeShaderModule(string wgslSource, string label = "")
```

与普通着色器模块的创建过程相同，区别在于返回类型为 `IComputeShaderModule` 接口。

### 4.2 管线创建

#### 4.2.1 普通渲染管线

```csharp
IRenderPipeline CreateRenderPipeline(RenderPipelineDescriptor descriptor)
```

创建步骤：

1. **构建原生描述符** (`RenderPipelineDescriptor`):
   - Vertex stage（顶点着色器模块 + 入口函数）
   - Fragment stage（片段着色器模块 + 入口函数 + 目标格式）
   - Vertex buffer layouts（顶点缓冲区布局）
   - Primitive topology（图元拓扑）
   - Blend state（混合状态）
   - Depth stencil state（深度模板状态）

2. **调用 API**:
   ```
   wgpu.DeviceCreateRenderPipeline(device, &nativeDescriptor)
       → 封装为 WebGPURenderPipeline
   ```

3. **清理临时原生内存**

#### 4.2.2 动态 Uniform 管线（两阶段创建）

```csharp
IRenderPipeline CreateRenderPipelineWithDynamicUniforms(
    RenderPipelineDescriptor descriptor,
    uint dynamicBindGroupIndex,
    uint dynamicBindingIndex)
```

这是 Kilo 引擎实现实例化渲染的关键方法，采用**两阶段创建**策略：

**第一阶段 — 推断管线布局：**

```
1. 使用默认自动推断布局创建临时 RenderPipeline
2. 从临时管线获取 BindGroupLayout:
   wgpu.RenderPipelineGetBindGroupLayout(tempPipeline, dynamicBindGroupIndex)
3. 从 BindGroupLayout 获取推断出的 Binding 条目
4. 释放临时管线
```

**第二阶段 — 构建显式管线布局：**

```
1. 创建新的 BindGroupLayout，将指定 binding 的
   HasDynamicOffset 设置为 true
2. 创建显式 PipelineLayout:
   wgpu.DeviceCreatePipelineLayout(device, &layoutDescriptor)
3. 使用显式 PipelineLayout 创建最终 RenderPipeline
```

这种模式的作用：
- 允许在同一个 Uniform Buffer 中存储多个对象的数据
- 通过**动态偏移**（dynamic offset）在绘制调用之间切换不同对象的数据区域
- 避免为每个实例创建单独的 Bind Group

### 4.3 绑定集创建

绑定集（Bind Group）是将资源绑定到着色器的核心机制。

#### 4.3.1 通用绑定集

```csharp
IBindingSet CreateBindingSet(BindingSetDescriptor descriptor)
```

流程：
1. 为每个 Binding 条目分配原生内存（`NativeMemory.Alloc`）
2. 填充 `BindGroupLayoutEntry` 和 `BindGroupEntry` 结构体
3. 创建 `BindGroupLayout`：
   ```
   wgpu.DeviceCreateBindGroupLayout(device, &layoutDescriptor)
   ```
4. 创建 `BindGroup`：
   ```
   wgpu.DeviceCreateBindGroup(device, &bindGroupDescriptor)
   ```
5. 释放临时原生内存

支持的绑定类型：
- `Buffer`：Uniform / Storage 缓冲区
- `Texture`：纹理资源
- `Sampler`：采样器

#### 4.3.2 基于管线的绑定集

```csharp
IBindingSet CreateBindingSetForPipeline(
    IRenderPipeline pipeline,
    uint groupIndex,
    IBuffer[] buffers,
    ITextureView[] textureViews,
    ISampler[] samplers)
```

从已有管线获取 Bind Group Layout，然后创建 Bind Group。这种方式确保绑定集的布局与管线完全兼容。

```
wgpu.RenderPipelineGetBindGroupLayout(pipeline, groupIndex)
    → 使用获取到的 layout 创建 BindGroup
```

#### 4.3.3 动态 Uniform 绑定集

```csharp
IBindingSet CreateDynamicUniformBindingSet(
    IBuffer buffer,
    ulong bufferSize,
    uint bindGroupIndex,
    uint bindingIndex)
```

专为动态偏移 uniform 缓冲区设计，设置 `HasDynamicOffset = true`。

### 4.4 帧生命周期

每一帧的渲染遵循以下流程：

```
BeginFrame()
    → GetCurrentSwapchainTexture()
    → BeginCommandEncoding()
    → [录制渲染命令]
    → Submit()
    → Present()
```

#### BeginFrame

```csharp
void BeginFrame()
{
    _currentFrameTexture = null; // 重置当前帧纹理引用
}
```

#### GetCurrentSwapchainTexture

```csharp
ITexture GetCurrentSwapchainTexture()
{
    var status = wgpu.SurfaceGetCurrentTexture(surface, out var surfaceTexture);
    // 检查状态，必要时重新配置 Surface
    _currentFrameTexture = new WebGPUTexture(wgpu, surfaceTexture.Texture);
    return _currentFrameTexture;
}
```

可能的状态：
- `Success`：正常获取
- `Timeout` / `Outdated` / `Lost`：需要重新配置 Surface

#### BeginCommandEncoding

```csharp
ICommandEncoder BeginCommandEncoding()
{
    var encoder = wgpu.DeviceCreateCommandEncoder(device, null);
    return new WebGPUCommandEncoder(wgpu, encoder);
}
```

#### Present

```csharp
void Present()
{
    wgpu.SurfacePresent(surface);
    _currentFrameTexture?.Dispose();
    _currentFrameTexture = null;
}
```

### 4.5 枚举映射表

所有 Kilo 引擎的抽象枚举到 Silk.NET WebGPU 枚举的完整映射。

#### 像素格式 (DriverPixelFormat → TextureFormat)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `Bgra8Unorm` | `TextureFormat.Bgra8Unorm` | 8位 BGRA 标准化无符号整数 |
| `Rgba8Unorm` | `TextureFormat.Rgba8Unorm` | 8位 RGBA 标准化无符号整数 |
| `Rgba8Snorm` | `TextureFormat.Rgba8Snorm` | 8位 RGBA 标准化有符号整数 |
| `Rgba8Uint` | `TextureFormat.Rgba8Uint` | 8位 RGBA 无符号整数 |
| `Rgba8Sint` | `TextureFormat.Rgba8Sint` | 8位 RGBA 有符号整数 |
| `Depth24Plus` | `TextureFormat.Depth24Plus` | 24位深度 |
| `Depth32Float` | `TextureFormat.Depth32Float` | 32位浮点深度 |
| `Depth24PlusStencil8` | `TextureFormat.Depth24PlusStencil8` | 24位深度 + 8位模板 |

#### 混合因子 (DriverBlendFactor → BlendFactor)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `Zero` | `BlendFactor.Zero` | 因子 = 0 |
| `One` | `BlendFactor.One` | 因子 = 1 |
| `SrcAlpha` | `BlendFactor.SrcAlpha` | 因子 = 源 alpha |
| `OneMinusSrcAlpha` | `BlendFactor.OneMinusSrcAlpha` | 因子 = 1 - 源 alpha |
| `DstAlpha` | `BlendFactor.DstAlpha` | 因子 = 目标 alpha |
| `OneMinusDstAlpha` | `BlendFactor.OneMinusDstAlpha` | 因子 = 1 - 目标 alpha |

#### 图元拓扑 (DriverPrimitiveTopology → PrimitiveTopology)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `TriangleList` | `PrimitiveTopology.TriangleList` | 三角形列表 |
| `TriangleStrip` | `PrimitiveTopology.TriangleStrip` | 三角形条带 |
| `LineList` | `PrimitiveTopology.LineList` | 线段列表 |
| `LineStrip` | `PrimitiveTopology.LineStrip` | 线段条带 |
| `PointList` | `PrimitiveTopology.PointList` | 点列表 |

#### 顶点格式 (VertexFormat → WebGPU VertexFormat)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `Float32x3` | `VertexFormat.Float32x3` | 3 个 float32 |
| `Float32x2` | `VertexFormat.Float32x2` | 2 个 float32 |
| `Float32x4` | `VertexFormat.Float32x4` | 4 个 float32 |
| `Float32` | `VertexFormat.Float32` | 1 个 float32 |
| `Uint8x4` | `VertexFormat.Uint8x4` | 4 个 uint8 |
| `Sint32` | `VertexFormat.Sint32` | 1 个 sint32 |

#### 比较函数 (DriverCompareFunction → CompareFunction)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `Never` | `CompareFunction.Never` | 永远不通过 |
| `Less` | `CompareFunction.Less` | 小于时通过 |
| `Equal` | `CompareFunction.Equal` | 等于时通过 |
| `LessEqual` | `CompareFunction.LessEqual` | 小于等于时通过 |
| `Greater` | `CompareFunction.Greater` | 大于时通过 |
| `NotEqual` | `CompareFunction.NotEqual` | 不等于时通过 |
| `GreaterEqual` | `CompareFunction.GreaterEqual` | 大于等于时通过 |
| `Always` | `CompareFunction.Always` | 总是通过 |

#### 加载操作 (DriverLoadAction → LoadOp)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `Load` | `LoadOp.Load` | 保留上一帧内容 |
| `Clear` | `LoadOp.Clear` | 清除为指定颜色 |
| `DontCare` | `LoadOp.Undefined` | 不关心初始内容 |

#### 存储操作 (DriverStoreAction → StoreOp)

| Kilo 枚举 | WebGPU 枚举 | 说明 |
|---|---|---|
| `Store` | `StoreOp.Store` | 存储渲染结果 |
| `Discard` | `StoreOp.Discard` | 丢弃渲染结果 |
| `DontCare` | `StoreOp.Undefined` | 不关心存储结果 |

---

## 5. 命令编码器 (WebGPUCommandEncoder)

`WebGPUCommandEncoder` 封装了 WebGPU 的命令录制流程，管理 `CommandEncoder*` 和 `RenderPassEncoder*` 的生命周期。

### 5.1 渲染通道 (Render Pass)

#### 传统方式

```csharp
void BeginRenderPass(ITexture colorAttachment, DriverPixelFormat format,
    float[] clearColor)
```

创建颜色附件的 TextureView，构建 `RenderPassColorAttachment`，开始渲染通道。

```
1. wgpu.TextureCreateView(colorAttachment, null) → colorView
2. 构建 RenderPassColorAttachment { view, loadOp=Clear, storeOp=Store, clearColor }
3. 构建 RenderPassDescriptor { colorAttachmentCount=1, colorAttachments }
4. wgpu.CommandEncoderBeginRenderPass(encoder, &descriptor) → renderPassEncoder
```

#### 完整描述符方式

```csharp
void BeginRenderPass(RenderPassDescriptor descriptor)
```

支持颜色附件 + 深度附件的完整描述：

```
1. 为每个颜色附件创建 TextureView
2. 如果有深度附件，创建深度 TextureView
3. 构建完整的 RenderPassDescriptor
4. wgpu.CommandEncoderBeginRenderPass(encoder, &descriptor)
```

### 5.2 渲染命令

#### 设置管线

```csharp
void SetPipeline(IRenderPipeline pipeline)
```

```
wgpu.RenderPassEncoderSetPipeline(renderPassEncoder, nativePipeline)
```

#### 设置顶点缓冲区

```csharp
void SetVertexBuffer(uint slot, IBuffer buffer)
```

```
wgpu.RenderPassEncoderSetVertexBuffer(renderPassEncoder, slot, nativeBuffer, 0, ulong.MaxValue)
```

注意：`offset = 0`，`size = ulong.MaxValue` 表示绑定整个缓冲区。

#### 设置索引缓冲区

```csharp
void SetIndexBuffer(IBuffer buffer)
```

```
wgpu.RenderPassEncoderSetIndexBuffer(renderPassEncoder, nativeBuffer, IndexFormat.Uint32, 0, ulong.MaxValue)
```

索引格式固定为 `Uint32`（32位无符号整数）。

#### 设置绑定集

```csharp
void SetBindingSet(uint groupIndex, IBindingSet bindingSet, uint[] dynamicOffsets)
```

```
// 如果有动态偏移:
wgpu.RenderPassEncoderSetBindGroup(renderPassEncoder, groupIndex, nativeBindGroup,
    (uint)dynamicOffsets.Length, dynamicOffsetsPtr)

// 如果没有动态偏移:
wgpu.RenderPassEncoderSetBindGroup(renderPassEncoder, groupIndex, nativeBindGroup,
    0, null)
```

动态偏移数组中的值是 Uniform Buffer 内的偏移量（必须为 256 的倍数）。

#### 绘制

```csharp
void DrawIndexed(uint indexCount, uint instanceCount = 1,
    uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
```

```
wgpu.RenderPassEncoderDrawIndexed(renderPassEncoder,
    indexCount, instanceCount, firstIndex, baseVertex, firstInstance)
```

#### 结束渲染通道

```csharp
void EndRenderPass()
```

```
wgpu.RenderPassEncoderEnd(renderPassEncoder)
renderPassEncoder = null
```

### 5.3 提交命令

```csharp
void Submit()
```

```
1. wgpu.CommandEncoderFinish(encoder, null) → commandBuffer
2. wgpu.QueueSubmit(queue, 1, &commandBuffer)
3. 释放 commandBuffer
```

WebGPU 的 CommandBuffer 是**一次性**的——创建、提交、释放。

### 5.4 完整渲染流程示例

```csharp
// 开始帧
driver.BeginFrame();
var swapchainTexture = driver.GetCurrentSwapchainTexture();

// 创建命令编码器
var encoder = driver.BeginCommandEncoding();

// 开始渲染通道
encoder.BeginRenderPass(swapchainTexture, format, new float[] { 0, 0, 0, 1 });

// 设置管线和资源
encoder.SetPipeline(pipeline);
encoder.SetVertexBuffer(0, vertexBuffer);
encoder.SetIndexBuffer(indexBuffer);
encoder.SetBindingSet(0, cameraBindSet);
encoder.SetBindingSet(1, objectBindSet, new uint[] { objectOffset });
encoder.SetBindingSet(2, lightBindSet);

// 绘制
encoder.DrawIndexed(indexCount);

// 结束渲染通道
encoder.EndRenderPass();

// 提交命令
encoder.Submit();

// 呈现
driver.Present();
```

---

## 6. GPU 数据布局

所有传递到 GPU 的数据结构必须遵循 WebGPU 的对齐规则。Kilo 引擎中最重要的约束是 **minUniformBufferOffsetAlignment = 256**，即所有 Uniform 结构体的大小必须是 256 字节的倍数。

### 6.1 CameraData (256 字节)

摄像机数据，每个摄像机一份，绑定到 Group 0, Binding 0。

```
偏移量    大小      字段             类型
──────────────────────────────────────────────
0         64        View            mat4x4<f32>
64        64        Projection      mat4x4<f32>
128       12        Position        vec3<f32>
140       4         LightCount      i32
144       112       (padding)       —
──────────────────────────────────────────────
总计: 256 字节
```

内存布局说明：
- `mat4x4<f32>` = 4x4 个 float32 = 64 字节
- `vec3<f32>` = 3 个 float32 = 12 字节
- `Position` 后面紧跟 `LightCount`，之间有 4 字节自然对齐
- 尾部填充到 256 字节以满足动态偏移对齐要求

C# 端数据结构建议：

```csharp
[StructLayout(LayoutKind.Sequential, Size = 256)]
struct CameraData
{
    public Matrix4x4 View;         // offset 0
    public Matrix4x4 Projection;   // offset 64
    public Vector3 Position;       // offset 128
    public int LightCount;         // offset 140
    // 112 bytes padding            offset 144
}
```

### 6.2 ObjectData (256 字节)

每个对象的数据，通过动态偏移在同一个 Uniform Buffer 中寻址。绑定到 Group 1, Binding 0。

```
偏移量    大小      字段             类型
──────────────────────────────────────────────
0         64        Model           mat4x4<f32>
64        4         MaterialId      i32
68        188       (padding)       —
──────────────────────────────────────────────
总计: 256 字节
```

动态偏移机制：
- 所有 ObjectData 存储在同一个 Uniform Buffer 中
- 每个对象占据 256 字节（即使实际数据只有 68 字节）
- 绘制时通过 `SetBindingSet(groupIndex, bindingSet, new uint[] { offset * 256 })` 指定偏移

```csharp
// 绘制第 3 个对象（偏移 = 3 * 256 = 768）
encoder.SetBindingSet(1, objectBindSet, new uint[] { 768 });
encoder.DrawIndexed(indexCount);

// 绘制第 5 个对象（偏移 = 5 * 256 = 1280）
encoder.SetBindingSet(1, objectBindSet, new uint[] { 1280 });
encoder.DrawIndexed(indexCount);
```

### 6.3 LightData (32 字节)

光源数据，存储在固定大小的数组中。绑定到 Group 2, Binding 0。

```
偏移量    大小      字段                   类型
──────────────────────────────────────────────
0         12        DirectionOrPosition    vec3<f32>
12        4         (padding)              —
16        12        Color                  vec3<f32>
28        4         Intensity              f32
32        4         Range                  f32
36        4         LightType              i32
40        8         (padding)              —
──────────────────────────────────────────────
总计: 48 字节（单个光源）
```

由于 LightData 存储在数组中，整个数组的 Uniform Buffer 大小为：

```
bufferSize = maxLightCount * 48  // 需根据实际对齐调整
```

**注意**：WebGPU 要求 Uniform Buffer 中结构体数组的每个元素大小必须是 16 字节对齐。因此实际的单个 LightData 大小可能需要调整到 48 或 64 字节的倍数，具体取决于 `minUniformBufferOffsetAlignment` 和 `minStorageBufferOffsetAlignment` 的值。在实际使用中应通过 `wgpu.DeviceGetLimits` 查询并验证。

---

## 7. 着色器管线 (WGSL)

Kilo 引擎使用 WGSL（WebGPU Shading Language）编写着色器。着色器源码以 C# 字符串常量的形式内嵌在 `src/Kilo.Rendering/Shaders/` 目录下的类中。

### 7.1 BasicLitShaders — 基础光照着色器

#### 绑定布局

```
Group 0 (视图/摄像机):
  - Binding 0: CameraData uniform — 摄像机矩阵和参数

Group 1 (每对象):
  - Binding 0: ObjectData uniform — 模型矩阵和材质 ID（动态偏移）

Group 2 (光照):
  - Binding 0: LightData array uniform — 光源数组
```

#### 顶点着色器

```wgsl
// 顶点输入
struct VertexInput {
    @location(0) position: vec3<f32>,  // 顶点位置
    @location(1) normal: vec3<f32>,    // 顶点法线
}

// 顶点输出
struct VertexOutput {
    @builtin(position) clipPosition: vec4<f32>,
    @location(0) worldNormal: vec3<f32>,
}

@group(0) @binding(0) var<uniform> camera: CameraData;
@group(1) @binding(0) var<uniform> object: ObjectData;

@vertex
fn vertexMain(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    let worldPosition = object.model * vec4<f32>(input.position, 1.0);
    output.clipPosition = camera.projection * camera.view * worldPosition;
    output.worldNormal = (object.model * vec4<f32>(input.normal, 0.0)).xyz;
    return output;
}
```

#### 片段着色器（当前实现）

当前片段着色器输出**法线可视化**，用于调试：

```wgsl
@fragment
fn fragmentMain(input: VertexOutput) -> @location(0) vec4<f32> {
    // 将法线从 [-1, 1] 映射到 [0, 1] 用于可视化
    return vec4<f32>(input.worldNormal * 0.5 + 0.5, 1.0);
}
```

法线可视化效果：
- X 轴正方向 → 红色 (1, 0, 0)
- Y 轴正方向 → 绿色 (0, 1, 0)
- Z 轴正方向 → 蓝色 (0, 0, 1)

### 7.2 SpriteShaders — 精灵着色器

#### 绑定布局

```
Group 0:
  - Binding 0: SpriteData uniform — 模型矩阵、投影矩阵、颜色（动态偏移）
```

#### 顶点输入

```wgsl
struct VertexInput {
    @location(0) position: vec2<f32>,  // 二维顶点位置
}
```

#### 特点

- 简单的二维彩色四边形渲染
- 每个精灵有独立的模型矩阵和颜色
- 使用动态偏移实现批量渲染

---

## 8. 内存管理模式

Kilo 引擎的 WebGPU 驱动采用了严格的内存管理策略，确保原生资源不泄漏。

### 8.1 安全封装模式

所有原生 WebGPU 对象都遵循以下封装模式：

```csharp
public class WebGPUTexture : ITexture, IDisposable
{
    private readonly WgpuApi _wgpu;
    private Texture* _nativeTexture;
    private bool _disposed;

    internal WebGPUTexture(WgpuApi wgpu, Texture* nativeTexture)
    {
        _wgpu = wgpu;
        _nativeTexture = nativeTexture;
    }

    internal Texture* NativePtr => _nativeTexture;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_nativeTexture != null)
        {
            _wgpu.TextureRelease(_nativeTexture);
            _nativeTexture = null;
        }
    }
}
```

关键点：
- 原生指针存储为私有字段，不对外暴露
- 通过 `NativePtr` 属性在驱动内部安全访问
- `Dispose` 模式确保资源只释放一次
- 所有 WebGPU 对象都通过对应的 `Release` 函数释放

### 8.2 临时原生内存管理

在创建管线和绑定集时，需要临时分配非托管内存来构建描述符。模式如下：

```csharp
// 分配
var layoutEntries = (BindGroupLayoutEntry*)NativeMemory.Alloc(
    count * (nuint)sizeof(BindGroupLayoutEntry));

try
{
    // 填充结构体...
    // 调用 WebGPU API...
}
finally
{
    // 确保释放
    NativeMemory.Free(layoutEntries);
}
```

### 8.3 字符串封送

WebGPU 描述符中的标签字符串需要封送为原生指针：

```csharp
var labelPtr = SilkMarshal.StringToPtr(label);
try
{
    descriptor.Label = labelPtr;
    // 调用 API...
}
finally
{
    SilkMarshal.Free(labelPtr);
}
```

### 8.4 管线创建的内存生命周期

管线创建过程中的内存生命周期尤为复杂，因为 `RenderPipelineDescriptor` 包含嵌套的指针结构：

```
RenderPipelineDescriptor
├── Vertex.Stage.Module → 指向 ShaderModule（由包装类持有）
├── Vertex.Buffers → 指向堆分配的 VertexBufferLayout 数组
│   ├── Attributes → 指向堆分配的 VertexAttribute 数组
├── Fragment.Stage.Module → 指向 ShaderModule
├── Fragment.Targets → 指向堆分配的 ColorTargetState 数组
├── DepthStencil → 指向栈分配或堆分配的 DepthStencilState
└── PrimitiveState → 内联结构体
```

所有堆分配的子结构在管线创建完成后立即释放，因为 WebGPU 会在 `CreateRenderPipeline` 调用期间复制所有必要数据。

### 8.5 资源释放规则

| 资源类型 | 释放函数 | 调用时机 |
|---|---|---|
| Texture | `wgpu.TextureRelease` | Dispose |
| TextureView | `wgpu.TextureViewRelease` | Dispose |
| Sampler | `wgpu.SamplerRelease` | Dispose |
| Buffer | `wgpu.BufferRelease` | Dispose |
| ShaderModule | `wgpu.ShaderModuleRelease` | Dispose |
| RenderPipeline | `wgpu.RenderPipelineRelease` | Dispose |
| BindGroup | `wgpu.BindGroupRelease` | Dispose |
| BindGroupLayout | `wgpu.BindGroupLayoutRelease` | Dispose |
| PipelineLayout | `wgpu.PipelineLayoutRelease` | Dispose |
| CommandBuffer | `wgpu.CommandBufferRelease` | Submit 后立即释放 |
| SurfaceTexture | 不需要单独释放 | 由 Present 管理 |

---

## 9. WebGPU 特定约束与注意事项

### 9.1 Uniform Buffer 对齐

WebGPU 规范要求 `minUniformBufferOffsetAlignment` 至少为 256 字节。Kilo 引擎统一使用 256 字节作为所有 Uniform 结构体的对齐粒度。

影响：
- `CameraData`：实际数据 144 字节，填充到 256 字节
- `ObjectData`：实际数据 68 字节，填充到 256 字节
- 动态偏移必须是 256 的倍数

建议在实际初始化时查询设备限制：

```csharp
wgpu.DeviceGetLimits(device, out var limits);
var minAlignment = limits.Limits.MinUniformBufferOffsetAlignment;
// 验证 minAlignment <= 256
```

### 9.2 动态偏移

动态偏移的使用场景：

1. **实例化渲染**：多个对象共享同一个 Bind Group，但各自的数据位于 Uniform Buffer 的不同偏移处
2. **帧内多对象绘制**：在同一个 Render Pass 中绘制多个对象，通过切换动态偏移来绑定不同的 ObjectData

注意事项：
- 一个 Bind Group 中可以有多个动态 uniform buffer binding
- `SetBindGroup` 调用时按 binding 声明顺序提供偏移数组
- 偏移值必须是 `minUniformBufferOffsetAlignment` 的倍数

### 9.3 Surface 管理

- Surface 格式在初始化时确定（通常为 `Bgra8Unorm`）
- 如果 `GetCurrentTexture` 返回错误状态，需要调用 `SurfaceConfigure` 重新配置
- `PresentMode.Fifo` 保证 VSync，帧率不超过显示器刷新率
- Surface 纹理不需要手动释放，由 WebGPU 运行时管理

### 9.4 命令缓冲区

- WebGPU 的 CommandBuffer 是**一次性使用**的
- 创建 → 提交 → 释放，不可重用
- 一个 CommandEncoder 可以录制多个 Render Pass
- `CommandEncoderFinish` 产生 CommandBuffer，之后 Encoder 不可再用

### 9.5 异步操作模式

WebGPU 的许多操作是异步的，使用回调模式：

```csharp
wgpu.InstanceRequestAdapter(instance, options, callback, userdata);
```

关键点：
- 回调可能在当前函数返回后才被调用
- 需要确保回调中引用的数据在回调触发时仍然有效
- Kilo 引擎使用局部变量和闭包来管理回调中的数据捕获

### 9.6 错误处理

建议在开发阶段始终设置以下回调：

```csharp
// 未捕获错误回调 — 所有 GPU 验证错误的最终报告点
wgpu.DeviceSetUncapturedErrorCallback(device, (type, message, userdata) =>
{
    Console.Error.WriteLine($"[WebGPU Error] Type={type}, Message={SilkMarshal.PtrToString(message)}");
}, null);

// 设备丢失回调 — GPU 设备不可用时触发
wgpu.DeviceSetDeviceLostCallback(device, (reason, message, userdata) =>
{
    Console.Error.WriteLine($"[WebGPU Device Lost] Reason={reason}");
}, null);
```

### 9.7 平台兼容性

Silk.NET 的 WebGPU 绑定通过以下方式实现跨平台：

- **Windows**：通过 Dawn 或 wgpu-native 原生库
- **macOS/iOS**：通过 wgpu-native（Metal 后端）
- **Linux**：通过 wgpu-native（Vulkan 后端）

`window.CreateWebGPUSurface` 自动根据平台选择正确的 Surface 创建方式（Win32、XCB、Metal、Wayland 等）。

---

## 附录：常用 API 速查

### 资源创建

| 操作 | WebGPU C API | 说明 |
|---|---|---|
| 创建纹理 | `wgpu.DeviceCreateTexture` | 需要描述符 |
| 创建纹理视图 | `wgpu.TextureCreateView` | 可选描述符 |
| 创建采样器 | `wgpu.DeviceCreateSampler` | 需要描述符 |
| 创建缓冲区 | `wgpu.DeviceCreateBuffer` | 需要描述符 |
| 创建着色器模块 | `wgpu.DeviceCreateShaderModule` | WGSL 源码 |
| 创建渲染管线 | `wgpu.DeviceCreateRenderPipeline` | 需要完整描述符 |
| 创建管线布局 | `wgpu.DeviceCreatePipelineLayout` | 绑定组布局数组 |
| 创建绑定组布局 | `wgpu.DeviceCreateBindGroupLayout` | 绑定条目数组 |
| 创建绑定组 | `wgpu.DeviceCreateBindGroup` | 绑定条目数组 |

### 命令录制

| 操作 | WebGPU C API | 说明 |
|---|---|---|
| 创建命令编码器 | `wgpu.DeviceCreateCommandEncoder` | |
| 开始渲染通道 | `wgpu.CommandEncoderBeginRenderPass` | 需要附件描述 |
| 设置管线 | `wgpu.RenderPassEncoderSetPipeline` | |
| 设置顶点缓冲区 | `wgpu.RenderPassEncoderSetVertexBuffer` | slot + buffer + offset + size |
| 设置索引缓冲区 | `wgpu.RenderPassEncoderSetIndexBuffer` | buffer + format + offset + size |
| 设置绑定组 | `wgpu.RenderPassEncoderSetBindGroup` | group index + bind group + offsets |
| 索引绘制 | `wgpu.RenderPassEncoderDrawIndexed` | indexCount + instanceCount + ... |
| 结束渲染通道 | `wgpu.RenderPassEncoderEnd` | |
| 完成编码 | `wgpu.CommandEncoderFinish` | 返回 CommandBuffer |
| 提交命令 | `wgpu.QueueSubmit` | CommandBuffer 数组 |

### Surface 操作

| 操作 | WebGPU C API | 说明 |
|---|---|---|
| 配置 Surface | `wgpu.SurfaceConfigure` | 格式、尺寸、呈现模式 |
| 获取当前纹理 | `wgpu.SurfaceGetCurrentTexture` | 每帧调用 |
| 呈现 | `wgpu.SurfacePresent` | 帧结束时调用 |
| 获取能力 | `wgpu.SurfaceGetCapabilities` | 查询支持的格式 |

---

*本文档基于 Kilo 引擎 `src/Kilo.Rendering/` 代码库编写，最后更新：2026-04-12*
