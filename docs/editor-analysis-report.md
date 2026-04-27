# Kilo 引擎 - 编辑器架构分析

**日期**：2026-04-09
**作者**：研究 Agent
**阶段**：阶段 2 - 文档与研究

---

## 概述

本报告分析了实现 Kilo 游戏引擎编辑器的四种方案：Dear ImGui (ImGui.NET)、Avalonia、Cysharp/R3 ViewModel（响应式）和基于 Veldrid 的自定义 UI。基于技术可行性、性能特征、开发工作量和与 Kilo 架构的一致性，本报告推荐 Dear ImGui (ImGui.NET) 作为主要方案，并考虑引入 R3 进行数据绑定的混合方案。

---

## 1. Dear ImGui (ImGui.NET)

### 概述

Dear ImGui 是一个为工具开发设计的即时模式 GUI 库。ImGui.NET 是提供 .NET 绑定的 C# 包装器。

### 关键特性

**即时模式渲染**：
- UI 在每一帧中从代码绘制
- 无保留模式或对象图
- 状态是显式且局部的
- 非常适合动态的、数据驱动的界面

**集成模型**：
- 作为游戏循环的一部分进行渲染
- 使用与游戏相同的图形上下文（兼容 Veldrid）
- 可以在游戏内或单独的编辑器窗口中渲染

### 技术分析

#### 成熟度和生态
- **状态**：非常成熟，在游戏开发中被广泛采用
- **.NET 支持**：ImGui.NET 提供 .NET 绑定，支持 .NET 10
- **文档**：丰富，有许多示例和教程
- **社区**：庞大，游戏开发中的活跃社区
- **2025 年使用**：在游戏引擎中持续保持相关性（[Noel Berry - 2025 年制作游戏](https://noelberry.ca/posts/making_games_in_2025/)）

#### 性能
- **渲染**：极其高效，为实时使用而设计
- **内存**：低内存占用，最小化分配
- **延迟**：近乎即时的 UI 更新
- **游戏集成**：可在与游戏相同的进程中运行

#### 开发体验
- **优点**：
  - 非常快速的原型设计和迭代
  - 简单的 API，易于学习
  - 无 UI 框架样板代码
  - 直接访问引擎数据
  - 可在游戏中用于调试
  - 在游戏开发中被广泛使用（经过验证的方法）

- **缺点**：
  - 无内置布局引擎（手动定位）
  - 与完整的 UI 框架相比，样式能力有限
  - 无 MVVM 或数据绑定
  - 需要手动状态管理
  - 不适合复杂的分层 UI
  - 无障碍功能有限

#### 与 Kilo 的集成

**直接 ECS 访问**：
```csharp
// ImGui 与 Kilo ECS 集成示例
void DrawEntityInspector(KiloWorld world, EntityId entity) {
    ImGui.Text($"实体：{entity.Id}");

    // 遍历组件
    foreach (var component in world.GetComponents(entity)) {
        ImGui.Separator();
        ImGui.Text(component.GetType().Name);

        // 绘制组件字段
        // （通过反射或代码生成）
    }
}
```

**Veldrid 集成**：
- ImGui.NET 提供 Veldrid 后端
- 可以渲染到与游戏相同的交换链
- 高效共享图形资源

### 可行性评估：**高**

Dear ImGui 是 Kilo 编辑器最可行的方案：

1. **在游戏开发中经过验证**：被游戏引擎和工具广泛使用
2. **性能**：适用于实时游戏编辑
3. **简洁性**：快速实现，最少的样板代码
4. **Veldrid 兼容**：与渲染直接集成
5. **游戏内调试**：既可作为编辑器也可作为游戏内调试器
6. **.NET 支持**：ImGui.NET 提供良好的 .NET 绑定

### 推荐使用场景
- 实体和组件检查器
- 场景层级
- 控制台和日志
- 性能分析
- 资源浏览器
- 属性编辑器
- 游戏内调试工具

---

## 2. Avalonia

### 概述

Avalonia 是一个基于 XAML 的跨平台 .NET UI 框架，类似于 WPF 但跨平台。

### 关键特性

**保留模式，MVVM 架构**：
- 基于 XAML 的声明式 UI
- MVVM（Model-View-ViewModel）模式
- 数据绑定和命令
- 丰富的样式和模板

**跨平台**：
- Windows、Linux、macOS
- 支持移动平台
- WebAssembly 支持

### 技术分析

#### 成熟度和生态
- **状态**：成熟，积极开发
- **.NET 支持**：支持 .NET 6+，预期支持 .NET 10
- **文档**：全面，有许多示例
- **社区**：庞大且活跃，尤其在跨平台桌面领域
- **在游戏开发中的使用**：游戏工具的采用在增加

#### 性能
- **渲染**：良好，但专为桌面应用设计
- **内存**：比即时模式 UI 占用更大
- **延迟**：对于工具可接受，可能高于 ImGui
- **游戏集成**：通常在单独的进程中运行

#### 开发体验
- **优点**：
  - 功能全面的 UI 框架
  - MVVM 模式和数据绑定
  - 丰富的样式和模板
  - 设计器工具（可视化编辑器）
  - 无障碍支持
  - 组件化架构
  - 大型控件库

- **缺点**：
  - 较陡的学习曲线（XAML、MVVM）
  - 更多样板代码和复杂性
  - 更高的内存占用
  - 较慢的开发迭代
  - 与游戏分离的进程（集成复杂）
  - 对简单工具来说过度设计
  - 游戏通信需要 IPC

#### 与 Kilo 的集成

**ViewModel 层**：
```csharp
// Avalonia ViewModel 示例 - ECS 实体
public class EntityViewModel : ViewModelBase
{
    private readonly KiloWorld _world;
    private readonly EntityId _entityId;

    public string Name { get; set; }
    public ObservableCollection<ComponentViewModel> Components { get; }

    public EntityViewModel(KiloWorld world, EntityId entityId)
    {
        _world = world;
        _entityId = entityId;
        // 从 ECS 数据初始化
    }
}
```

**IPC 通信**：
- 游戏进程通过命名管道、TCP 或 RPC 暴露 API
- 编辑器进程与游戏通信
- ECS 数据的序列化以供 UI 显示
- 增加了复杂性和延迟

### 可行性评估：**中**

Avalonia 可行但对 Kilo 有显著挑战：

1. **进程分离**：通常需要单独的编辑器进程
2. **IPC 开销**：与游戏的通信增加了复杂性和延迟
3. **过度设计**：完整的 MVVM 框架对游戏工具可能过度
4. **学习曲线**：XAML 和 MVVM 需要更多专业知识
5. **开发时间**：比 ImGui 实现时间更长
6. **游戏内调试**：无法轻松在游戏内运行

**最适合**：
- 复杂的、数据密集型工具（资源编辑器、关卡编辑器）
- 团队协作功能
- 生产管线
- 需要丰富 UI 的场景

**不适合**：
- 快速迭代和原型设计
- 游戏内调试
- 简单的检查器工具
- 实时性能分析

---

## 3. Cysharp/R3 响应式框架

### 概述

R3 是 Reactive Extensions (Rx) 的现代 C# 重新实现，为游戏开发优化。它提供了用于处理异步和基于事件操作的响应式编程模式。

### 关键特性

**响应式编程**：
- 可观察序列和观察者
- LINQ 风格的组合操作符
- 推送式数据流
- 可组合且声明式

**多平台支持**：
- Unity、Godot、Avalonia、WPF、WinForms、WinUI3、Stride、MAUI 等
- 零分配 LINQ 优化
- 为游戏和实时应用优化

### 技术分析

#### 什么是 R3？

来自 [GitHub 仓库](https://github.com/Cysharp/R3)：

> "dotnet/reactive 和 UniRx 的全新未来，支持包括 Unity、Godot、Avalonia、WPF、WinForms、WinUI3、Stride、LogicLooper、MAUI 在内的众多平台。"

**关键特性**：
- 第三代 Rx（Rx.NET 和 UniRx 的继任者）
- 遇错不停（与 Rx.NET 不同）
- 零分配 LINQ，包含 LINQ to Span、LINQ to SIMD、LINQ to Tree
- 为游戏和实时应用优化
- 平台特定的调度器和时间提供者

#### 成熟度和生态
- **状态**：积极开发，吸引力不断增长
- **.NET 支持**：支持现代 .NET，预期支持 .NET 10
- **文档**：持续增长，有示例和教程
- **社区**：活跃，尤其在游戏开发领域（Unity、Godot）
- **2025 年使用**：在现代游戏引擎和工具中使用

#### 性能
- **内存**：零分配优化
- **CPU**：LINQ to SIMD 和 Span 实现高效率
- **延迟**：低，为实时设计
- **GC 影响**：由于分配优化而最小

#### 开发体验
- **优点**：
  - LINQ 操作符的强大组合能力
  - 非常适合处理事件和状态变化
  - 声明式代码风格
  - 非常适合 UI 数据绑定场景
  - 平台特定的优化
  - 不断增长的生态

- **缺点**：
  - 响应式编程的学习曲线
  - 本身不是 UI 框架（需要 UI 层）
  - 与 Rx.NET 接口不同（不兼容）
  - 可能导致复杂的可观察链
  - 调试可能具有挑战性

### 如何将 ECS 数据转化为 ViewModel？

用户的想法是使用 R3 创建响应式 ViewModel 来观察 ECS 状态。以下是具体实现方式：

#### 方案 1：基于查询的可观察对象

```csharp
// 从 ECS 查询创建可观察对象
public class EntityObservable
{
    private readonly KiloWorld _world;

    public IObservable<IReadOnlyList<EntityId>> GetEntitiesWithComponent<T>()
        where T : struct
    {
        return Observable.Create<IReadOnlyList<EntityId>>(observer =>
        {
            // 初始查询
            var entities = _world.Query<T>().ToList();
            observer.OnNext(entities);

            // 订阅 ECS 事件（如果可用）
            // 或使用 Observable.Interval 进行轮询
            return Disposable.Empty;
        });
    }
}
```

#### 方案 2：组件变更可观察对象

```csharp
// 观察特定实体的组件变化
public class ComponentViewModel<T> where T : struct
{
    private readonly KiloWorld _world;
    private readonly EntityId _entityId;
    private readonly BehaviorSubject<T> _component;

    public IObservable<T> Component => _component.AsObservable();

    public ComponentViewModel(KiloWorld world, EntityId entityId)
    {
        _world = world;
        _entityId = entityId;
        _component = new BehaviorSubject<T>(world.GetComponent<T>(entityId));

        // 轮询变更或订阅 ECS 事件
        Observable.Interval(TimeSpan.FromMilliseconds(16))
            .Subscribe(_ => UpdateComponent());
    }

    private void UpdateComponent()
    {
        var current = _world.GetComponent<T>(_entityId);
        _component.OnNext(current);
    }
}
```

#### 方案 3：查询迭代器可观察对象

```csharp
// 观察查询结果的变化
public class QueryObservable<T> where T : struct
{
    private readonly KiloWorld _world;

    public IObservable<QueryResult<T>> ObserveQuery()
    {
        return Observable.Create<QueryResult<T>>(observer =>
        {
            var query = _world.Query<T>();

            // 定期或变更时发出结果
            var subscription = Observable.Interval(TimeSpan.FromMilliseconds(16))
                .Subscribe(_ =>
                {
                    var results = new QueryResult<T>(query);
                    observer.OnNext(results);
                });

            return subscription;
        });
    }
}
```

### GC 影响

R3 注重性能，但也有需要注意的地方：

#### 积极方面
- **零分配 LINQ**：许多操作符无分配
- **LINQ to Span**：尽可能使用栈分配的 span
- **LINQ to SIMD**：向量化的操作，分配最少
- **对象池**：内部复用对象

#### 潜在顾虑
- **观察者对象**：每次订阅创建观察者对象
- **可观察链**：复杂的链可能创建中间对象
- **事件订阅**：大量订阅增加对象数量
- **装箱**：值类型在某些场景中可能被装箱

#### 缓解策略
```csharp
// 使用值类型最小化装箱
public readonly struct EntityData
{
    public readonly EntityId Id;
    public readonly string Name;
}

// 使用 SingleAssignmentDisposable 减少分配
var subscription = new SingleAssignmentDisposable();
subscription.Disposable = source.Subscribe(...);

// 尽可能使用 ref struct
ref struct QueryResult<T> where T : struct
{
    public readonly Span<EntityId> Entities;
    public readonly Span<T> Components;
}
```

### 可行性评估：**中高**（作为数据层，而非 UI 框架）

R3 作为 **独立 UI 框架**：**不可行**
- R3 不是 UI 框架；它是响应式编程库
- 需要与实际的 UI 框架（ImGui、Avalonia 等）结合使用

R3 作为 **数据绑定层**：**可行但有注意事项**

**优点**：
- 非常适合响应式处理状态变化
- 可以创建干净、声明式的数据流
- 适合复杂的 UI 状态管理
- 性能优化（零分配）

**缺点**：
- 增加复杂性和学习曲线
- ECS 原生不提供可观察事件
- 需要轮询或事件桥接来观察变化
- 可能创建许多小型分配
- 调试响应式链可能困难

**推荐的集成模式**：
```
UI 层（ImGui/Avalonia）
    ↓
响应式 ViewModel（R3）
    ↓
事件桥接/轮询
    ↓
ECS 数据（Kilo.ECS）
```

### R3 与 Kilo 的最佳使用场景

1. **复杂状态管理**：当 UI 需要响应多个状态变化
2. **异步操作**：资源加载、网络操作、文件 I/O
3. **事件聚合**：协调多个 UI 元素的更新
4. **基于时间的操作**：定时器、动画、轮询
5. **数据验证**：用户输入的响应式验证

**不推荐用于**：
- 简单的单向数据显示
- 直接的 ECS 访问模式
- 性能关键的游戏循环
- 简单的检查器工具

---

## 4. 基于 Veldrid 的自定义 UI

### 概述

直接在 Veldrid 之上构建自定义 UI 框架，使用引擎自身的渲染能力来构建编辑器界面。

### 关键特性

**完全控制**：
- 完全控制渲染和行为
- 为游戏引擎需求量身定制
- 可以与游戏渲染共享资源
- 最大的性能优化空间

**高复杂度**：
- 必须从零实现所有 UI 原语
- 需要大量开发工作
- 调试和维护负担

### 技术分析

#### 成熟度和生态
- **状态**：无现有生态（自定义实现）
- **.NET 支持**：取决于实现
- **文档**：无（需要自行创建）
- **社区**：无（专有）
- **在游戏开发中的使用**：AAA 引擎中常见（Unreal、Unity）

#### 性能
- **渲染**：潜在最优（量身定制）
- **内存**：可为特定需求优化
- **延迟**：最小（直接控制）
- **游戏集成**：无缝（相同渲染器）

#### 开发体验
- **优点**：
  - 最大的控制和定制能力
  - 可针对特定引擎需求优化
  - 与游戏渲染无缝集成
  - 无外部依赖
  - 可以精确实现所需功能

- **缺点**：
  - 极高的开发工作量
  - 必须从零实现所有功能：
    - 文本渲染
    - 布局系统
    - 事件处理
    - 控件（按钮、滑块、树视图等）
    - 样式
    - 无障碍
  - 长期维护负担
  - 重复造轮子
  - 无社区支持或示例

#### 与 Kilo 的集成

```csharp
// 在 Veldrid 上的自定义 UI 渲染示例
public class CustomUIRenderer
{
    private readonly GraphicsDevice _device;
    private readonly CommandList _commandList;

    public void RenderUI(UIElement root)
    {
        _commandList.Begin();

        // 设置 UI 渲染管线
        // （着色器、顶点缓冲区等）

        RenderElement(root);

        _commandList.End();
        _device.SubmitCommands(_commandList);
    }

    private void RenderElement(UIElement element)
    {
        // 每种 UI 元素类型的自定义渲染
        switch (element)
        {
            case Button button:
                RenderButton(button);
                break;
            case TextBox textBox:
                RenderTextBox(textBox);
                break;
            // ... 更多类型
        }
    }
}
```

### 可行性评估：**低**（对于初始实现）

构建自定义 UI 框架对 Kilo 的初始编辑器 **不推荐**：

1. **巨大的工作量**：需要实现完整的 UI 框架
2. **耗时**：需要数月甚至数年才能匹敌 ImGui 的功能
3. **机会成本**：时间最好花在引擎功能上
4. **维护负担**：维护自定义 UI 的长期成本
5. **重复造轮子**：ImGui 已经解决了这个问题
6. **无社区支持**：无外部帮助或示例

**何时可能合理**：
- 引擎成熟且稳定之后
- 当现有方案不满足特定需求
- 对于拥有专门 UI 团队的 AAA 工作室
- 当极致性能至关重要且已证明现有方案无法实现

**不推荐用于**：
- 初始引擎开发
- 小到中型团队
- 时间/预算有限的项目

---

## 5. 对比总结

| 方面 | Dear ImGui | Avalonia | R3（响应式） | 自定义 Veldrid UI |
|------|-----------|----------|-------------|------------------|
| **类型** | 即时模式 UI | 保留模式 UI | 响应式库 | 自定义框架 |
| **成熟度** | 非常高 | 高 | 中高 | 无（自定义） |
| **.NET 10 支持** | 是 | 预期 | 预期 | 取决于实现 |
| **开发时间** | 低 | 中高 | 中（作为数据层） | 非常高 |
| **性能** | 优秀 | 良好 | 优秀（零分配） | 潜在最优 |
| **学习曲线** | 低 | 高（XAML/MVVM） | 中（Rx 概念） | 非常高 |
| **ECS 集成** | 直接 | 复杂（IPC） | 中（桥接） | 直接 |
| **游戏内使用** | 是 | 否 | 不适用 | 是 |
| **样式** | 有限 | 优秀 | 不适用 | 自定义 |
| **数据绑定** | 手动 | 内置 | 优秀（响应式） | 手动 |
| **社区** | 庞大 | 庞大 | 增长中（游戏） | 无 |
| **维护** | 外部 | 外部 | 外部 | 内部（高负担） |

---

## 6. 推荐方案

### 主要推荐：**Dear ImGui (ImGui.NET)**

**架构概览**：

```
┌─────────────────────────────────────────────────────────────┐
│                     Kilo 引擎编辑器                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              ImGui.NET（UI 层）                        │  │
│  │  - 实体检查器                                         │  │
│  │  - 场景层级                                           │  │
│  │  - 资源浏览器                                         │  │
│  │  - 控制台和日志                                       │  │
│  │  - 属性编辑器                                         │  │
│  │  - 性能分析器                                         │  │
│  └──────────────────────────────────────────────────────┘  │
│                           ↓                                 │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         直接 ECS 访问（Kilo.ECS）                     │  │
│  │  - KiloWorld                                          │  │
│  │  - 实体查询                                           │  │
│  │  - 组件访问                                           │  │
│  └──────────────────────────────────────────────────────┘  │
│                           ↓                                 │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Veldrid（渲染）                           │  │
│  │  - ImGui 渲染器后端                                   │  │
│  │  - 游戏渲染                                           │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 辅助增强：**R3 用于复杂状态管理**

对于需要响应式数据流的复杂 UI 场景，选择性使用 R3：

```
┌─────────────────────────────────────────────────────────────┐
│                     增强架构                                 │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              ImGui.NET（UI 层）                        │  │
│  └──────────────┬───────────────────────────────────────┘  │
│                 ↓                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │      R3 响应式层（用于复杂状态）                       │  │
│  │  - 实体变更可观察对象                                  │  │
│  │  - 查询结果可观察对象                                  │  │
│  │  - 异步操作处理                                        │  │
│  └──────────────┬───────────────────────────────────────┘  │
│                 ↓                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         直接 ECS 访问（Kilo.ECS）                     │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 理由

**为何选择 Dear ImGui 作为主要方案**：

1. **在游戏开发中经过验证**：被游戏引擎和开发者广泛使用
2. **快速开发**：最少的样板代码，快速实现
3. **优秀的性能**：为实时游戏编辑而设计
4. **直接 ECS 集成**：无需 IPC 即可直接访问 ECS 数据
5. **游戏内调试**：既可作为编辑器也可作为游戏内调试器
6. **Veldrid 兼容**：与渲染管线直接集成
7. **简洁性**：易于学习和使用
8. **活跃的社区**：庞大的社区，有丰富的示例和支持

**为何选择 R3 作为辅助增强**：

1. **选择性使用**：仅在 R3 能提供价值的复杂场景中使用
2. **性能**：零分配优化很有价值
3. **复杂状态**：非常适合管理复杂的 UI 状态和异步操作
4. **不断增长的生态**：对响应式模式有良好的未来保障
5. **学习投资**：响应式编程是有价值的技能

**为何不选 Avalonia**：

1. **过度设计**：完整的 MVVM 框架对初始编辑器来说过度
2. **IPC 复杂性**：单独的进程增加了不必要的复杂性
3. **开发时间**：实现时间更长
4. **游戏内限制**：无法轻松在游戏内运行进行调试
5. **学习曲线**：XAML 和 MVVM 需要更多专业知识

**为何不选自定义 UI**：

1. **巨大的工作量**：需要从零实现完整的 UI 框架
2. **机会成本**：时间最好花在引擎功能上
3. **维护负担**：维护自定义 UI 的长期成本
4. **重复造轮子**：ImGui 已经很好地解决了这个问题

---

## 7. 实现路线图

### 阶段 1：基于 ImGui 的核心编辑器（推荐的第一步）

**交付物**：
1. ImGui.NET 与 Veldrid 的集成
2. 基础编辑器窗口框架
3. 实体检查器
4. 场景层级
5. 控制台/日志窗口

**工作量**：2-4 周

### 阶段 2：增强工具

**交付物**：
1. 资源浏览器
2. 属性编辑器（针对常见组件类型）
3. 性能分析器
4. 保存/加载功能

**工作量**：2-3 周

### 阶段 3：R3 集成（如需要）

**交付物**：
1. R3 包集成
2. ECS 可观察桥接
3. 复杂 UI 的响应式 ViewModel
4. 异步操作处理

**工作量**：1-2 周

**触发条件**：当复杂状态管理成为问题时再实现 R3

### 阶段 4：高级功能

**交付物**：
1. 高级属性编辑器
2. 自定义控件
3. 脚本集成
4. 编辑器工具的插件系统

**工作量**：持续进行

---

## 8. 风险评估

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|---------|
| ImGui 在复杂 UI 中的局限性 | 中 | 中 | 在复杂场景中使用 R3；如需要可后续考虑 Avalonia |
| R3 学习曲线影响进度 | 低 | 低 | 仅在需要时引入 R3；提供培训资源 |
| ImGui.NET .NET 10 兼容性 | 极低 | 低 | ImGui.NET 积极维护；在原型中验证 |
| 大型场景的性能问题 | 低 | 中 | 实现虚拟化和懒加载；分析和优化 |
| 自定义控件的维护负担 | 中 | 中 | 保持自定义控件最少；利用 ImGui 生态 |

---

## 9. 结论

Kilo 编辑器的推荐方案为：

**主要方案**：Dear ImGui (ImGui.NET) 作为 UI 层
**辅助方案**：Cysharp/R3 用于复杂状态管理（仅在需要时）

此方案提供：
- **快速开发**：ImGui 允许快速迭代和原型设计
- **优秀的性能**：ImGui 和 R3 都为实时使用而优化
- **直接集成**：无缝的 ECS 访问，无 IPC 复杂性
- **灵活性**：如需要可演进为更复杂的方案
- **经过验证的技术**：两项技术在游戏开发中都被广泛使用
- **低风险**：成熟、文档完善、积极维护

编辑器应从 ImGui 开始，仅当复杂状态管理成为瓶颈时再引入 R3。Avalonia 和自定义 UI 框架只有在 ImGui 被证明不足以满足特定用例时才应考虑，而这对于专注于 ECS 操作和调试的游戏引擎编辑器来说是不太可能的。

---

## 来源

- [ImGui.NET GitHub 仓库](https://github.com/ImGuiNET/ImGui.NET)
- [Noel Berry - 2025 年制作电子游戏](https://noelberry.ca/posts/making_games_in_2025/)
- [Cysharp/R3 GitHub 仓库](https://github.com/Cysharp/R3)
- [SixLabors/ImageSharp GitHub](https://github.com/SixLabors/ImageSharp)
- [R3 - Reactive Extensions 的全新现代重新实现](https://neuecc.medium.com/r3-a-new-modern-reimplementation-of-reactive-extensions-for-c-cf29abcc5826)
