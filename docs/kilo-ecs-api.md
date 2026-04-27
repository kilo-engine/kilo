# Kilo.ECS 用户接口文档

> 命名空间：`Kilo.ECS`
> 底层引擎：TinyEcs（Bevy 风格 ECS）
> 设计哲学：除 ECS 核心外，一切皆为插件。

---

## 目录

- [1. 快速开始](#1-快速开始)
- [2. KiloApp — 应用框架](#2-kiloapp--应用框架)
- [3. KiloStage — 执行阶段](#3-kilostage--执行阶段)
- [4. IKiloPlugin — 插件接口](#4-ikiloplugin--插件接口)
- [5. KiloWorld — 世界容器](#5-kiloworld--世界容器)
- [6. KiloEntity — 实体句柄](#6-kiloentity--实体句柄)
- [7. EntityId — 实体标识](#7-entityid--实体标识)
- [8. 查询系统](#8-查询系统)
- [9. 资源管理](#9-资源管理)
- [10. 状态机](#10-状态机)
- [11. Bundle — 组件组](#11-bundle--组件组)
- [12. 观察者/触发器](#12-观察者触发器)
- [13. 原始类型](#13-原始类型)
- [14. 典型用法示例](#14-典型用法示例)

---

## 1. 快速开始

```csharp
using Kilo.ECS;

// 创建应用
var app = new KiloApp();

// 注册资源和系统
app.AddResource(new GameConfig());
app.AddSystem(KiloStage.Startup, world => { /* 初始化场景 */ });
app.AddSystem(KiloStage.Update, world => { /* 游戏逻辑 */ });

// 启动主循环
app.Run();
```

---

## 2. KiloApp — 应用框架

应用的主入口。负责管理插件、系统注册和执行循环。

```csharp
public class KiloApp
```

### 构造函数

| 签名 | 说明 |
|---|---|
| `KiloApp(ThreadingMode threadingMode = ThreadingMode.Auto)` | 创建新应用（含新 World） |
| `KiloApp(KiloWorld world, ThreadingMode threadingMode = ThreadingMode.Auto)` | 包装已有 World |

### 属性

| 属性 | 类型 | 说明 |
|---|---|---|
| `World` | `KiloWorld` | 底层 World（延迟创建） |

### 资源与状态

| 方法 | 说明 |
|---|---|
| `AddResource<T>(T resource)` | 注册全局资源，返回 `this` 支持链式调用 |
| `AddState<TState>(TState initial)` | 注册枚举状态机，返回 `this` |

### 插件

| 方法 | 说明 |
|---|---|
| `AddPlugin(IKiloPlugin plugin)` | 注册插件（调用 `plugin.Build(this)`），返回 `this` |

### 阶段

| 方法 | 说明 |
|---|---|
| `AddStage(KiloStage stage)` | 添加自定义阶段，返回 `KiloStageConfigurator` |

### 系统

| 方法 | 说明 |
|---|---|
| `AddSystem(KiloStage stage, Action<KiloWorld> system)` | 注册接收 `KiloWorld` 的系统，返回 `this` |
| `AddSystem(KiloStage stage, Action system)` | 注册无参数系统，返回 `this` |

### 执行

| 方法 | 说明 |
|---|---|
| `RunStartup()` | 仅运行 Startup 系统（一次性） |
| `Run()` | 启动主循环（首次调用自动执行 Startup） |
| `Update()` | 执行单帧更新 |

---

## 3. KiloStage — 执行阶段

系统按阶段分组，阶段之间按顺序执行。

```csharp
public sealed class KiloStage
```

### 预定义阶段（按执行顺序）

| 阶段 | 说明 |
|---|---|
| `Startup` | 仅运行一次（始终单线程） |
| `First` | 第一个常规阶段 |
| `PreUpdate` | 更新前 |
| `Update` | 主游戏逻辑 |
| `PostUpdate` | 更新后 |
| `Last` | 末尾阶段（渲染、清理等） |

### 方法

| 方法 | 说明 |
|---|---|
| `KiloStage.Custom(string name)` | 创建自定义阶段 |
| `Name` | 阶段名称 |

### 阶段排序（KiloStageConfigurator）

```csharp
app.AddStage(KiloStage.Custom("Physics"))
    .After(KiloStage.Update)
    .Before(KiloStage.PostUpdate)
    .Build();
```

---

## 4. IKiloPlugin — 插件接口

遵循 Bevy 哲学：除 ECS 外的一切功能都通过插件注册。

```csharp
public interface IKiloPlugin
{
    void Build(KiloApp app);
}
```

### 示例

```csharp
public sealed class PhysicsPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddResource(new PhysicsWorld());
        app.AddSystem(KiloStage.Update, StepPhysics);
    }

    private static void StepPhysics(KiloWorld world) { /* ... */ }
}
```

---

## 5. KiloWorld — 世界容器

ECS 核心容器。管理所有实体、组件、资源和查询。

```csharp
public sealed class KiloWorld : IDisposable
```

### 生命周期

| 属性/方法 | 说明 |
|---|---|
| `CurrentTick` | 当前 tick（变更检测用） |
| `EntityCount` | 存活实体数量 |
| `IsDeferred` | 是否处于延迟模式 |
| `Update()` | 推进 tick，返回新 tick 值 |
| `Dispose()` | 释放资源 |

### 资源

| 方法 | 说明 |
|---|---|
| `GetResource<T>()` | 获取资源 |
| `GetResourceRef<T>()` | 获取可变引用 |
| `HasResource<T>()` | 检查资源是否存在 |
| `AddResource<T>(T resource)` | 添加资源 |

### 实体创建

| 方法 | 说明 |
|---|---|
| `Entity(ulong id = 0)` | 创建或获取实体（`id=0` 自动生成新 ID） |
| `Entity<T>()` | 获取/创建与组件类型关联的实体 |
| `Entity(string name)` | 创建或获取命名实体 |

### 实体生命周期

| 方法 | 说明 |
|---|---|
| `Delete(EntityId entity)` | 删除实体及其子实体 |
| `Exists(EntityId entity)` | 检查实体是否存活 |

### 组件操作

| 方法 | 说明 |
|---|---|
| `Set<T>(EntityId entity, T component)` | 设置组件 |
| `Unset<T>(EntityId entity)` | 移除组件/标签 |
| `Has<T>(EntityId entity)` | 检查是否有组件/标签 |
| `Get<T>(EntityId entity)` | 获取组件的可变引用 |

### 变更检测

| 方法 | 说明 |
|---|---|
| `SetChanged<T>(EntityId entity)` | 标记组件已变更 |

### 实体信息

| 方法 | 说明 |
|---|---|
| `GetType(EntityId id)` | 获取原型签名（`ReadOnlySpan<ComponentInfo>`） |
| `Name(EntityId id)` | 获取实体名称 |
| `GetAlive(EntityId id)` | 解析为当前存活的版本 |

### 延迟操作

| 方法 | 说明 |
|---|---|
| `Deferred(Action<KiloWorld> fn)` | 在延迟块中执行操作 |
| `BeginDeferred()` | 进入延迟模式 |
| `EndDeferred()` | 退出延迟模式并合并操作 |

### 查询

| 方法 | 说明 |
|---|---|
| `QueryBuilder()` | 创建查询构建器 |

### 事件

| 事件 | 说明 |
|---|---|
| `OnEntityCreated` | 实体创建时触发 |
| `OnEntityDeleted` | 实体删除时触发 |
| `OnComponentSet` | 组件被设置时触发 |
| `OnComponentUnset` | 组件被移除时触发 |
| `OnComponentAdded` | 组件首次添加时触发 |

---

## 6. KiloEntity — 实体句柄

轻量级实体引用（`readonly ref struct`），零开销。

```csharp
public readonly ref struct KiloEntity
```

### 属性

| 属性 | 说明 |
|---|---|
| `Id` | 实体唯一标识 |
| `Generation` | 世代计数器 |
| `Invalid` | 无效实体句柄（静态） |

### 组件操作（链式）

| 方法 | 说明 |
|---|---|
| `Set<T>(T component)` | 设置组件，返回 `this` |
| `Unset<T>()` | 按类型移除组件/标签 |
| `Get<T>()` | 获取组件引用 |
| `Has<T>()` | 检查是否有组件/标签 |

### 层级

| 方法 | 说明 |
|---|---|
| `AddChild(KiloEntity child)` | 添加子实体 |
| `RemoveChild(KiloEntity child)` | 移除子实体 |

### 生命周期

| 方法 | 说明 |
|---|---|
| `Delete()` | 删除此实体 |
| `Exists()` | 检查是否存活 |

### 类型转换

```csharp
// 隐式转换为 EntityId
EntityId id = entity;

// 比较操作
if (entityA == entityB) { /* ... */ }
```

---

## 7. EntityId — 实体标识

轻量级实体 ID（`readonly struct`，底层为 `ulong`）。

```csharp
public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
```

| 成员 | 说明 |
|---|---|
| `Value` | 原始 `ulong` 值 |
| `IsValid()` | 是否有效（非零） |
| `RealId()` | 提取实际 ID（低 32 位） |
| `Generation()` | 提取世代计数（高 16 位） |
| `implicit operator ulong` | 隐式转 `ulong` |
| `implicit operator EntityId` | 隐式从 `ulong` 转换 |

---

## 8. 查询系统

### KiloQueryBuilder

构建查询。流式 API。

```csharp
public sealed class KiloQueryBuilder
```

| 方法 | 说明 |
|---|---|
| `With<T>()` | 要求包含组件 T |
| `Without<T>()` | 排除包含组件 T 的实体 |
| `Optional<T>()` | 标记组件 T 为可选 |
| `Build()` | 构建查询，返回缓存的 `KiloQuery` |

### KiloQuery

已缓存的查询实例。

```csharp
public sealed class KiloQuery
```

| 方法 | 说明 |
|---|---|
| `Count()` | 匹配的实体数量 |
| `Iter()` | 获取迭代器 |
| `Iter(EntityId entity)` | 获取特定实体的迭代器 |

### KiloQueryIterator

查询结果迭代器（`ref struct`），按 Archetype 块遍历。

```csharp
public ref struct KiloQueryIterator
```

| 成员 | 说明 |
|---|---|
| `Count` | 当前块中的实体数量 |
| `Next()` | 移动到下一个 Archetype 块 |
| `Entities()` | 当前块的实体视图 |
| `Data<T>(int index)` | 按列索引获取组件数据 Span |
| `GetColumnIndexOf<T>()` | 获取组件类型的列索引 |
| `GetColumn<T>(int index)` | 获取 DataRow 访问器 |
| `GetChangedTicks(int index)` | 获取变更 tick |
| `GetAddedTicks(int index)` | 获取添加 tick |

### 查询示例

```csharp
var query = world.QueryBuilder()
    .With<Position>()
    .With<Velocity>()
    .Without<Static>()
    .Build();

var iter = query.Iter();
while (iter.Next())
{
    var positions = iter.Data<Position>(iter.GetColumnIndexOf<Position>());
    var velocities = iter.Data<Velocity>(iter.GetColumnIndexOf<Velocity>());
    for (int i = 0; i < iter.Count; i++)
        positions[i].Value += velocities[i].Value * dt;
}
```

---

## 9. 资源管理

资源是全局单例，不绑定到实体。通常用于全局配置、共享状态。

```csharp
// 注册
app.AddResource(new RenderSettings { Width = 1280, Height = 720 });

// 读取
var settings = world.GetResource<RenderSettings>();

// 可变引用
ref var settings = ref world.GetResourceRef<RenderSettings>();
settings.BloomEnabled = true;

// 检查存在
if (world.HasResource<RenderSettings>()) { /* ... */ }
```

---

## 10. 状态机

基于枚举的状态机，支持帧末延迟切换。

```csharp
// 定义状态
public enum GameState { Menu, Loading, Playing, Paused }

// 注册
app.AddState(GameState.Menu);

// 读取当前状态
var state = world.GetResource<State<GameState>>();
if (state.Current == GameState.Playing) { /* ... */ }
if (state.IsChanged) { /* ... */ }

// 切换状态（帧末生效）
var next = world.GetResource<NextState<GameState>>();
next.Set(GameState.Playing);
```

| 类型 | 属性/方法 | 说明 |
|---|---|---|
| `State<T>` | `.Current` | 当前状态 |
| `State<T>` | `.Previous` | 上一个状态 |
| `State<T>` | `.IsChanged` | 本帧是否发生了变化 |
| `NextState<T>` | `.Set(T)` | 队列状态切换 |
| `NextState<T>` | `.IsQueued` | 是否有待切换 |

---

## 11. Bundle — 组件组

将相关组件打包，简化实体创建。

```csharp
public interface IKiloBundle
{
    void Insert(KiloEntity entity);
}

// 使用
public struct PlayerBundle : IKiloBundle
{
    public Position Position;
    public Velocity Velocity;
    public Health Health;

    public void Insert(KiloEntity entity)
    {
        entity.Set(Position).Set(Velocity).Set(Health);
    }
}

// 创建实体并插入 bundle
var player = world.Entity()
    .Set(new PlayerTag())
    .InsertBundle(new PlayerBundle { Position = new(0, 0, 0), ... });
```

---

## 12. 观察者/触发器

用于事件驱动的组件变更通知。

| 接口 | 说明 |
|---|---|
| `IKiloTrigger` | 基础触发器标记接口 |
| `IKiloEntityTrigger` | 携带实体 ID 的触发器（`.EntityId`） |
| `IKiloPropagatingTrigger` | 可沿层级向上传播的触发器（`.ShouldPropagate`） |

---

## 13. 原始类型

### ComponentInfo

```csharp
public readonly struct ComponentInfo
{
    public ulong Id { get; }    // 组件 ID
    public int Size { get; }    // 数据大小（字节），0 表示标签
}
```

### Ptr\<T\> / PtrRO\<T\>

组件数据指针（`ref struct`）。

```csharp
public ref struct Ptr<T> where T : struct
{
    public ref T Ref { get; }     // 可变引用
    public bool IsValid();        // 是否有效
}

public readonly ref struct PtrRO<T> where T : struct
{
    public ref readonly T Ref { get; }  // 只读引用
}
```

### DataRow\<T\>

查询列的逐行访问器。

```csharp
public ref struct DataRow<T> where T : struct
{
    public Ptr<T> Value { get; }  // 当前元素指针
    public nint Size { get; }     // 每元素字节数
    public void Next();           // 前进到下一元素
}
```

---

## 14. 典型用法示例

### 完整游戏初始化

```csharp
var app = new KiloApp();

// 插件
app.AddPlugin(new WindowPlugin());
app.AddPlugin(new PhysicsPlugin());
app.AddPlugin(new RenderingPlugin());

// 状态
app.AddState(GameState.Menu);

// Startup — 初始化场景
app.AddSystem(KiloStage.Startup, world =>
{
    var player = world.Entity("Player");
    player.Set(new Position(0, 1, 0))
          .Set(new Velocity())
          .Set(new Health { Value = 100 })
          .AddChild(world.Entity().Set(new Weapon { Damage = 25 }));
});

// Update — 游戏逻辑
app.AddSystem(KiloStage.Update, world =>
{
    var input = world.GetResource<InputState>();
    var dt = world.GetResource<DeltaTime>().Value;

    var query = world.QueryBuilder()
        .With<Position>()
        .With<Velocity>()
        .Build();

    var iter = query.Iter();
    while (iter.Next())
    {
        var pos = iter.Data<Position>(iter.GetColumnIndexOf<Position>());
        var vel = iter.Data<Velocity>(iter.GetColumnIndexOf<Velocity>());
        for (int i = 0; i < iter.Count; i++)
            pos[i].Value += vel[i].Value * dt;
    }
});

// 运行
app.Run();
```

### 延迟操作（在遍历中安全删除实体）

```csharp
world.Deferred(w =>
{
    var query = w.QueryBuilder().With<Destroyed>().Build();
    var iter = query.Iter();
    while (iter.Next())
    {
        var entities = iter.Entities();
        for (int i = 0; i < iter.Count; i++)
            w.Delete(entities[i].Id);
    }
});
```

### ThreadingMode

```csharp
public enum ThreadingMode
{
    Auto,    // 根据 CPU 核心数自动决定
    Single,  // 强制单线程
    Multi    // 启用多线程
}
```

---

> 共 **22** 个公开类型：6 个 class、1 个 enum、4 个 interface、4 个 struct、4 个 ref struct、1 个 static class、2 个泛型 class。
