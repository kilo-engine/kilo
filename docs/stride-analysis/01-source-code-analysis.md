# Stride 引擎源码分析文档

> 基于 Stride (formerly Xenko) 开源引擎源码深入分析  
> 源码来源：https://github.com/stride3d/stride

---

## 一、项目概述

Stride 是一个由 .NET Foundation 支持的开源跨平台 C# 游戏引擎，前身为 Silicon Studio 开发的 Xenko。引擎采用高度模块化的架构设计，支持 Windows、Linux、Android、iOS 等平台，底层图形 API 支持 Direct3D 11/12、Vulkan、OpenGL。源码采用 MIT 协议开源，整个仓库是一个大型 .NET 解决方案，包含引擎运行时、编辑器（Game Studio）、资产管道、着色器编译器、物理/音频/输入等子系统。

---

## 二、目录结构与工程组织

```
stride-repo/
├── sources/           # 主源码（引擎核心、运行时、编辑器、工具）
├── samples/           # 示例项目（Audio、Graphics、Input、Physics、UI 等）
├── tests/             # 回归测试与集成测试
├── build/             # 构建脚本、解决方案文件、MSBuild SDK
├── deps/              # 外部依赖
├── docs/              # 构建文档
└── global.json        # .NET SDK 版本锁定（net10.0）
```

### 2.1 解决方案分层

`build/Stride.sln` 通过 Solution Folder 将项目按逻辑分层：

| 文件夹 | 说明 |
|--------|------|
| 10-CoreRuntime | 核心运行时库（Stride.Core.*） |
| 20-StrideRuntime | 引擎运行时（Stride.Engine、Stride.Games、Stride.Graphics 等） |
| 30-CoreDesign | 设计时核心 |
| 40-Assets | 资产编译与导入 |
| 50-Presentation | WPF / Avalonia 编辑器 UI 框架 |
| 60-Editor | Game Studio 编辑器本体 |
| 70-StrideAssets | 引擎专用资产类型 |
| 80-Shaders | 着色器语言与编译器 |

### 2.2 sources/ 关键项目

#### 核心层 (core/)

| 项目 | 功能 |
|------|------|
| **Stride.Core** | 引用计数、PropertyContainer/PropertyKey、底层序列化、NativeStream、ServiceRegistry、ComponentBase |
| **Stride.Core.Mathematics** | 数学库（Vector2/3/4、Matrix、Quaternion、BoundingBox、BoundingFrustum 等），**不依赖 Stride.Core** |
| **Stride.Core.IO** | 虚拟文件系统 VirtualFileSystem |
| **Stride.Core.Serialization** | 二进制序列化、git-like CAS 存储 ObjectDatabase |
| **Stride.Core.MicroThreading** | 基于 C# async/await 的微线程库 |
| **Stride.Core.AssemblyProcessor** | MSBuild 时 IL 编织器，自动生成序列化器、Module Initializer、内存固定辅助代码 |
| **Stride.Core.CompilerServices** | Roslyn 分析器 |

#### 运行时层 (engine/)

| 项目 | 功能 |
|------|------|
| **Stride.Engine** | ECS（Entity/EntityComponent/EntityProcessor）、Scene、ScriptComponent、Game |
| **Stride.Games** | GameBase、GameSystemBase、GamePlatform、游戏主循环（Tick/Update/Draw） |
| **Stride.Graphics** | 底层图形抽象：GraphicsDevice、CommandList、Texture、Buffer、PipelineState，后端实现 D3D11/D3D12/Vulkan/Null |
| **Stride.Rendering** | 高层渲染架构：RenderSystem、RenderFeature、RenderStage、RenderView、Material、Mesh、GraphicsCompositor |
| **Stride.Audio** | 音频引擎抽象（AudioEngine、Sound、SoundInstance、AudioEmitter/Listener） |
| **Stride.Input** | 输入抽象（InputManager、键盘/鼠标/手柄/触摸/传感器） |
| **Stride.Physics** | Bullet 物理集成（Simulation、ColliderShape、RigidbodyComponent） |
| **Stride.BepuPhysics** | BepuPhysics 2.5 替代物理方案 |
| **Stride.Particles** | 粒子系统 |
| **Stride.UI** | UI 渲染与布局系统 |
| **Stride.Shaders** / **Stride.Shaders.Compiler** | SDSL 着色器解析、Effect 编译 |
| **Stride.Navigation** | 导航网格（DotRecast） |
| **Stride.VirtualReality** | VR 抽象与 OpenXR 集成 |

#### 编辑器层 (editor/)

- **Stride.GameStudio**：Game Studio 编辑器主程序
- **Stride.Assets.Presentation**：资产编辑器（模型、材质、场景等）
- 基于 Avalonia 与 WPF 构建

#### 构建 SDK (sdk/)

三个自定义 MSBuild SDK 包：

- **Stride.Build.Sdk**：平台探测、图形 API 多目标构建、Assembly Processor
- **Stride.Build.Sdk.Editor**：编辑器项目专用属性
- **Stride.Build.Sdk.Tests**：xUnit 集成、测试启动器代码生成

所有项目直接从源码导入 SDK，而非通过 NuGet：

```xml
<Import Project="$([MSBuild]::GetDirectoryNameOfFileAbove(...))/sdk/Stride.Build.Sdk/Sdk/Sdk.props" />
...
<Import Project="$(StrideRoot)sources/sdk/Stride.Build.Sdk/Sdk/Sdk.targets" />
```

---

## 三、核心架构模式

### 3.1 Entity-Component-System (ECS)

Stride 的 ECS 实现是引擎最核心的架构模式，代码集中在 `Stride.Engine` 中。

#### Entity（实体）

```csharp
public sealed class Entity : ComponentBase, IEnumerable<EntityComponent>, IIdentifiable
{
    public Guid Id { get; set; }
    public EntityComponentCollection Components { get; }
    public TransformComponent Transform => TransformValue;
    
    public T Get<T>() where T : EntityComponent;
    public T GetOrCreate<T>() where T : EntityComponent, new();
    public void Add(EntityComponent component);
    public void Remove<T>() where T : EntityComponent;
}
```

- `Entity` 为 `sealed` 类，继承自 `ComponentBase`（引用计数对象）
- 每个 Entity 默认自带一个 `TransformComponent`
- 通过泛型方法 `Get<T>()` / `GetOrCreate<T>()` 访问组件
- Entity 必须属于一个 `Scene`

#### EntityComponent（组件）

```csharp
[DataContract(Inherited = true)]
public abstract class EntityComponent : IIdentifiable
{
    public Entity Entity { get; internal set; }
    public Guid Id { get; set; } = Guid.NewGuid();
}
```

- 所有组件继承 `EntityComponent`
- 标记 `[DataContract]` 用于二进制序列化
- 序列化时通过自定义 `Serializer` 确保所属 Entity 被正确引用

#### EntityProcessor（处理器/系统）

```csharp
public abstract class EntityProcessor
{
    public virtual void Update(GameTime time);
    public virtual void Draw(RenderContext context);
}

public abstract class EntityProcessor<TComponent, TData> : EntityProcessor
    where TComponent : EntityComponent
{
    protected abstract TData GenerateComponentData(Entity entity, TComponent component);
    protected override void OnEntityComponentAdding(...);
    protected override void OnEntityComponentRemoved(...);
}
```

- 这是 ECS 中 "System" 的实现
- `EntityProcessor<TComponent, TData>` 自动追踪包含特定组件类型的实体
- 通过 `[DefaultEntityComponentProcessor(typeof(MyProcessor))]` 属性实现**自动发现**
- `EntityManager` 在组件类型注册时扫描该属性并自动实例化对应 Processor

#### EntityManager / SceneInstance

```csharp
public class EntityManager
{
    public void Update(GameTime time);   // 遍历所有 Processor.Update()
    public void Draw(RenderContext context); // 遍历所有 Processor.Draw()
}

public sealed class SceneInstance : EntityManager
{
    public Scene RootScene { get; set; }
    public TrackingCollection<VisibilityGroup> VisibilityGroups { get; }
}
```

- `SceneInstance` 是 `EntityManager` 的特化，管理一个 `Scene` 下的所有实体
- 负责维护 `VisibilityGroup`（渲染可见性组）
- 支持嵌套 Scene（通过 `Scene.Children`）

### 3.2 Service Registry（服务注册表）

`ServiceRegistry`（位于 `Stride.Core`）是一个轻量级 IoC 容器：

```csharp
public class ServiceRegistry : IServiceRegistry
{
    public void AddService<T>(T obj);
    public T GetService<T>();
}
```

- `GameBase` 在构造时创建 `ServiceRegistry`
- 所有子系统向其中注册：`GraphicsDeviceManager`、`AudioSystem`、`InputManager` 等
- `ScriptComponent` 通过懒加载属性直接访问：`GraphicsDevice`、`Content`、`Input`、`SceneSystem`、`EffectSystem`、`Audio` 等

### 3.3 Game System 模式

```csharp
public abstract class GameSystemBase : ComponentBase, IUpdateable, IDrawable, IContentable
{
    public virtual void Update(GameTime time);
    public virtual void Draw(GameTime time);
}
```

- `GameSystemBase` 是引擎子系统的插件基类
- 典型实现：`SceneSystem`、`ScriptSystem`、`EffectSystem`、`AudioSystem`
- `GameBase` 维护 `GameSystems` 集合，在主循环中按优先级调用 `Update()` / `Draw()`

### 3.4 游戏主循环

`GameBase`（`Stride.Games`）的主循环简化如下：

```csharp
while (IsRunning)
{
    Tick();
}

void Tick()
{
    Update(gameTime);   // 更新逻辑
    Draw(gameTime);     // 渲染
}

void Update(GameTime time)
{
    foreach (var system in GameSystems)
        system.Update(time);
}

void Draw(GameTime time)
{
    foreach (var system in GameSystems)
        system.Draw(time);
}
```

- `SceneSystem` 在 `Update()` 中更新场景中的 Processors
- `SceneSystem` 在 `Draw()` 中驱动 `GraphicsCompositor` 执行渲染

---

## 四、序列化与资产管道

### 4.1 二进制序列化

- 使用 `[DataContract]` / `[DataMember]` 驱动二进制序列化
- `DataSerializer<T>` 负责具体类型的序列化逻辑
- **Assembly Processor** 在编译时自动为标记了 `[DataContract]` 的类型生成 `DataSerializer<T>` 实现

### 4.2 ContentManager 与 ObjectDatabase

```csharp
public class ContentManager
{
    public T Load<T>(string url);
    public void Save(string url, object obj);
}
```

- `ObjectDatabase` 提供类似 git 的 CAS（Content Addressable Storage）存储
- 运行时资产挂载在 `/asset` 虚拟路径下
- `GameSettings` 资产决定默认场景、图形配置、分辨率、Effect 编译模式

### 4.3 Asset Pipeline

- `Stride.Assets` 定义资产编译器框架
- `Stride.Assets.Models` 处理模型导入（FBX、glTF 等）
- 编译时资产通过 Build Engine 生成运行时二进制格式

---

## 五、着色器系统 (SDSL)

### 5.1 Stride Shading Language (SDSL)

- HLSL 扩展语法，支持**继承、mixin、自动 in-out 属性编织**
- 通过 `Stride.Core.Shaders`（基于 Irony 解析器）解析为 AST
- 运行时根据目标平台编译为：
  - HLSL（Direct3D）
  - GLSL（OpenGL）
  - SPIR-V（Vulkan）

### 5.2 Effect System

```csharp
public class EffectSystem : GameSystemBase
{
    public Task<Effect> LoadEffect(string effectName, CompilerParameters parameters);
}
```

- Effect 使用类 C# 语法组合 Shader
- 支持条件编译生成 Effect Permutation
- 由于 iOS/Android 不支持运行时着色器编译，使用 `.sdeffectlog` 文件枚举所有 Permutation
- 支持异步编译与磁盘缓存

---

## 六、平台抽象层

### 6.1 GamePlatform

运行时根据目标平台创建对应后端：

- **Desktop**（WinForms）
- **DesktopSDL**（基于 SDL2 的跨平台方案）
- **Android** / **iOS** / **UWP**

### 6.2 GraphicsDevice 后端

`GraphicsDevice`（`Stride.Graphics`）抽象了底层图形 API：

- **Direct3D 11**
- **Direct3D 12**
- **Vulkan**
- **Null**（无图形输出，用于服务器/测试）

设计上从用户视角尽量模拟 Direct3D 11 的行为，降低跨平台使用成本。

### 6.3 Input 抽象

`InputManager` 统一管理输入设备：

- `KeyboardDeviceBase`
- `MouseDeviceBase`
- `GamePadDeviceBase`
- `PointerDeviceBase`（触摸/笔）
- `Sensor`（加速度计、陀螺仪）

---

## 七、源码质量与工程实践

### 7.1 性能优化

- 广泛使用 `FastTrackingCollection`、`FastListStruct`、`ConcurrentCollector` 等自定义集合减少 GC
- `Dispatcher` 提供基于工作窃取的并行 For/ForEach
- 渲染管线 `Extract` 和 `Prepare` 阶段大量使用并行化
- 延迟渲染模式下支持多线程 Command List 录制

### 7.2 内存管理

- `ComponentBase` 提供引用计数机制
- `ObjectCollector` 用于脚本生命周期内的对象池管理
- `NativeStream` 提供非托管内存操作支持

### 7.3 编译时代码生成

- Assembly Processor 在构建时自动注入代码
- 避免运行时反射带来的性能损耗
- 自动生成序列化器、Module Initializer

---

## 八、总结

Stride 是一款架构清晰、模块化的现代 C# 游戏引擎：

1. **基础层** (`Stride.Core.*`) 提供序列化、数学、VFS、线程原语
2. **运行时层** (`Stride.Games` + `Stride.Engine`) 提供基于自动发现 ECS 的游戏循环与场景管理
3. **图形层** 分为底层 API 抽象 (`Stride.Graphics`) 和高层渲染架构 (`Stride.Rendering`)，采用多阶段并行渲染管线
4. **子系统**（音频、输入、物理、粒子、UI、VR）以服务形式接入游戏系统集合
5. **编辑器** (`Stride.GameStudio`) 直接复用运行时代码，保证所见即所得
6. **构建系统** 使用自定义 MSBuild SDK，处理图形 API 多目标、着色器代码生成、IL 编织、Native C++ 编译

源码整体风格统一，文档注释完善，适合作为中大型 3D 游戏项目的底层框架，也是学习现代游戏引擎架构的极佳教材。
