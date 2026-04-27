# Veldrid 渲染指南

## 目录

1. [概述与安装](#1-概述与安装)
2. [图形设备创建](#2-图形设备创建)
3. [资源管理](#3-资源管理)
4. [管线状态对象](#4-管线状态对象)
5. [命令列表与渲染循环](#5-命令列表与渲染循环)
6. [着色器编写](#6-着色器编写)
7. [2D 渲染](#7-2d-渲染)
8. [3D 渲染](#8-3d-渲染)
9. [与 ECS 的集成模式](#9-与-ecs-的集成模式)
10. [性能最佳实践](#10-性能最佳实践)

---

## 1. 概述与安装

### 什么是 Veldrid？

Veldrid 是一个**跨平台、图形 API 无关的 .NET 渲染和计算库**。它提供了对系统 GPU 的强大统一接口，能够构建真正可移植的高性能 3D 应用。

### 关键特性

- **跨平台**：运行于 Windows、Linux、macOS
- **多后端支持**：Direct3D 11、Vulkan、Metal、OpenGL 3、OpenGL ES 3
- **现代图形 API**：支持计算着色器、实例化渲染、多渲染目标
- **高性能**：为性能关键型应用设计
- **.NET Standard 2.0**：兼容 .NET Core 2.0+、.NET Framework、Mono

### 支持的后端

| 后端 | 平台 | 说明 |
|------|------|------|
| Direct3D 11 | Windows | Windows 上兼容性最广 |
| Vulkan | Windows、Linux | 现代、高性能 |
| Metal | macOS | macOS 原生图形 |
| OpenGL 3 | Windows、Linux、macOS | 遗留支持 |
| OpenGL ES 3 | 移动端 | 移动平台 |

### 安装

将以下 NuGet 包添加到项目中：

```xml
<ItemGroup>
  <PackageReference Include="Veldrid" Version="4.8.0" />
  <PackageReference Include="Veldrid.StartupUtilities" Version="4.8.0" />
  <PackageReference Include="Veldrid.SPIRV" Version="1.0.13" />
</ItemGroup>
```

- **Veldrid**：核心图形库
- **Veldrid.StartupUtilities**：基于 SDL2 的窗口创建辅助工具
- **Veldrid.SPIRV**：运行时着色器编译和转换

### 从源码构建

```bash
git clone https://github.com/veldrid/veldrid.git
cd veldrid
dotnet build
```

---

## 2. 图形设备创建

### 基本设置

使用 Veldrid 的第一步是创建 `GraphicsDevice`，它代表 GPU，是所有图形操作的核心对象。

```csharp
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

class Program
{
    private static GraphicsDevice _graphicsDevice;

    static void Main()
    {
        // 创建窗口
        WindowCreateInfo windowCI = new WindowCreateInfo()
        {
            X = 100,
            Y = 100,
            WindowWidth = 1280,
            WindowHeight = 720,
            WindowTitle = "Veldrid 应用程序"
        };
        Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);

        // 配置图形设备选项
        GraphicsDeviceOptions options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,  // Y 轴向上的坐标系
            PreferDepthRangeZeroToOne = true,           // 标准深度范围
            Debug = false                                // 启用调试层进行验证
        };

        // 创建图形设备
        _graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options);

        // 应用程序循环
        while (window.Exists)
        {
            window.PumpEvents();
            // 在此处渲染
        }

        // 清理
        _graphicsDevice.Dispose();
    }
}
```

### GraphicsDeviceOptions

```csharp
public struct GraphicsDeviceOptions
{
    public bool PreferStandardClipSpaceYDirection;  // Y 向上 vs Y 向下
    public bool PreferDepthRangeZeroToOne;          // [0,1] vs [-1,1]
    public bool SyncToVerticalBlank;               // 垂直同步
    public bool ResourceBindingModel;              // 绑定模型
    public bool Debug;                             // 调试验证层
    public SwapchainDepthFormat? SwapchainDepthFormat; // 深度缓冲格式
}
```

### 后端选择

Veldrid 可以自动选择最佳可用后端，也可以显式指定：

```csharp
// 自动选择
_graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options);

// 显式后端选择
GraphicsDeviceOptions options = new GraphicsDeviceOptions { ... };
Backend backend = Backend.Vulkan;  // 或 .D3D11, .Metal, .OpenGL
_graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options, backend);
```

### 窗口管理

```csharp
Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);

// 窗口事件
window.Resized += () =>
{
    // 处理大小变化 - 图形设备将自动调整大小
    _graphicsDevice.ResizeMainWindow((uint)window.Width, (uint)window.Height);
};

// 输入处理
while (window.Exists)
{
    window.PumpEvents();
    if (window.KeyDown(Key.Escape)) break;

    // 鼠标状态
    var mouseState = window.MouseState;
    // 键盘状态
    var keyboardState = window.KeyState;
}
```

---

## 3. 资源管理

### 资源工厂

所有 GPU 资源都通过 `ResourceFactory` 创建：

```csharp
ResourceFactory factory = _graphicsDevice.ResourceFactory;
```

### 设备缓冲区

缓冲区用于存储顶点数据、索引数据和其他 GPU 可访问的数据。

#### 顶点缓冲区

```csharp
// 定义顶点结构
struct VertexPositionColor
{
    public Vector3 Position;
    public Vector4 Color;

    public const uint SizeInBytes = 28; // 12 (Vector3) + 16 (Vector4)
}

// 创建顶点数据
VertexPositionColor[] vertices = new VertexPositionColor[]
{
    new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0), new Vector4(1, 0, 0, 1)),
    new VertexPositionColor(new Vector3(0.5f, 0.5f, 0), new Vector4(0, 1, 0, 1)),
    new VertexPositionColor(new Vector3(0, -0.5f, 0), new Vector4(0, 0, 1, 1))
};

// 创建缓冲区
DeviceBuffer vertexBuffer = factory.CreateBuffer(new BufferDescription(
    (uint)(vertices.Length * VertexPositionColor.SizeInBytes),
    BufferUsage.VertexBuffer
));

// 上传数据
_graphicsDevice.UpdateBuffer(vertexBuffer, 0, vertices);
```

#### 索引缓冲区

```csharp
// 创建索引数据
ushort[] indices = new ushort[] { 0, 1, 2 };

// 创建缓冲区
DeviceBuffer indexBuffer = factory.CreateBuffer(new BufferDescription(
    (uint)(indices.Length * sizeof(ushort)),
    BufferUsage.IndexBuffer
));

// 上传数据
_graphicsDevice.UpdateBuffer(indexBuffer, 0, indices);
```

#### Uniform/常量缓冲区

```csharp
// 定义 uniform 数据结构
struct UniformData
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Vector4 Color;
}

// 创建缓冲区（Dynamic 用于频繁更新）
DeviceBuffer uniformBuffer = factory.CreateBuffer(new BufferDescription(
    (uint)Unsafe.SizeOf<UniformData>(),
    BufferUsage.UniformBuffer | BufferUsage.Dynamic
));

// 更新 uniform 数据
var uniforms = new UniformData
{
    Model = Matrix4x4.Identity,
    View = viewMatrix,
    Projection = projectionMatrix,
    Color = new Vector4(1, 0, 0, 1)
};
_graphicsDevice.UpdateBuffer(uniformBuffer, 0, ref uniforms);
```

#### 缓冲区用法标志

```csharp
public enum BufferUsage
{
    None = 0,
    VertexBuffer = 1,
    IndexBuffer = 2,
    UniformBuffer = 4,
    StructuredBufferReadOnly = 8,
    StructuredBufferReadWrite = 16,
    IndirectBuffer = 32,
    Dynamic = 64  // 用于频繁更新的缓冲区
}
```

### 纹理

#### 创建纹理

```csharp
// 纹理描述
TextureDescription textureDesc = new TextureDescription(
    width: 256,
    height: 256,
    depth: 1,
    mipLevels: 1,
    arrayLayers: 1,
    format: PixelFormat.R8_G8_B8_A8_UNorm,
    usage: TextureUsage.Sampled,
    type: TextureType.Texture2D
);

Texture texture = factory.CreateTexture(textureDesc);

// 上传纹理数据
byte[] textureData = LoadTextureData("texture.png");
_graphicsDevice.UpdateTexture(
    texture,
    textureData,
    0, 0, 0,  // mip, array, depth
    0, 0, 0,  // x, y, z
    256, 256, 1,  // width, height, depth
    0, 0  // layer, mip
);
```

#### 纹理视图

```csharp
// 创建纹理视图用于采样
TextureView textureView = factory.CreateTextureView(
    new TextureViewDescription(
        texture,
        0, 1,  // mip 层级
        0, 1   // array 层
    )
);
```

#### 纹理采样器

```csharp
Sampler sampler = factory.CreateSampler(new SamplerDescription(
    addressModeU: SamplerAddressMode.Wrap,
    addressModeV: SamplerAddressMode.Wrap,
    addressModeW: SamplerAddressMode.Wrap,
    filter: SamplerFilter.MinLinear_MagLinear_MipLinear,
    comparisonKind: ComparisonKind.Never,
    minimumLod: 0,
    maximumLod: float.MaxValue,
    maximumAnisotropy: 16,
    borderColor: SamplerBorderColor.TransparentBlack
));
```

### 着色器

#### 从代码加载着色器（GLSL）

```csharp
using Veldrid.SPIRV;

const string VertexCode = @"
#version 450
layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;
layout(location = 0) out vec4 fsin_Color;
void main()
{
    gl_Position = vec4(Position, 1.0);
    fsin_Color = Color;
}";

const string FragmentCode = @"
#version 450
layout(location = 0) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;
void main()
{
    fsout_Color = fsin_Color;
}";

// 创建着色器描述
ShaderDescription vertexShaderDesc = new ShaderDescription(
    ShaderStages.Vertex,
    Encoding.UTF8.GetBytes(VertexCode),
    "main"
);
ShaderDescription fragmentShaderDesc = new ShaderDescription(
    ShaderStages.Fragment,
    Encoding.UTF8.GetBytes(FragmentCode),
    "main"
);

// 编译并创建着色器
Shader[] shaders = factory.CreateFromSpirv(
    vertexShaderDesc,
    fragmentShaderDesc
);
```

#### 加载预编译着色器

```csharp
// 加载 SPIR-V 着色器（用于 Vulkan）
Shader vertexShader = factory.CreateShader(new ShaderDescription(
    ShaderStages.Vertex,
    File.ReadAllBytes("vertex.spv"),
    "main"
));

// 加载编译后的 HLSL 着色器（用于 D3D11）
Shader fragmentShader = factory.CreateShader(new ShaderDescription(
    ShaderStages.Fragment,
    File.ReadAllBytes("fragment.fxc"),
    "main"
));
```

### 资源布局

资源布局定义着色器如何访问资源（纹理、缓冲区、采样器）。

```csharp
// 定义资源集
ResourceLayout resourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
    // 绑定点 0：Uniform 缓冲区
    new ResourceLayoutElementDescription("Uniforms", 0, ResourceKind.UniformBuffer, ShaderStages.Vertex),
    // 绑定点 1：纹理
    new ResourceLayoutElementDescription("Texture", 1, ResourceKind.TextureReadOnly, ShaderStages.Fragment),
    // 绑定点 2：采样器
    new ResourceLayoutElementDescription("Sampler", 2, ResourceKind.Sampler, ShaderStages.Fragment)
));

// 创建资源集
ResourceSet resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
    resourceLayout,
    uniformBuffer,
    textureView,
    sampler
));
```

---

## 4. 管线状态对象

### 图形管线概述

`Pipeline` 对象封装了渲染所需的所有状态，包括着色器、混合状态、深度/模板状态等。

### 创建图形管线

```csharp
// 定义顶点布局
VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
    new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
    new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4)
);

// 创建管线描述
GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription
{
    // 混合状态
    BlendState = BlendStateDescription.SingleOverrideBlend,

    // 深度/模板状态
    DepthStencilState = new DepthStencilStateDescription(
        depthTestEnabled: true,
        depthWriteEnabled: true,
        comparisonKind: ComparisonKind.LessEqual
    ),

    // 光栅化器状态
    RasterizerState = new RasterizerStateDescription(
        cullMode: FaceCullMode.Back,
        fillMode: PolygonFillMode.Solid,
        frontFace: FrontFace.Clockwise,
        depthClipEnabled: true,
        scissorTestEnabled: false
    ),

    // 图元拓扑
    PrimitiveTopology = PrimitiveTopology.TriangleList,

    // 资源布局
    ResourceLayouts = new[] { resourceLayout },

    // 着色器
    ShaderSet = new ShaderSetDescription(
        new[] { vertexLayout },
        shaders
    ),

    // 输出目标
    Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription
};

// 创建管线
Pipeline pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
```

### 混合状态

```csharp
// 简单覆盖混合（无混合）
BlendStateDescription simpleBlend = BlendStateDescription.SingleOverrideBlend;

// Alpha 混合
BlendStateDescription alphaBlend = new BlendStateDescription(
    alphaToCoverageEnabled: false,
    attachmentStates: new[]
    {
        new BlendAttachmentDescription(
            sourceColorFactor: BlendFactor.SourceAlpha,
            destinationColorFactor: BlendFactor.InverseSourceAlpha,
            blendFunction: BlendFunction.Add,
            sourceAlphaFactor: BlendFactor.One,
            destinationAlphaFactor: BlendFactor.InverseSourceAlpha,
            blendAlphaFunction: BlendFunction.Add
        )
    }
);

// 加法混合
BlendStateDescription additiveBlend = new BlendStateDescription(
    alphaToCoverageEnabled: false,
    attachmentStates: new[]
    {
        new BlendAttachmentDescription(
            sourceColorFactor: BlendFactor.SourceAlpha,
            destinationColorFactor: BlendFactor.One,
            blendFunction: BlendFunction.Add,
            sourceAlphaFactor: BlendFactor.One,
            destinationAlphaFactor: BlendFactor.One,
            blendAlphaFunction: BlendFunction.Add
        )
    }
);
```

### 深度/模板状态

```csharp
// 标准深度测试
DepthStencilStateDescription standardDepth = new DepthStencilStateDescription(
    depthTestEnabled: true,
    depthWriteEnabled: true,
    comparisonKind: ComparisonKind.LessEqual
);

// 无深度测试（用于 UI、透明物体）
DepthStencilStateDescription noDepth = new DepthStencilStateDescription(
    depthTestEnabled: false,
    depthWriteEnabled: false,
    comparisonKind: ComparisonKind.Always
);

// 仅深度读取（用于透明物体）
DepthStencilStateDescription depthReadOnly = new DepthStencilStateDescription(
    depthTestEnabled: true,
    depthWriteEnabled: false,
    comparisonKind: ComparisonKind.LessEqual
);
```

### 光栅化器状态

```csharp
// 标准光栅化器
RasterizerStateDescription standardRasterizer = new RasterizerStateDescription(
    cullMode: FaceCullMode.Back,
    fillMode: PolygonFillMode.Solid,
    frontFace: FrontFace.Clockwise,
    depthClipEnabled: true,
    scissorTestEnabled: false
);

// 无背面剔除（用于双面物体）
RasterizerStateDescription noCull = new RasterizerStateDescription(
    cullMode: FaceCullMode.None,
    fillMode: PolygonFillMode.Solid,
    frontFace: FrontFace.Clockwise,
    depthClipEnabled: true,
    scissorTestEnabled: false
);

// 线框渲染
RasterizerStateDescription wireframe = new RasterizerStateDescription(
    cullMode: FaceCullMode.Back,
    fillMode: PolygonFillMode.Wireframe,
    frontFace: FrontFace.Clockwise,
    depthClipEnabled: true,
    scissorTestEnabled: false
);
```

### 图元拓扑

```csharp
public enum PrimitiveTopology
{
    PointList,           // 点
    LineList,            // 不连续的线段
    LineStrip,           // 连续的线段
    TriangleList,        // 不连续的三角形
    TriangleStrip        // 连续的三角形
}
```

---

## 5. 命令列表与渲染循环

### 命令列表基础

命令列表用于记录和执行图形命令。

```csharp
CommandList commandList = factory.CreateCommandList();
```

### 基本渲染帧

```csharp
private void DrawFrame()
{
    // 开始记录命令
    _commandList.Begin();

    // 设置帧缓冲
    _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);

    // 清除屏幕
    _commandList.ClearColorTarget(0, RgbaFloat.Black);
    _commandList.ClearDepthStencil(1.0f);

    // 设置管线
    _commandList.SetPipeline(_pipeline);

    // 设置资源
    _commandList.SetGraphicsResourceSet(0, _resourceSet);

    // 设置顶点和索引缓冲区
    _commandList.SetVertexBuffer(0, _vertexBuffer);
    _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);

    // 绘制
    _commandList.DrawIndexed(
        indexCount: 6,
        instanceCount: 1,
        indexStart: 0,
        vertexOffset: 0,
        instanceStart: 0
    );

    // 结束记录
    _commandList.End();

    // 提交命令
    _graphicsDevice.SubmitCommands(_commandList);

    // 呈现
    _graphicsDevice.SwapBuffers();
}
```

### 完整应用循环

```csharp
static void Main()
{
    // 设置窗口和设备...

    CreateResources();

    while (window.Exists)
    {
        window.PumpEvents();
        if (window.KeyDown(Key.Escape)) break;

        DrawFrame();
    }

    DisposeResources();
}

private void CreateResources()
{
    ResourceFactory factory = _graphicsDevice.ResourceFactory;

    // 创建所有资源...
    _vertexBuffer = factory.CreateBuffer(...);
    _indexBuffer = factory.CreateBuffer(...);
    _pipeline = factory.CreateGraphicsPipeline(...);
    _commandList = factory.CreateCommandList();
}

private void DisposeResources()
{
    _pipeline.Dispose();
    _vertexBuffer.Dispose();
    _indexBuffer.Dispose();
    _commandList.Dispose();
    _graphicsDevice.Dispose();
}
```

### 多渲染通道

```csharp
private void RenderScene()
{
    _commandList.Begin();

    // 通道 1：阴影贴图
    _commandList.SetFramebuffer(_shadowFramebuffer);
    _commandList.ClearDepthStencil(1.0f);
    _commandList.SetPipeline(_shadowPipeline);
    // 绘制场景...

    // 通道 2：主场景
    _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
    _commandList.ClearColorTarget(0, RgbaFloat.Black);
    _commandList.ClearDepthStencil(1.0f);
    _commandList.SetPipeline(_mainPipeline);
    _commandList.SetGraphicsResourceSet(0, _shadowMapResourceSet);
    // 绘制场景...

    // 通道 3：UI 覆盖层
    _commandList.SetPipeline(_uiPipeline);
    // 绘制 UI...

    _commandList.End();
    _graphicsDevice.SubmitCommands(_commandList);
}
```

### 裁剪测试

```csharp
// 在光栅化器状态中启用裁剪
RasterizerStateDescription rasterizerState = new RasterizerStateDescription(
    cullMode: FaceCullMode.Back,
    fillMode: PolygonFillMode.Solid,
    frontFace: FrontFace.Clockwise,
    depthClipEnabled: true,
    scissorTestEnabled: true  // 启用裁剪
);

// 设置裁剪矩形
_commandList.SetScissorRect(0, new Rectangle(0, 0, 640, 480));
```

---

## 6. 着色器编写

### GLSL 着色器

Veldrid 通过 SPIRV 包处理 GLSL，可在运行时编译和转换着色器。

#### 基本顶点着色器

```glsl
#version 450

// 输入属性
layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 TexCoord;
layout(location = 2) in vec3 Normal;

// 输出到片段着色器
layout(location = 0) out vec2 fsin_TexCoord;
layout(location = 1) out vec3 fsin_Normal;
layout(location = 2) out vec3 fsin_WorldPos;

// Uniform 变量
layout(set = 0, binding = 0) uniform Uniforms
{
    mat4 Model;
    mat4 View;
    mat4 Projection;
};

void main()
{
    vec4 worldPos = Model * vec4(Position, 1.0);
    gl_Position = Projection * View * worldPos;

    fsin_TexCoord = TexCoord;
    fsin_Normal = mat3(Model) * Normal;
    fsin_WorldPos = worldPos.xyz;
}
```

#### 基本片段着色器

```glsl
#version 450

// 从顶点着色器输入
layout(location = 0) in vec2 fsin_TexCoord;
layout(location = 1) in vec3 fsin_Normal;
layout(location = 2) in vec3 fsin_WorldPos;

// 输出
layout(location = 0) out vec4 fsout_Color;

// 资源
layout(set = 0, binding = 1) uniform texture2D AlbedoTexture;
layout(set = 0, binding = 2) uniform sampler Sampler;

void main()
{
    vec3 normal = normalize(fsin_Normal);
    vec3 lightDir = normalize(vec3(1.0, 1.0, 1.0));

    // 采样纹理
    vec4 albedo = texture(sampler2D(AlbedoTexture, Sampler), fsin_TexCoord);

    // 简单的方向光照
    float diffuse = max(dot(normal, lightDir), 0.0);

    fsout_Color = vec4(albedo.rgb * diffuse, albedo.a);
}
```

### HLSL 着色器

对于 D3D11 后端，可以使用 HLSL 着色器：

#### HLSL 顶点着色器

```hlsl
cbuffer Uniforms : register(b0)
{
    matrix Model;
    matrix View;
    matrix Projection;
};

struct VSInput
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : NORMAL;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : NORMAL;
    float3 WorldPos : WORLDPOS;
};

PSInput VSMain(VSInput input)
{
    PSInput output;

    float4 worldPos = mul(Model, float4(input.Position, 1.0));
    output.Position = mul(Projection, mul(View, worldPos));
    output.TexCoord = input.TexCoord;
    output.Normal = mul((float3x3)Model, input.Normal);
    output.WorldPos = worldPos.xyz;

    return output;
}
```

#### HLSL 片段着色器

```hlsl
Texture2D AlbedoTexture : register(t0);
SamplerState Sampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : NORMAL;
    float3 WorldPos : WORLDPOS;
};

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 normal = normalize(input.Normal);
    float3 lightDir = normalize(float3(1.0, 1.0, 1.0));

    float4 albedo = AlbedoTexture.Sample(Sampler, input.TexCoord);
    float diffuse = max(dot(normal, lightDir), 0.0);

    return float4(albedo.rgb * diffuse, albedo.a);
}
```

### 计算着色器

```glsl
#version 450

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba8) uniform readonly image2D InputImage;
layout(set = 0, binding = 1, rgba8) uniform writeonly image2D OutputImage;

void main()
{
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    vec4 color = imageLoad(InputImage, coord);

    // 反转颜色
    color.rgb = 1.0 - color.rgb;

    imageStore(OutputImage, coord, color);
}
```

---

## 7. 2D 渲染

### 精灵批处理

要实现高效的 2D 精灵渲染，请使用实例化渲染或将多个精灵批量处理。

```csharp
struct SpriteVertex
{
    public Vector2 Position;
    public Vector2 TexCoord;
    public Vector4 Color;
}

struct SpriteInstanceData
{
    public Matrix3x2 Transform;
    public Vector4 Color;
    public Vector2 TexCoordOffset;
    public Vector2 TexCoordScale;
}

// 创建四边形顶点缓冲区（所有精灵共享）
SpriteVertex[] quadVertices = new SpriteVertex[]
{
    new SpriteVertex(new Vector2(0, 0), new Vector2(0, 0), Vector4.One),
    new SpriteVertex(new Vector2(1, 0), new Vector2(1, 0), Vector4.One),
    new SpriteVertex(new Vector2(0, 1), new Vector2(0, 1), Vector4.One),
    new SpriteVertex(new Vector2(1, 1), new Vector2(1, 1), Vector4.One)
};

DeviceBuffer quadVertexBuffer = factory.CreateBuffer(
    new BufferDescription(
        (uint)(quadVertices.Length * Unsafe.SizeOf<SpriteVertex>()),
        BufferUsage.VertexBuffer
    )
);
_graphicsDevice.UpdateBuffer(quadVertexBuffer, 0, quadVertices);

// 为多个精灵创建实例缓冲区
const int MaxSprites = 1000;
DeviceBuffer instanceBuffer = factory.CreateBuffer(
    new BufferDescription(
        (uint)(MaxSprites * Unsafe.SizeOf<SpriteInstanceData>()),
        BufferUsage.VertexBuffer | BufferUsage.Dynamic
    )
);

// 用精灵变换更新实例数据
SpriteInstanceData[] instances = new SpriteInstanceData[spriteCount];
// 填充实例数据...
_graphicsDevice.UpdateBuffer(instanceBuffer, 0, instances);
```

### 2D 相机

```csharp
public class Camera2D
{
    public Vector2 Position { get; set; }
    public float Zoom { get; set; } = 1.0f;
    public float Rotation { get; set; }

    public Matrix4x4 GetViewMatrix(int viewportWidth, int viewportHeight)
    {
        Matrix4x4 view = Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0);
        view *= Matrix4x4.CreateRotationZ(-Rotation);
        view *= Matrix4x4.CreateScale(Zoom, Zoom, 1);

        // 将原点居中
        view *= Matrix4x4.CreateTranslation(viewportWidth / 2f, viewportHeight / 2f, 0);

        return view;
    }

    public Matrix4x4 GetProjectionMatrix(int viewportWidth, int viewportHeight)
    {
        return Matrix4x4.CreateOrthographicOffCenter(
            0, viewportWidth, viewportHeight, 0, -1, 1
        );
    }
}
```

### 绘制精灵

```csharp
void DrawSprite(Texture texture, Vector2 position, Vector2 size, Vector4 color)
{
    Matrix3x2 transform =
        Matrix3x2.CreateTranslation(position.X, position.Y) *
        Matrix3x2.CreateScale(size.X, size.Y);

    var instance = new SpriteInstanceData
    {
        Transform = transform,
        Color = color,
        TexCoordOffset = Vector2.Zero,
        TexCoordScale = Vector2.One
    };

    // 添加到批次...
}

void RenderSprites()
{
    _commandList.SetVertexBuffer(0, _quadVertexBuffer);
    _commandList.SetVertexBuffer(1, _instanceBuffer);
    _commandList.SetPipeline(_spritePipeline);
    _commandList.SetGraphicsResourceSet(0, _spriteResourceSet);

    _commandList.DrawIndexedInstanced(
        indexCount: 6,
        instanceCount: _spriteCount,
        indexStart: 0,
        vertexOffset: 0,
        instanceStart: 0
    );
}
```

---

## 8. 3D 渲染

### 3D 相机

```csharp
public class Camera3D
{
    public Vector3 Position { get; set; }
    public Vector3 Target { get; set; }
    public Vector3 Up { get; set; } = Vector3.UnitY;
    public float Fov { get; set; } = MathF.PI / 4; // 45 度
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 1000.0f;

    public Matrix4x4 ViewMatrix
    {
        get => Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    public Matrix4x4 ProjectionMatrix(int width, int height)
    {
        float aspectRatio = (float)width / height;
        return Matrix4x4.CreatePerspectiveFieldOfView(
            Fov, aspectRatio, NearPlane, FarPlane
        );
    }
}
```

### 网格渲染

```csharp
struct MeshVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;
    public Vector3 Tangent;
}

public class Mesh
{
    private DeviceBuffer _vertexBuffer;
    private DeviceBuffer _indexBuffer;
    private uint _indexCount;

    public Mesh(GraphicsDevice device, MeshVertex[] vertices, ushort[] indices)
    {
        ResourceFactory factory = device.ResourceFactory;

        _vertexBuffer = factory.CreateBuffer(new BufferDescription(
            (uint)(vertices.Length * Unsafe.SizeOf<MeshVertex>()),
            BufferUsage.VertexBuffer
        ));
        device.UpdateBuffer(_vertexBuffer, 0, vertices);

        _indexBuffer = factory.CreateBuffer(new BufferDescription(
            (uint)(indices.Length * sizeof(ushort)),
            BufferUsage.IndexBuffer
        ));
        device.UpdateBuffer(_indexBuffer, 0, indices);

        _indexCount = (uint)indices.Length;
    }

    public void Draw(CommandList commandList)
    {
        commandList.SetVertexBuffer(0, _vertexBuffer);
        commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        commandList.DrawIndexed(_indexCount, 1, 0, 0, 0);
    }
}
```

### 模型矩阵

```csharp
public Matrix4x4 GetModelMatrix(Vector3 position, Quaternion rotation, Vector3 scale)
{
    return
        Matrix4x4.CreateFromQuaternion(rotation) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(position);
}
```

### 光照

#### 光源 Uniform 缓冲区

```csharp
struct Light
{
    public Vector3 Position;
    public float Intensity;
    public Vector3 Color;
    public float _padding;
}

struct LightUniforms
{
    public Vector3 ViewPosition;
    public float _padding;
    public Light Lights[16];
    public int LightCount;
}
```

#### PBR 光照着色器（片段着色器）

```glsl
#version 450

layout(location = 0) in vec2 fsin_TexCoord;
layout(location = 1) in vec3 fsin_Normal;
layout(location = 2) in vec3 fsin_WorldPos;
layout(location = 3) in vec3 fsin_Tangent;

layout(location = 0) out vec4 fsout_Color;

layout(set = 0, binding = 0) uniform Uniforms
{
    mat4 Model;
    mat4 View;
    mat4 Projection;
};

layout(set = 0, binding = 1) uniform LightData
{
    vec3 ViewPosition;
    int LightCount;
    vec4 Lights[16]; // 位置、颜色、强度打包
};

layout(set = 0, binding = 2) uniform texture2D AlbedoMap;
layout(set = 0, binding = 3) uniform texture2D NormalMap;
layout(set = 0, binding = 4) uniform texture2D MetallicRoughnessMap;
layout(set = 0, binding = 5) uniform sampler Sampler;

const float PI = 3.14159265359;

vec3 getNormalFromMap()
{
    vec3 tangentNormal = texture(sampler2D(NormalMap, Sampler), fsin_TexCoord).xyz * 2.0 - 1.0;

    vec3 Q1 = dFdx(fsin_WorldPos);
    vec3 Q2 = dFdy(fsin_WorldPos);
    vec2 st1 = dFdx(fsin_TexCoord);
    vec2 st2 = dFdy(fsin_TexCoord);

    vec3 N = normalize(fsin_Normal);
    vec3 T = normalize(Q1 * st2.t - Q2 * st1.t);
    vec3 B = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

float distributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / max(denom, 0.0001);
}

float geometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float geometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = geometrySchlickGGX(NdotV, roughness);
    float ggx1 = geometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

void main()
{
    vec3 albedo = pow(texture(sampler2D(AlbedoMap, Sampler), fsin_TexCoord).rgb, vec3(2.2));
    vec3 normal = getNormalFromMap();
    vec4 mr = texture(sampler2D(MetallicRoughnessMap, Sampler), fsin_TexCoord);
    float metallic = mr.b;
    float roughness = mr.g;

    vec3 N = normalize(normal);
    vec3 V = normalize(ViewPosition - fsin_WorldPos);

    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    vec3 Lo = vec3(0.0);

    for (int i = 0; i < LightCount; ++i)
    {
        vec3 lightPos = Lights[i].xyz;
        vec3 lightColor = Lights[i + 16].rgb;
        float intensity = Lights[i + 32].r;

        vec3 L = normalize(lightPos - fsin_WorldPos);
        vec3 H = normalize(V + L);
        float distance = length(lightPos - fsin_WorldPos);
        float attenuation = 1.0 / (distance * distance);

        vec3 radiance = lightColor * intensity * attenuation;

        float NDF = distributionGGX(N, H, roughness);
        float G = geometrySmith(N, V, L, roughness);
        vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metallic;

        vec3 numerator = NDF * G * F;
        float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0);
        vec3 specular = numerator / max(denominator, 0.001);

        float NdotL = max(dot(N, L), 0.0);
        Lo += (kD * albedo / PI + specular) * radiance * NdotL;
    }

    vec3 ambient = vec3(0.03) * albedo;
    vec3 color = ambient + Lo;

    color = color / (color + vec3(1.0));
    color = pow(color, vec3(1.0 / 2.2));

    fsout_Color = vec4(color, 1.0);
}
```

---

## 9. 与 ECS 的集成模式

### ECS 组件方案

```csharp
// 渲染组件
public struct TransformComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

public struct MeshComponent
{
    public DeviceBuffer VertexBuffer;
    public DeviceBuffer IndexBuffer;
    public uint IndexCount;
    public Pipeline Pipeline;
}

public struct MaterialComponent
{
    public ResourceSet ResourceSet;
    public Vector4 Color;
}

public struct RenderTag
{
    // 可渲染实体的标记组件
}

// 渲染系统
public class RenderSystem : IKiloSystem
{
    private GraphicsDevice _graphicsDevice;
    private CommandList _commandList;

    public RenderSystem(GraphicsDevice device)
    {
        _graphicsDevice = device;
        _commandList = device.ResourceFactory.CreateCommandList();
    }

    public void Update(KiloWorld world)
    {
        _commandList.Begin();
        _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Black);

        // 查询可渲染实体
        var query = world.Query<TransformComponent, MeshComponent, MaterialComponent>()
            .With<RenderTag>()
            .Build();

        foreach (ref var entity in query.Iter())
        {
            ref var transform = ref entity.Get<TransformComponent>();
            ref var mesh = ref entity.Get<MeshComponent>();
            ref var material = ref entity.Get<MaterialComponent>();

            // 创建模型矩阵
            Matrix4x4 modelMatrix = Matrix4x4.CreateFromQuaternion(transform.Rotation) *
                                   Matrix4x4.CreateScale(transform.Scale) *
                                   Matrix4x4.CreateTranslation(transform.Position);

            // 更新 uniform 变量
            UpdateUniforms(modelMatrix, material.Color);

            // 绘制网格
            _commandList.SetPipeline(mesh.Pipeline);
            _commandList.SetGraphicsResourceSet(0, material.ResourceSet);
            _commandList.SetVertexBuffer(0, mesh.VertexBuffer);
            _commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt16);
            _commandList.DrawIndexed(mesh.IndexCount, 1, 0, 0, 0);
        }

        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);
        _graphicsDevice.SwapBuffers();
    }
}
```

### 资源管理器

```csharp
public class ResourceManager : IDisposable
{
    private GraphicsDevice _device;
    private Dictionary<string, Texture> _textures;
    private Dictionary<string, Pipeline> _pipelines;
    private Dictionary<string, ResourceLayout> _layouts;
    private Dictionary<string, ResourceSet> _resourceSets;

    public ResourceManager(GraphicsDevice device)
    {
        _device = device;
        _textures = new Dictionary<string, Texture>();
        _pipelines = new Dictionary<string, Pipeline>();
        _layouts = new Dictionary<string, ResourceLayout>();
        _resourceSets = new Dictionary<string, ResourceSet>();
    }

    public Texture LoadTexture(string path)
    {
        if (_textures.TryGetValue(path, out var texture))
            return texture;

        // 从文件加载纹理
        var image = Image.Load(path);
        var pixelData = image.GetPixelSpan();

        var textureDesc = new TextureDescription(
            (uint)image.Width, (uint)image.Height, 1, 1, 1,
            PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled,
            TextureType.Texture2D
        );

        texture = _device.ResourceFactory.CreateTexture(textureDesc);
        _device.UpdateTexture(texture, pixelData.ToArray(), 0, 0, 0, 0, 0, 0,
            (uint)image.Width, (uint)image.Height, 1, 0, 0);

        _textures[path] = texture;
        return texture;
    }

    public Pipeline GetPipeline(string name, GraphicsPipelineDescription description)
    {
        if (_pipelines.TryGetValue(name, out var pipeline))
            return pipeline;

        pipeline = _device.ResourceFactory.CreateGraphicsPipeline(description);
        _pipelines[name] = pipeline;
        return pipeline;
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values) texture.Dispose();
        foreach (var pipeline in _pipelines.Values) pipeline.Dispose();
        foreach (var layout in _layouts.Values) layout.Dispose();
        foreach (var set in _resourceSets.Values) set.Dispose();
    }
}
```

---

## 10. 性能最佳实践

### 1. 合理使用动态缓冲区

```csharp
// 好的做法：为频繁更新的数据使用动态缓冲区
DeviceBuffer dynamicBuffer = factory.CreateBuffer(new BufferDescription(
    size,
    BufferUsage.UniformBuffer | BufferUsage.Dynamic  // 使用 Dynamic 标志
));

// 不好的做法：每帧更新静态缓冲区
DeviceBuffer staticBuffer = factory.CreateBuffer(new BufferDescription(
    size,
    BufferUsage.UniformBuffer  // 缺少 Dynamic 标志
));
// 不要这样做：device.UpdateBuffer(staticBuffer, 0, data) 每帧调用
```

### 2. 最小化状态变更

```csharp
// 好的做法：按管线/材质批量绘制
_commandList.SetPipeline(pipeline1);
foreach (var mesh in meshesUsingPipeline1)
{
    _commandList.SetGraphicsResourceSet(0, mesh.ResourceSet);
    mesh.Draw(_commandList);
}

_commandList.SetPipeline(pipeline2);
foreach (var mesh in meshesUsingPipeline2)
{
    _commandList.SetGraphicsResourceSet(0, mesh.ResourceSet);
    mesh.Draw(_commandList);
}

// 不好的做法：每次绘制都改变状态
foreach (var mesh in allMeshes)
{
    _commandList.SetPipeline(mesh.Pipeline);  // 状态变更！
    _commandList.SetGraphicsResourceSet(0, mesh.ResourceSet);
    mesh.Draw(_commandList);
}
```

### 3. 使用实例化渲染

```csharp
// 使用一次绘制调用渲染同一网格的多个实例
_commandList.DrawIndexedInstanced(
    indexCount: meshIndexCount,
    instanceCount: instanceCount,  // 绘制多个实例
    indexStart: 0,
    vertexOffset: 0,
    instanceStart: 0
);
```

### 4. 减少绘制调用

- 合并共享相同材质的网格
- 精灵使用纹理图集
- 重复对象使用实例化渲染

### 5. 优化顶点格式

```csharp
// 好的做法：使用合适的格式
new VertexElementDescription("Position", ..., VertexElementFormat.Float3)
new VertexElementDescription("TexCoord", ..., VertexElementFormat.Float2)

// 不好的做法：使用比需要更大的格式
new VertexElementDescription("Position", ..., VertexElementFormat.Float4)  // 浪费空间
```

### 6. 使用 Mipmap

```csharp
// 创建带 mipmap 的纹理
TextureDescription textureDesc = new TextureDescription(
    width, height, depth,
    mipLevels: CalculateMipLevels(width, height),  // 多个 mip 层级
    arrayLayers: 1,
    format: format,
    usage: TextureUsage.Sampled,
    type: TextureType.Texture2D
);

static uint CalculateMipLevels(uint width, uint height)
{
    return (uint)MathF.Ceiling(MathF.Log2(MathF.Max(width, height)));
}
```

### 7. 性能分析和优化

```csharp
// 使用时间戳查询进行性能分析
// （简化示例 - 实际实现因后端而异）
var queryPool = factory.CreateQueryPool(new QueryDescription(
    QueryType.Timestamp, 2
));

_commandList.Begin();
_commandList.WriteTimestamp(queryPool, 0, 0);

// 渲染命令...

_commandList.WriteTimestamp(queryPool, 0, 1);
_commandList.End();
_graphicsDevice.SubmitCommands(_commandList);

// 读回并计算时间差
ulong[] timestamps = new ulong[2];
_graphicsDevice.GetQueryPoolResults(queryPool, 0, 2, timestamps);
double elapsedMs = (timestamps[1] - timestamps[0]) * timestampPeriod * 0.000001;
```

### 8. 资源复用

```csharp
// 复用资源布局和资源集
private static ResourceLayout _standardResourceLayout;

public static void Initialize(GraphicsDevice device)
{
    // 创建一次，到处复用
    _standardResourceLayout = device.ResourceFactory.CreateResourceLayout(
        new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("Uniforms", 0, ResourceKind.UniformBuffer, ShaderStages.Vertex),
            new ResourceLayoutElementDescription("Texture", 1, ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("Sampler", 2, ResourceKind.Sampler, ShaderStages.Fragment)
        )
    );
}
```

### 9. 正确释放资源

```csharp
// 始终释放 GPU 资源
public class Renderer : IDisposable
{
    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _commandList?.Dispose();
        _pipeline?.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _resourceSet?.Dispose();
        _graphicsDevice?.Dispose();
    }
}
```

---

## 结论

Veldrid 为 .NET 应用提供了强大、跨平台的图形 API。其优势在于：

1. **跨平台兼容性** - 一次编写，在多个后端运行
2. **现代图形功能** - 支持计算着色器、实例化渲染等
3. **面向性能的设计** - 底层控制便于优化
4. **灵活的架构** - 支持各种窗口系统

与 Kilo 等 ECS 系统集成时，请注意：
- 将资源管理与 ECS 组件分离
- 使用批处理和实例化渲染提升性能
- 创建可复用的管线和资源抽象
- 实现正确的资源释放模式

更多信息：
- [Veldrid 文档](https://veldrid.dev/)
- [Veldrid GitHub 仓库](https://github.com/veldrid/veldrid)
- [Veldrid 示例](https://github.com/mellinoe/veldrid-samples)
