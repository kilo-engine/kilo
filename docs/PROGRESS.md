# Kilo 引擎 - 总进度文档

> 本文档跟踪 Kilo 游戏引擎的所有计划、进度和阶段。
> 它是跨 Agent 协调的唯一真实来源。

## 当前阶段：阶段 4 - 引擎 MVP

## 阶段概览

| 阶段 | 状态 | 描述 |
|------|------|------|
| 阶段 1 | **已完成** | ECS 防腐层 MVP |
| 阶段 2 | **已完成** | Veldrid/Bepu 文档、插件选型、编辑器分析 |
| 阶段 3 | **已完成** | 插件开发（渲染、物理、输入、资源） |
| 阶段 4 | **进行中** | 引擎 MVP（整合所有插件的可运行游戏） |
| 阶段 5 | 已规划 | 编辑器架构与高级功能 |

---

## 阶段 1：ECS 防腐层（已完成）

### 交付物
- [x] `src/Kilo.ECS/` - 封装 TinyEcs + TinyEcs.Bevy 的防腐层
- [x] `tests/Kilo.ECS.Tests/` - 33 个 xUnit 测试，全部通过
- [x] `samples/Kilo.Samples.HelloECS/` - 可运行的示例
- [x] `README.md` - 项目文档
- [x] 目标框架 .NET 10.0

### 架构
```
游戏代码 / 插件
       |
   Kilo.ECS（防腐层）
       |
   TinyEcs + TinyEcs.Bevy（内部实现）
```

### 关键类型
| Kilo 类型 | 封装 | 类型 |
|-----------|------|------|
| `KiloWorld` | `TinyEcs.World` | sealed class |
| `KiloEntity` | `TinyEcs.EntityView` | readonly ref struct |
| `EntityId` | `ulong` (EcsID) | readonly struct |
| `Ptr<T>` | `TinyEcs.Ptr<T>` | ref struct |
| `KiloQueryBuilder` | `TinyEcs.QueryBuilder` | class |
| `KiloQuery` | `TinyEcs.Query` | class |
| `KiloQueryIterator` | `TinyEcs.QueryIterator` | ref struct |
| `KiloApp` | `TinyEcs.Bevy.App` | class |
| `KiloStage` | `TinyEcs.Bevy.Stage` | sealed class |
| `IKiloPlugin` | - | interface |
| `State<T>` | `TinyEcs.Bevy.State<T>` | sealed class |
| `NextState<T>` | `TinyEcs.Bevy.NextState<T>` | sealed class |
| `IKiloBundle` | - | interface |

### 已知限制（在后续迭代中解决）
- 系统参数注入（Query<TData>, Res<T>, Commands）仍直接使用 TinyEcs.Bevy 类型
- 需要为 Kilo 品牌的 Data/Filter 类型重新生成 T4 模板
- Observer 触发类型需要完整的 Kilo 封装
- USE_PAIR 功能不支持（TinyEcs 中默认禁用）

---

## 阶段 2：文档与研究（已完成）

### 交付物
- [x] Veldrid 渲染指南：`docs/veldrid-guide.md`（1614 行）
- [x] BepuPhysics 指南：`docs/bepuphysics-guide.md`（1548 行）
- [x] 插件选型报告：`docs/plugin-selection-report.md`（644 行）
- [x] 编辑器架构分析：`docs/editor-analysis-report.md`（773 行）

### 研究关键决策
- **渲染**：Veldrid（跨平台、多后端、.NET 兼容）
- **物理**：BepuPhysics v2（纯 C#、高性能、无原生依赖）
- **音频**：按选型报告推荐
- **输入**：按选型报告推荐
- **编辑器**：按分析报告推荐（评估 R3 + ImGui 方案）

---

## 阶段 3：插件开发（已完成）

### 交付物
- [x] `src/Kilo.Rendering/` - Veldrid 渲染插件（8 组件、2 资源、6 系统）
- [x] `src/Kilo.Physics/` - BepuPhysics 物理插件（4 组件、2 资源、3 系统）
- [x] `src/Kilo.Input/` - 输入插件（1 组件、2 资源、1 系统）
- [x] `src/Kilo.Assets/` - 资源管理插件（1 组件、2 资源、1 系统、加载器接口）
- [x] `tests/Kilo.Rendering.Tests/` - 17 个测试通过
- [x] `tests/Kilo.Physics.Tests/` - 27 个测试通过（1 个跳过：BepuPhysics beta Dispose 问题）
- [x] `tests/Kilo.Input.Tests/` - 24 个测试通过
- [x] `tests/Kilo.Assets.Tests/` - 38 个测试通过

### 依赖
| 插件 | NuGet 包 |
|------|----------|
| Kilo.Rendering | Veldrid 4.8.0, Veldrid.StartupUtilities, Veldrid.SPIRV |
| Kilo.Physics | BepuPhysics 2.5.0-beta.28, BepuUtilities |
| Kilo.Input | （纯 Kilo.ECS 依赖，窗口层由宿主提供） |
| Kilo.Assets | SixLabors.ImageSharp 3.1.* |

### 架构规则（已验证）
- 每个插件 = 独立的 .csproj，仅引用 `Kilo.ECS`
- 每个插件实现 `IKiloPlugin`
- 每个插件有自己的测试项目
- 插件不直接引用 TinyEcs
- 插件定义 ECS 组件（struct）和系统（函数）

---

## 阶段 4：引擎 MVP（进行中）

### 目标
一个整合所有插件并包含性能基准测试的可运行游戏示例。

### 子阶段
1. **4A - 整合示例**：创建使用全部 4 个插件的 samples 项目
2. **4B - 性能基准**：添加性能测试
3. **4C - 文档更新**：README、架构图

---

## 阶段 5：未来方向（已规划）

- 使用 T4 模板的完整系统参数注入
- 编辑器插件
- 资源管线
- 场景管理
- 网络
- 脚本系统

---

## 构建与运行命令

```bash
dotnet build Kilo.slnx
dotnet test tests/Kilo.ECS.Tests/Kilo.ECS.Tests.csproj --framework net10.0
dotnet run --project samples/Kilo.Samples.HelloECS
```

## 文件位置

- 解决方案：`Kilo.slnx`
- 本文档：`docs/PROGRESS.md`
- 源代码：`src/`
- 测试：`tests/`
- 示例：`samples/`
- 文档：`docs/`
