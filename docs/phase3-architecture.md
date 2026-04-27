# 阶段 3 - 插件架构设计

**日期**：2026-04-09
**状态**：架构设计
**依赖**：Kilo.ECS（阶段 1，已完成）

---

## 1. 共享组件

这些组件结构体在多个插件间共享（尤其是渲染 + 物理）。它们位于各自的插件命名空间中，但遵循一致的命名约定。Transform 组件非常基础，因此我们只定义一次并从所有插件引用。

### 1.1 Transform（共享概念）

每个需要变换数据的插件定义自己的组件。在阶段 3 中，`Kilo.Rendering` 定义 `LocalToWorld` 和 `LocalTransform`，`Kilo.Physics` 通过 ECS 查询读取它们。

---

## 2. Kilo.Rendering 插件

### 2.1 项目文件

**路径**：`src/Kilo.Rendering/Kilo.Rendering.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Kilo.Rendering</RootNamespace>
    <AssemblyName>Kilo.Rendering</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kilo.ECS\Kilo.ECS.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Veldrid" Version="4.8.0" />
    <PackageReference Include="Veldrid.StartupUtilities" Version="4.8.0" />
    <PackageReference Include="Veldrid.SPIRV" Version="1.0.13" />
  </ItemGroup>
</Project>
```

### 2.2 项目结构

```
src/Kilo.Rendering/
├── Kilo.Rendering.csproj
├── Components/
│   ├── LocalToWorld.cs          // Matrix4x4 世界变换
│   ├── LocalTransform.cs        // 位置、旋转、缩放
│   ├── Camera.cs                // 视图/投影矩阵
│   ├── MeshRenderer.cs          // GPU 网格句柄 + 材质
│   ├── Mesh.cs                  // CPU/GPU 网格数据（DeviceBuffer 引用）
│   ├── Material.cs              // 着色器资源 + 纹理引用
│   ├── Sprite.cs                // 2D 精灵数据
│   ├── PointLight.cs            // 点光源组件
│   ├── DirectionalLight.cs      // 平行光组件
│   └── WindowSize.cs            // 当前窗口尺寸（资源）
├── Resources/
│   ├── RenderContext.cs         // GraphicsDevice、CommandList、交换链
│   ├── ShaderCache.cs           // 编译后的着色器管线缓存
│   └── RenderSettings.cs        // VSync、分辨率、后端偏好
├── Systems/
│   ├── BeginFrameSystem.cs      // 开始命令列表，清除渲染目标
│   ├── RenderSystem.cs          // 网格主渲染循环
│   ├── SpriteRenderSystem.cs    // 2D 精灵批处理
│   ├── EndFrameSystem.cs        // 提交命令，交换缓冲区
│   ├── CameraSystem.cs          // 更新视图/投影矩阵
│   └── WindowResizeSystem.cs    // 处理窗口大小变化
├── RenderingPlugin.cs           // IKiloPlugin 实现
└── RenderDeviceCreator.cs       // 创建 GraphicsDevice + 窗口的辅助类
```

### 2.3 组件（结构体）

```csharp
// Components/LocalTransform.cs
using System.Numerics;

namespace Kilo.Rendering;

public struct LocalTransform
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    public static LocalTransform Identity => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One
    };
}

// Components/LocalToWorld.cs
using System.Numerics;

namespace Kilo.Rendering;

public struct LocalToWorld
{
    public Matrix4x4 Value;
}

// Components/Camera.cs
using System.Numerics;

namespace Kilo.Rendering;

public struct Camera
{
    public Matrix4x4 ViewMatrix;
    public Matrix4x4 ProjectionMatrix;
    public float FieldOfView;
    public float NearPlane;
    public float FarPlane;
    public bool IsActive;
}

// Components/MeshRenderer.cs
using Veldrid;

namespace Kilo.Rendering;

public struct MeshRenderer
{
    public int MeshHandle;       // RenderContext.Meshes 的索引
    public int MaterialHandle;   // RenderContext.Materials 的索引
}

// Components/Sprite.cs
using System.Numerics;
using Veldrid;

namespace Kilo.Rendering;

public struct Sprite
{
    public Vector4 Tint;
    public Vector2 Size;
    public int TextureHandle;    // RenderContext.Textures 的索引
    public float ZIndex;
}

// Components/PointLight.cs
using System.Numerics;

namespace Kilo.Rendering;

public struct PointLight
{
    public Vector3 Position;
    public Vector3 Color;
    public float Intensity;
    public float Range;
}

// Components/DirectionalLight.cs
using System.Numerics;

namespace Kilo.Rendering;

public struct DirectionalLight
{
    public Vector3 Direction;
    public Vector3 Color;
    public float Intensity;
}

// Components/WindowSize.cs
namespace Kilo.Rendering;

public struct WindowSize
{
    public int Width;
    public int Height;
}
```

### 2.4 资源

```csharp
// Resources/RenderContext.cs
using Veldrid;

namespace Kilo.Rendering;

public sealed class RenderContext
{
    public GraphicsDevice Device { get; set; } = null!;
    public CommandList CommandList { get; set; } = null!;
    public Framebuffer MainFramebuffer => Device.SwapchainFramebuffer;
    public bool WindowResized { get; set; }
}
```

### 2.5 RenderingPlugin（IKiloPlugin）

```csharp
// RenderingPlugin.cs
using Kilo.ECS;

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
        // 注册资源
        app.AddResource(_settings);
        app.AddResource(new RenderContext());

        // 启动：创建设备 + 窗口
        app.AddSystem(KiloStage.Startup, world =>
        {
            var context = new RenderContext();
            var (device, window) = RenderDeviceCreator.Create(_settings);
            context.Device = device;
            context.CommandList = device.ResourceFactory.CreateCommandList();
            app.AddResource(new WindowSize
            {
                Width = _settings.Width,
                Height = _settings.Height
            });
            app.AddResource(context);
        });

        // 按正确的阶段顺序注册系统
        app.AddSystem(KiloStage.First, new CameraSystem().Update);
        app.AddSystem(KiloStage.Last, new BeginFrameSystem().Update);
        app.AddSystem(KiloStage.Last, new RenderSystem().Update);
        app.AddSystem(KiloStage.Last, new SpriteRenderSystem().Update);
        app.AddSystem(KiloStage.Last, new EndFrameSystem().Update);
    }
}
```

### 2.6 系统签名

每个系统是一个带有 `Update(KiloWorld world)` 方法的类。此模式与当前的 `AddSystem(KiloStage, Action<KiloWorld>)` API 兼容。

```csharp
// Systems/BeginFrameSystem.cs
public sealed class BeginFrameSystem
{
    public void Update(KiloWorld world) { /* 开始命令列表，清除 */ }
}

// Systems/RenderSystem.cs
public sealed class RenderSystem
{
    public void Update(KiloWorld world) { /* 查询 MeshRenderer+LocalToWorld，绘制 */ }
}

// Systems/EndFrameSystem.cs
public sealed class EndFrameSystem
{
    public void Update(KiloWorld world) { /* 提交命令，交换缓冲区 */ }
}

// Systems/CameraSystem.cs
public sealed class CameraSystem
{
    public void Update(KiloWorld world) { /* 查询 Camera+LocalTransform，更新矩阵 */ }
}
```

### 2.7 测试项目

**路径**：`tests/Kilo.Rendering.Tests/Kilo.Rendering.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Kilo.Rendering.Tests</RootNamespace>
    <AssemblyName>Kilo.Rendering.Tests</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Kilo.Rendering\Kilo.Rendering.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
    <PackageReference Include="xunit" Version="*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="*" />
  </ItemGroup>
</Project>
```

**测试**：
- `ComponentTests.cs` — 验证组件结构体可 blit、默认值正确
- `PluginRegistrationTests.cs` — 验证插件注册资源和系统
- `RenderContextTests.cs` — 测试 RenderContext 创建、释放
- `CameraSystemTests.cs` — 使用已知值测试相机矩阵计算
- `LocalTransformTests.cs` — 测试 LocalTransform.Identity、矩阵转换

---

## 3. Kilo.Physics 插件

### 3.1 项目文件

**路径**：`src/Kilo.Physics/Kilo.Physics.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Kilo.Physics</RootNamespace>
    <AssemblyName>Kilo.Physics</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kilo.ECS\Kilo.ECS.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="BepuPhysics" Version="2.5.0-beta.28" />
    <PackageReference Include="BepuUtilities" Version="2.5.0-beta.28" />
  </ItemGroup>
</Project>
```

### 3.2 项目结构

```
src/Kilo.Physics/
├── Kilo.Physics.csproj
├── Components/
│   ├── PhysicsBody.cs           // BodyHandle + 刚体类型
│   ├── PhysicsShape.cs          // 形状索引 + 质量信息
│   ├── PhysicsVelocity.cs       // 线性 + 角速度（映射 Bepu）
│   └── PhysicsCollider.cs       // 碰撞体描述数据
├── Resources/
│   ├── PhysicsWorld.cs          // Simulation、BufferPool、ThreadDispatcher
│   └── PhysicsSettings.cs       // 重力、求解器迭代次数、时间步长
├── Systems/
│   ├── PhysicsStepSystem.cs     // simulation.Timestep()
│   ├── SyncToPhysicsSystem.cs   // ECS Transform → Bepu 刚体
│   └── SyncFromPhysicsSystem.cs // Bepu 刚体 → ECS Transform
├── PhysicsPlugin.cs             // IKiloPlugin 实现
└── PhysicsBodyFactory.cs        // 创建物理刚体的辅助类
```

### 3.3 组件

```csharp
// Components/PhysicsBody.cs
using BepuPhysics;

namespace Kilo.Physics;

public struct PhysicsBody
{
    public BodyHandle BodyHandle;
    public bool IsDynamic;
    public bool IsKinematic;
}

// Components/PhysicsShape.cs
using BepuPhysics.Collidables;

namespace Kilo.Physics;

public struct PhysicsShape
{
    public TypedIndex ShapeIndex;
    public float Mass;
    public float CollisionMargin;
}

// Components/PhysicsVelocity.cs
using System.Numerics;

namespace Kilo.Physics;

public struct PhysicsVelocity
{
    public Vector3 Linear;
    public Vector3 Angular;
}

// Components/PhysicsCollider.cs
namespace Kilo.Physics;

public struct PhysicsCollider
{
    public int ColliderId;
    public bool IsTrigger;
}
```

### 3.4 资源

```csharp
// Resources/PhysicsWorld.cs
using BepuPhysics;
using BepuUtilities.Memory;

namespace Kilo.Physics;

public sealed class PhysicsWorld : IDisposable
{
    public Simulation Simulation { get; }
    public BufferPool BufferPool { get; }

    public PhysicsWorld(PhysicsSettings settings)
    {
        BufferPool = new BufferPool();
        Simulation = Simulation.Create(BufferPool,
            new KiloNarrowPhaseCallbacks(),
            new KiloPoseIntegratorCallbacks(settings.Gravity),
            new SolveDescription(settings.VelocityIterations, settings.SubstepCount));
    }

    public void Step(float deltaTime, int threadCount = 0)
    {
        using var dispatcher = threadCount > 0
            ? new SimpleThreadDispatcher(threadCount)
            : new SimpleThreadDispatcher(Environment.ProcessorCount);
        Simulation.Timestep(deltaTime, dispatcher);
    }

    public void Dispose()
    {
        BufferPool.Clear();
    }
}

// Resources/PhysicsSettings.cs
using System.Numerics;

namespace Kilo.Physics;

public sealed class PhysicsSettings
{
    public Vector3 Gravity { get; set; } = new(0f, -9.81f, 0f);
    public int VelocityIterations { get; set; } = 8;
    public int SubstepCount { get; set; } = 2;
    public float FixedTimestep { get; set; } = 1f / 60f;
}
```

### 3.5 PhysicsPlugin

```csharp
// PhysicsPlugin.cs
using Kilo.ECS;

namespace Kilo.Physics;

public sealed class PhysicsPlugin : IKiloPlugin
{
    private readonly PhysicsSettings _settings;

    public PhysicsPlugin(PhysicsSettings? settings = null)
    {
        _settings = settings ?? new PhysicsSettings();
    }

    public void Build(KiloApp app)
    {
        var physicsWorld = new PhysicsWorld(_settings);

        app.AddResource(_settings);
        app.AddResource(physicsWorld);

        // PreUpdate：将 ECS 变换同步到 Bepu
        app.AddSystem(KiloStage.PreUpdate, new SyncToPhysicsSystem().Update);

        // Update：执行物理模拟步进
        app.AddSystem(KiloStage.Update, world =>
        {
            var pw = // 获取 PhysicsWorld 资源
            pw.Step(_settings.FixedTimestep);
        });

        // PostUpdate：将 Bepu 刚体同步回 ECS
        app.AddSystem(KiloStage.PostUpdate, new SyncFromPhysicsSystem().Update);
    }
}
```

### 3.6 测试项目

**路径**：`tests/Kilo.Physics.Tests/Kilo.Physics.Tests.csproj`

**测试**：
- `ComponentTests.cs` — 组件结构体布局、默认值
- `PhysicsWorldTests.cs` — Simulation 创建、步进、释放
- `PluginRegistrationTests.cs` — 插件注册资源和系统
- `SyncSystemTests.cs` — 使用已知值测试 Transform ↔ Bepu 刚体同步
- `PhysicsBodyFactoryTests.cs` — 创建静态/动态刚体

---

## 4. Kilo.Input 插件

### 4.1 项目文件

**路径**：`src/Kilo.Input/Kilo.Input.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Kilo.Input</RootNamespace>
    <AssemblyName>Kilo.Input</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kilo.ECS\Kilo.ECS.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Silk.NET.Input" Version="*" />
    <PackageReference Include="Silk.NET.Windowing" Version="*" />
  </ItemGroup>
</Project>
```

### 4.2 项目结构

```
src/Kilo.Input/
├── Kilo.Input.csproj
├── Components/
│   └── InputReceiver.cs         // 标签组件：实体接收输入
├── Resources/
│   ├── InputState.cs            // 当前帧键盘/鼠标状态
│   └── InputSettings.cs         // 死区、灵敏度
├── Systems/
│   └── InputPollSystem.cs       // 轮询 Silk.NET 输入，更新 InputState
├── InputPlugin.cs               // IKiloPlugin 实现
└── InputState.cs                // 键盘按键、鼠标位置/按钮、手柄
```

### 4.3 组件与资源

```csharp
// Components/InputReceiver.cs
namespace Kilo.Input;

public struct InputReceiver { }

// Resources/InputState.cs
using System.Numerics;

namespace Kilo.Input;

public sealed class InputState
{
    // 键盘
    public bool[] KeysDown { get; } = new bool[512];
    public bool[] KeysPressed { get; } = new bool[512];   // 本帧刚按下
    public bool[] KeysReleased { get; } = new bool[512];   // 本帧刚释放

    // 鼠标
    public Vector2 MousePosition;
    public Vector2 MouseDelta;
    public bool[] MouseButtonsDown { get; } = new bool[5];
    public float ScrollDelta;

    // 工具方法
    public bool IsKeyDown(int key) => KeysDown[key];
    public bool IsKeyPressed(int key) => KeysPressed[key];
    public bool IsKeyReleased(int key) => KeysReleased[key];
    public bool IsMouseButtonDown(int button) => MouseButtonsDown[button];
}

// Resources/InputSettings.cs
namespace Kilo.Input;

public sealed class InputSettings
{
    public float MouseSensitivity { get; set; } = 1.0f;
    public float GamepadDeadZone { get; set; } = 0.1f;
}
```

### 4.4 InputPlugin

```csharp
// InputPlugin.cs
using Kilo.ECS;

namespace Kilo.Input;

public sealed class InputPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddResource(new InputState());
        app.AddResource(new InputSettings());

        // First 阶段：在任何游戏逻辑之前轮询输入
        app.AddSystem(KiloStage.First, new InputPollSystem().Update);
    }
}
```

### 4.5 测试项目

**路径**：`tests/Kilo.Input.Tests/Kilo.Input.Tests.csproj`

**测试**：
- `ComponentTests.cs` — InputReceiver 标签组件
- `InputStateTests.cs` — 默认状态、按键查询方法
- `PluginRegistrationTests.cs` — 插件注册资源/系统
- `InputPollSystemTests.cs` — 帧状态转换（按下→按住→释放）

---

## 5. Kilo.Assets 插件

### 5.1 项目文件

**路径**：`src/Kilo.Assets/Kilo.Assets.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Kilo.Assets</RootNamespace>
    <AssemblyName>Kilo.Assets</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kilo.ECS\Kilo.ECS.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.*" />
    <PackageReference Include="System.Text.Json" Version="*" />
  </ItemGroup>
</Project>
```

注意：AssimpNetter（3D 模型加载）推迟到阶段 4/5，届时我们会有真正的网格管线。阶段 3 专注于图像加载 + JSON 序列化。

### 5.2 项目结构

```
src/Kilo.Assets/
├── Kilo.Assets.csproj
├── Components/
│   └── AssetReference.cs        // 已加载资源的句柄
├── Resources/
│   ├── AssetManager.cs          // 集中式资源加载/缓存
│   └── AssetSettings.cs         // 根路径、热重载标志
├── Loaders/
│   ├── IAssetLoader.cs          // 通用资源加载器接口
│   ├── TextureLoader.cs         // 通过 ImageSharp 加载图像
│   └── JsonLoader.cs            // JSON 反序列化
├── Systems/
│   └── AssetLoadSystem.cs       // 处理待加载资源
├── AssetsPlugin.cs              // IKiloPlugin 实现
└── AssetHandle.cs               // 已加载资源的类型化句柄
```

### 5.3 组件与资源

```csharp
// AssetHandle.cs
namespace Kilo.Assets;

public readonly struct AssetHandle<T>
{
    public readonly int Id;
    public AssetHandle(int id) => Id = id;
    public bool IsValid => Id >= 0;
}

// Components/AssetReference.cs
namespace Kilo.Assets;

public struct AssetReference
{
    public int AssetId;
    public string Path;
    public bool IsLoaded;
}

// Resources/AssetManager.cs
using System.Collections.Concurrent;

namespace Kilo.Assets;

public sealed class AssetManager
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Dictionary<int, object> _assets = new();
    private int _nextId;

    public AssetHandle<T> Load<T>(string path) where T : class
    {
        if (_cache.TryGetValue(path, out var cached))
            return new AssetHandle<T>(_cacheEntryId(path));

        // 实际加载委托给 IAssetLoader<T>
        // 目前返回一个加载系统将填充的句柄
        var id = System.Threading.Interlocked.Increment(ref _nextId);
        return new AssetHandle<T>(id);
    }

    public T? Get<T>(AssetHandle<T> handle) where T : class
    {
        return _assets.TryGetValue(handle.Id, out var asset) ? (T)asset : null;
    }

    public void Store<T>(AssetHandle<T> handle, T asset) where T : class
    {
        _assets[handle.Id] = asset;
    }

    public void Clear()
    {
        _assets.Clear();
        _cache.Clear();
    }
}
```

### 5.4 加载器

```csharp
// Loaders/IAssetLoader.cs
namespace Kilo.Assets;

public interface IAssetLoader<T> where T : class
{
    T Load(string path);
    string[] SupportedExtensions { get; }
}
```

### 5.5 AssetsPlugin

```csharp
// AssetsPlugin.cs
using Kilo.ECS;

namespace Kilo.Assets;

public sealed class AssetsPlugin : IKiloPlugin
{
    private readonly AssetSettings _settings;

    public AssetsPlugin(AssetSettings? settings = null)
    {
        _settings = settings ?? new AssetSettings();
    }

    public void Build(KiloApp app)
    {
        app.AddResource(_settings);
        app.AddResource(new AssetManager());

        // 启动：注册加载器
        app.AddSystem(KiloStage.Startup, world =>
        {
            // 加载器注册通过 AssetManager 进行
        });

        // PreUpdate：处理待加载资源
        app.AddSystem(KiloStage.PreUpdate, new AssetLoadSystem().Update);
    }
}
```

### 5.6 测试项目

**路径**：`tests/Kilo.Assets.Tests/Kilo.Assets.Tests.csproj`

**测试**：
- `AssetHandleTests.cs` — 句柄创建、有效性、相等性
- `AssetManagerTests.cs` — 加载/获取/存储/清除生命周期
- `AssetReferenceTests.cs` — 组件默认值
- `PluginRegistrationTests.cs` — 插件注册资源/系统
- `JsonLoaderTests.cs` — 加载 JSON 文件（嵌入的测试数据）

---

## 6. 解决方案集成

### 6.1 解决方案文件更新

添加到 `Kilo.slnx`：

```xml
<Folders Name="/src/">
  <!-- 已有 -->
  <Project Path="src/Kilo.Rendering/Kilo.Rendering.csproj" />
  <Project Path="src/Kilo.Physics/Kilo.Physics.csproj" />
  <Project Path="src/Kilo.Input/Kilo.Input.csproj" />
  <Project Path="src/Kilo.Assets/Kilo.Assets.csproj" />
</Folder>
<Folder Name="/tests/">
  <!-- 已有 -->
  <Project Path="tests/Kilo.Rendering.Tests/Kilo.Rendering.Tests.csproj" />
  <Project Path="tests/Kilo.Physics.Tests/Kilo.Physics.Tests.csproj" />
  <Project Path="tests/Kilo.Input.Tests/Kilo.Input.Tests.csproj" />
  <Project Path="tests/Kilo.Assets.Tests/Kilo.Assets.Tests.csproj" />
</Folder>
```

### 6.2 Directory.Build.props

已在 `kilo/Directory.Build.props` 中配置 `net10.0` 目标 — 所有新项目继承此配置。

---

## 7. 架构规则

1. **插件仅引用 `Kilo.ECS`** — 永不直接引用 TinyEcs
2. **组件是结构体** — ECS 要求，无例外
3. **资源是 sealed class** — `RenderContext`、`PhysicsWorld`、`InputState`、`AssetManager`
4. **系统是带有 `Update(KiloWorld)` 方法的类** — 兼容 `AddSystem(KiloStage, Action<KiloWorld>)` API
5. **无跨插件引用** — 每个插件独立
6. **阶段放置约定**：
   - `Startup`：资源/设备初始化
   - `First`：输入轮询
   - `PreUpdate`：同步 ECS → 外部（物理）、资源加载
   - `Update`：模拟步进（物理）、游戏逻辑
   - `PostUpdate`：同步外部 → ECS（物理）
   - `Last`：渲染

---

## 8. 实现顺序

1. **Kilo.Rendering** — 基础；定义所有插件共用的 Transform 组件
2. **Kilo.Physics** — 依赖 Rendering 的 Transform 约定
3. **Kilo.Input** — 独立；轻量级
4. **Kilo.Assets** — 独立；图像加载 + JSON

每个插件由专门的实现 Agent 完成，同时由测试 Agent 并行编写测试。

---

## 9. NuGet 包版本

| 包 | 版本 | 所属插件 |
|---|------|---------|
| Veldrid | 4.8.0 | Rendering |
| Veldrid.StartupUtilities | 4.8.0 | Rendering |
| Veldrid.SPIRV | 1.0.13 | Rendering |
| BepuPhysics | 2.5.0-beta.28 | Physics |
| BepuUtilities | 2.5.0-beta.28 | Physics |
| Silk.NET.Input | latest | Input |
| Silk.NET.Windowing | latest | Input |
| SixLabors.ImageSharp | 3.1.* | Assets |
