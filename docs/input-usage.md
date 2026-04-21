# Kilo.Input 使用指南

> Input 系统将物理按键与游戏逻辑解耦。通过 Action Mapping，游戏代码查询"跳跃"而非 `Key.Space`。

---

## 架构设计

### 4 层管线模型

```
[平台采集层]  Silk.NET 回调 → 标准化原始事件（InputWiring）
     ↓
[原始输入层]  InputState（键盘/鼠标/手柄，帧边界 ResetFrame）
     ↓  InputMapSystem.Update(inputState, mapStack, dt)
[动作映射层]  InputMap → InputBinding → Modifier 链 → Trigger → InputAction 状态
     ↓  InputMapStack.BeginFrame() / SavePrevious() 实现边缘检测
[游戏逻辑层]  stack.JustPressed("Jump") / stack.GetVector2("Move")
```

### 帧边界 & 边缘检测

```
Frame N:
  BeginFrame()        ← SavePrevious: WasActive = IsActive
  InputMapSystem()    ← 计算新状态，写入 IsActive
  游戏系统读取         ← JustPressed = IsActive && !WasActive
  ResetFrame()        ← 清理 Pressed/Released/Delta
Frame N+1:
  ...
```

### 核心类型关系

```
InputMapStack (ECS Resource, 游戏代码的查询入口)
  ├── InputMap[] (按 Priority 排序，高优先级消费输入)
  │     └── ActionDef (Action 名 + 类型 + 绑定 + 修饰器 + 触发器)
  │           ├── InputBinding[] (物理按键/按钮/轴)
  │           ├── CompositeAxis2D? (WASD→Vector2 组合绑定)
  │           ├── IInputModifier[] (Scale / Negate / DeadZone / ScaleByDelta)
  │           └── IInputTrigger (Press / Hold / Pulse)
  └── InputAction{} (运行时状态：IsActive / WasActive / FloatValue / Vector2Value)
```

### 设计原则

| 原则 | 实现方式 |
|------|---------|
| 硬件与逻辑解耦 | 游戏代码只查 Action 名，不接触 KeyCode |
| 帧边界明确 | `BeginFrame()` + `ResetFrame()` 双定界 |
| ECS 原生 | 全值类型，无 `static`，无 GC |
| 开放封闭 | `IInputModifier` / `IInputTrigger` 可扩展无需改核心 |
| 优先级消费 | 高优先级 Map 消费输入，低优先级被屏蔽 |

### 对齐四大引擎的设计取舍

| 维度 | Kilo 选择 | 参考 |
|------|----------|------|
| 动作值类型 | Button / Axis1D / Axis2D | Unreal |
| 组合输入 | `CompositeAxis2D` 一条搞定 WASD→Vector2 | Unity |
| 上下文管理 | 优先级堆栈 + 输入消费 | Unreal |
| 查询 API | `stack.JustPressed("Jump")` | Stride |
| 死区算法 | 径向死区 + 线性重映射 | Bevy |
| 触发模式 | `IInputTrigger` 链 | Unreal |
| 修饰器 | `IInputModifier` 链 | Unreal |

## 快速上手

### 1. 添加插件

```csharp
var app = new KiloApp();
app.AddPlugin(new WindowPlugin());  // 提供 InputState
app.AddPlugin(new InputPlugin());   // 提供 InputMapStack + InputMapSystem
```

### 2. 注册动作映射（Startup 阶段）

```csharp
app.AddSystem(KiloStage.Startup, world =>
{
    var stack = world.GetResource<InputMapStack>();

    var player = new InputMap("Player", priority: 0);
    player
        // 2D 移动：WASD + 左摇杆
        .AddAxis2D("Move",
            upKey: (int)Key.W, downKey: (int)Key.S,
            leftKey: (int)Key.A, rightKey: (int)Key.D,
            stick: GamepadThumbstick.LeftStick,
            modifiers: [new ScaleModifier { Factor = 5.0f }])
        // 跳跃：空格 / 手柄 A 键
        .AddAction("Jump", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.Space },
            new() { SourceType = BindingSourceType.GamepadButton, GamepadButton = 0 },
        ])
        // 开火：鼠标左键 / 手柄右肩
        .AddAction("Fire", ActionType.Button,
        [
            new() { SourceType = BindingSourceType.Mouse, KeyCode = 0 },
            new() { SourceType = BindingSourceType.GamepadButton, GamepadButton = 5 },
        ])
        // 升降：E / Q
        .AddAction("MoveUp", ActionType.Axis1D,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.E },
        ])
        .AddAction("MoveDown", ActionType.Axis1D,
        [
            new() { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.Q },
        ]);

    stack.Register(player);
    stack.Enable("Player");
});
```

### 3. 查询输入（Update 阶段）

```csharp
app.AddSystem(KiloStage.Update, world =>
{
    var input = world.GetResource<InputMapStack>();

    // 2D 移动值（Vector2）
    var move = input.GetVector2("Move");
    pos += new Vector3(move.X, 0, move.Y) * deltaTime;

    // 按钮边缘检测
    if (input.JustPressed("Jump")) Jump();
    if (input.JustReleased("Fire")) EndFire();

    // 持续按住
    if (input.IsPressed("Fire")) Fire();

    // 1D 轴值
    if (input.GetFloat("MoveUp") > 0f) pos.Y += speed * deltaTime;
});
```

## 查询 API

| 方法 | 返回类型 | 说明 |
|------|---------|------|
| `IsPressed(action)` | `bool` | 当前帧按住 |
| `JustPressed(action)` | `bool` | 本帧刚按下（仅触发一帧） |
| `JustReleased(action)` | `bool` | 本帧刚松开 |
| `GetFloat(action)` | `float` | Axis1D 值 |
| `GetVector2(action)` | `Vector2` | Axis2D 值 |
| `GetAction(action)` | `InputAction?` | 获取完整动作状态 |

## 上下文切换

多个 InputMap 可以按优先级堆叠，用于菜单/载具/暂停等场景：

```csharp
// 注册
stack.Register(new InputMap("Player", 0));
stack.Register(new InputMap("Vehicle", 5));
stack.Register(new InputMap("UI", 10));

// 进入载具
stack.Disable("Player");
stack.Enable("Vehicle");

// 打开菜单（高优先级自动屏蔽低优先级的冲突动作）
stack.Enable("UI");

// 关闭菜单
stack.Disable("UI");
```

高优先级 Map 的动作会"消费"对应输入，低优先级 Map 无法响应同一按键。

## Modifier（修饰器）

Modifier 链在 Binding 之后、Trigger 之前执行，用于变换输入值：

```csharp
player.AddAction("Move", ActionType.Button,
    bindings: [...],
    modifiers: [
        new ScaleModifier { Factor = 2.0f },       // 缩放（灵敏度）
        new NegateModifier { NegateX = true },       // 反转轴
        new DeadZoneModifier { Lower = 0.1f },       // 死区
        new ScaleByDeltaModifier(),                   // 帧率无关
    ]);
```

内置 Modifier：

| 类型 | 作用 |
|------|------|
| `ScaleModifier` | 乘以常数因子（灵敏度） |
| `NegateModifier` | 反转轴方向 |
| `DeadZoneModifier` | 1D: `[Lower, Upper] → [0, 1]`；2D: 径向死区 |
| `ScaleByDeltaModifier` | 乘以 deltaTime（帧率无关） |

## Trigger（触发器）

Trigger 决定动作"何时"激活：

```csharp
// 按下即触发（默认）
player.AddAction("Jump", ActionType.Button, [...]);

// 长按 0.5 秒触发
player.AddAction("Charge", ActionType.Button, [...],
    trigger: new HoldTrigger { Duration = 0.5f });

// 连发（每 0.1 秒触发一次）
player.AddAction("RapidFire", ActionType.Button, [...],
    trigger: new PulseTrigger { Interval = 0.1f });
```

## Gamepad

InputState 支持最多 4 个手柄，自动处理死区和线性重映射：

```csharp
var input = world.GetResource<InputState>();
ref var gp = ref input.Gamepads[0];

// 直接读取原始状态（Action Mapping 层已处理死区）
gp.LeftStick;        // Vector2，已应用死区
gp.RightStick;       // Vector2
gp.LeftTrigger;      // float [0, 1]
gp.ButtonsDown[0];   // South (A)

// 振动
gp.VibrationLeftMotor = 0.5f;
gp.VibrationRightMotor = 0.3f;
```

## 完整管线

```
Silk.NET 平台回调
    ↓
InputState（原始状态：键盘/鼠标/手柄）
    ↓  InputMapSystem.Update()
InputMap（绑定配置）
    ↓  评估 Bindings / Composite
Modifier 链（Scale → Negate → DeadZone）
    ↓  变换值
Trigger（Press / Hold / Pulse）
    ↓  判定是否激活
InputMapStack（写入动作状态）
    ↓
游戏代码查询 input.JustPressed("Jump")
```

## 扩展：自定义 Modifier

```csharp
public struct ExponentialCurveModifier : IInputModifier
{
    public float Exponent;
    public ExponentialCurveModifier() { Exponent = 2.0f; }

    public float ModifyFloat(float value, float dt)
        => MathF.Sign(value) * MathF.Pow(MathF.Abs(value), Exponent);
    public Vector2 ModifyVector2(Vector2 value, float dt)
        => new(ModifyFloat(value.X, dt), ModifyFloat(value.Y, dt));
}
```

## 扩展：自定义 Trigger

```csharp
public struct DoubleTapTrigger : IInputTrigger
{
    public float Interval { get; set; } = 0.3f;
    private float _lastTapTime;
    private bool _wasActive;

    public TriggerState Update(float rawMagnitude, float deltaTime)
    {
        bool isActive = rawMagnitude > 0f;
        if (isActive && !_wasActive) // 上升沿
        {
            float now = /* cumulative time */;
            // 检测双击逻辑...
        }
        _wasActive = isActive;
        return TriggerState.None;
    }
}
```
