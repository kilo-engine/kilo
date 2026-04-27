# Kilo 输入系统 — 技术方向文档

> 对照 Stride / Bevy / Unity / Unreal 四大引擎，确定 Kilo 输入系统架构。

---

## 一、当前现状

```
InputState (Resource)
├── KeysDown[512] / KeysPressed[512] / KeysReleased[512]
├── MousePosition / MouseDelta / MouseButtonsDown[5] / ScrollDelta
└── 无：Gamepad / Action 映射 / 死区 / 事件回调

InputWiring (Silk.NET 回调)
└── 直接写入 InputState 的 bool 数组

使用方式（RenderDemo）：
└── input.IsKeyDown((int)Key.W)  ← 到处硬编码物理按键
```

**核心问题**：游戏逻辑与物理按键耦合。换个键位要改代码，无法支持手柄。

---

## 二、四引擎对比

### 2.1 总体架构对比

| 维度 | Stride | Bevy | Unity | Unreal |
|------|--------|------|-------|--------|
| **抽象层** | VirtualButton（名称→按键） | Action + Binding（Rust macro） | InputActionAsset（序列化资源） | InputAction + MappingContext（数据资产） |
| **映射粒度** | 一个名字绑多个按键 | Action 绑定 Binding 列表 | Action → Composite → Key | Action → Key + Modifier 链 + Trigger 链 |
| **值类型** | float（0/1 或模拟量） | bool / f32 / Vec2 / Vec3 | bool / float / Vector2 / Quaternion | bool / Axis1D / Axis2D / Axis3D |
| **上下文切换** | 无内置（换 ConfigSet） | Context 组件 | ActionMap 启用/禁用 | MappingContext 堆栈 + 优先级 |
| **组合输入** | 无 | Cardinal(AWSD) | Composite(WASD→Vector2) | Modifier 链(Negate+Swizzle) |
| **触发模式** | 无（仅按下/释放） | Condition(Hold/Tap/Combo) | Interaction(Press/Hold/DoubleTap) | Trigger(Down/Hold/Tap/Pulse/Combo) |
| **修饰器** | 无 | Modifier(DeadZone/Scale) | Processor(Deadzone/Invert/Normalize) | Modifier(DeadZone/Scalar/Swizzle/Curve) |
| **手柄振动** | SetVibration() | GamepadRumbleRequest | 无内置（需手柄特定 API） | force feedback via IHapticDevice |
| **多人** | GetVirtualButton(playerIdx, name) | Gamepad 组件绑定到 Context 实体 | PlayerInput 组件 per player | LocalPlayer 子系统 per player |
| **配置方式** | 代码 | 代码（macro） | 编辑器资产 / 代码 | 编辑器资产 / 代码 |

### 2.2 各引擎核心设计思想

#### Stride — 最简单
```
VirtualButtonConfigSet
  └── VirtualButtonConfig
       └── VirtualButtonBinding("Move", VirtualButton.Keyboard.A)
       └── VirtualButtonBinding("Move", VirtualButton.GamePad.LeftThumbLeft)
```
- 仅做名字→按键的映射，无触发模式/修饰器/组合
- 查询 `Input.GetVirtualButton(playerIdx, "Move")` 返回 float
- **优点**：极简，上手快
- **缺点**：WASD→Vector2 需手动拼，无死区/触发的内置支持

#### Bevy — ECS 原生
```
Action<Movement> (Output=Vec2)
  └── bindings![Cardinal::wasd(), GamepadStick::LeftStick]

Action<Jump> (Output=bool)
  └── bindings![KeyCode::Space, GamepadButton::South]
```
- Action 是 ECS 组件，Context 是标记组件
- 支持 Observer（On<Trigger<Fire, Action<Jump>>>）和 Poll（action.value()）
- Gamepad 是 per-entity 组件，天然支持本地多人
- **优点**：与 ECS 深度集成，类型安全
- **缺点**：Rust macro 依赖，概念较新

#### Unity — 数据驱动
```
InputActionAsset
  └── ActionMap("Player")
       └── InputAction("Move", ValueType=Vector2)
            └── CompositeBinding("WASD")
                 ├── W → (0,1), S → (0,-1), A → (-1,0), D → (1,0)
       └── InputAction("Jump", ValueType=Button)
            └── Binding("<Keyboard>/space")
            └── Binding("<Gamepad>/buttonSouth")
                 └── Interaction="Hold(duration=0.2)"
                 └── Processor="Deadzone(min=0.1)"
```
- 完整的序列化资产格式，编辑器可视化配置
- Composite 绑定（WASD/DPad→Vector2）解决组合输入
- Interaction（Press/Hold/DoubleTap/SlowTap）和 Processor（Deadzone/Invert/Normalize）
- **优点**：最完整，编辑器支持好
- **缺点**：API 复杂，学习曲线陡

#### Unreal — 最强大的管线
```
InputMappingContext (priority=0, "Walking")
  └── InputAction("Move", ValueType=Axis2D)
       ├── Key=W + Modifiers=[Swizzle(YXZ)]
       ├── Key=S + Modifiers=[Negate, Swizzle(YXZ)]
       ├── Key=A + Modifiers=[Negate]
       ├── Key=D + Modifiers=[]
       └── Key=Gamepad_Left2D + Modifiers=[DeadZone(0.2), Scalar(2.0)]

InputMappingContext (priority=10, "Driving")
  └── InputAction("Move", ValueType=Axis2D)
       └── Key=Gamepad_Left2D + Modifiers=[DeadZone(0.1)]
```
- Modifier 链：DeadZone → Scalar → Negate → Swizzle → Curve → Smooth
- Trigger 链：Down / Pressed / Released / Hold / Tap / Pulse / Combo / Chord
- Context 优先级堆栈：高层 Context 覆盖低层
- 与 GAS（Gameplay Ability System）通过 Tag 桥接
- **优点**：最灵活，管线化设计
- **缺点**：概念多，C++ 模板重

---

## 三、Kilo 输入系统设计

### 3.1 设计原则

1. **参考 Unreal 的管线架构，简化到 Bevy 的 API 复杂度**
2. **保留 Stride 的简洁查询 API**
3. **ECS 原生**：Action 作为组件，Context 作为资源
4. **代码优先**：配置用 C# 表达式，未来可加序列化
5. **渐进式**：第一版只做核心映射层，后续迭代加 Modifier/Trigger

### 3.2 分层架构

```
┌──────────────────────────────────────────────────────┐
│  Layer 3 — Game Code                                 │
│  input.JustPressed("Jump") / input.GetValue("Move") │
├──────────────────────────────────────────────────────┤
│  Layer 2 — Action Mapping（新增）                     │
│  InputMap Resource → Action → Binding → PhysicalKey  │
│  InputContext Resource → 活跃 Action 集合 + 优先级    │
│  Modifier 链 (DeadZone / Scale / Negate)             │
│  Trigger 模式 (Press / Hold / DoubleTap)             │
├──────────────────────────────────────────────────────┤
│  Layer 1 — Raw Input（现有，增强）                     │
│  InputState → Keyboard / Mouse / Gamepad             │
│  Silk.NET 回调 → 写入 Raw 状态                        │
│  Gamepad 新增：Axis / DeadZone / Rumble              │
└──────────────────────────────────────────────────────┘
```

---

## 四、Layer 1 — Raw Input 增强

### 4.1 扩展 InputState

```csharp
// Kilo.Window/Resources/InputState.cs — 增强版
public sealed class InputState
{
    // ── Keyboard ──
    public bool[] KeysDown { get; } = new bool[512];
    public bool[] KeysPressed { get; } = new bool[512];
    public bool[] KeysReleased { get; } = new bool[512];

    // ── Mouse ──
    public Vector2 MousePosition;
    public Vector2 MouseDelta;
    public bool[] MouseButtonsDown { get; } = new bool[8];
    public bool[] MouseButtonsPressed { get; } = new bool[8];
    public bool[] MouseButtonsReleased { get; } = new bool[8];
    public float ScrollDelta;

    // ── Gamepad（新增）──
    public GamepadState[] Gamepads { get; } = new GamepadState[4]; // 支持最多 4 个手柄
    public int ConnectedGamepadCount;

    // ── 鼠标锁定（新增）──
    public bool IsMouseLocked;

    public void ResetFrame()
    {
        KeysPressed.AsSpan().Clear();
        KeysReleased.AsSpan().Clear();
        MouseButtonsPressed.AsSpan().Clear();
        MouseButtonsReleased.AsSpan().Clear();
        MouseDelta = Vector2.Zero;
        ScrollDelta = 0f;
        // Gamepad 的 pressed/released 在 GamepadState 内部清理
    }
}
```

### 4.2 GamepadState

```csharp
// Kilo.Window/Resources/GamepadState.cs
public struct GamepadState
{
    public bool IsConnected;

    // 模拟轴 [-1, 1]
    public Vector2 LeftStick;
    public Vector2 RightStick;
    public float LeftTrigger;   // [0, 1]
    public float RightTrigger;  // [0, 1]

    // 按钮（16 个标准按钮）
    public bool[] ButtonsDown { get; } = new bool[16];
    public bool[] ButtonsPressed { get; } = new bool[16];
    public bool[] ButtonsReleased { get; } = new bool[16];

    // 死区设置
    public float LeftStickDeadZone = 0.15f;
    public float RightStickDeadZone = 0.15f;
    public float TriggerThreshold = 0.1f;

    // 振动
    public float VibrationLeftMotor;   // [0, 1]
    public float VibrationRightMotor;  // [0, 1]

    public enum Button
    {
        South = 0,   // A / Cross
        East = 1,    // B / Circle
        West = 2,    // X / Square
        North = 3,   // Y / Triangle
        LeftShoulder = 4,
        RightShoulder = 5,
        LeftTrigger2 = 6,   // Digital shoulder
        RightTrigger2 = 7,
        LeftThumb = 8,      // Stick click
        RightThumb = 9,
        DPadUp = 10,
        DPadDown = 11,
        DPadLeft = 12,
        DPadRight = 13,
        Select = 14,        // Back / Share
        Start = 15,         // Start / Options
    }
}
```

### 4.3 InputWiring 扩展

```csharp
// 在现有 InputWiring.WireInputEvents 中增加：
foreach (var gamepad in inputContext.Gamepads)
{
    int gpIndex = gamepad.Index;
    if (gpIndex >= 4) continue;
    ref var state = ref inputState.Gamepads[gpIndex];
    state.IsConnected = true;

    gamepad.ButtonDown += (_, button) =>
    {
        int idx = (int)button;
        if (idx >= 0 && idx < 16)
        {
            state.ButtonsDown[idx] = true;
            state.ButtonsPressed[idx] = true;
        }
    };
    gamepad.ButtonUp += (_, button) =>
    {
        int idx = (int)button;
        if (idx >= 0 && idx < 16)
        {
            state.ButtonsDown[idx] = false;
            state.ButtonsReleased[idx] = true;
        }
    };
    gamepad.ThumbstickMoved += (_, thumbstick) =>
    {
        if (thumbstick.Thumbstick == Thumbstick.Left)
            state.LeftStick = ApplyDeadZone(thumbstick.X, thumbstick.Y, state.LeftStickDeadZone);
        else
            state.RightStick = ApplyDeadZone(thumbstick.X, thumbstick.Y, state.RightStickDeadZone);
    };
    gamepad.TriggerMoved += (_, trigger) =>
    {
        float value = trigger.Value > state.TriggerThreshold ? trigger.Value : 0f;
        if (trigger.Trigger == Trigger.Left)
            state.LeftTrigger = value;
        else
            state.RightTrigger = value;
    };
}

// 鼠标锁定
mouse.Cursor.Click += (_, _) => { /* 按需锁定 */ };
```

### 4.4 死区处理

参考 Bevy 的 AxisSettings（线性重映射 + 迟滞）和 Unreal 的 UInputModifierDeadZone：

```csharp
// Kilo.Window/InputProcessing.cs
public static class InputProcessing
{
    /// <summary>
    /// 径向死区 + 线性重映射（参考 Bevy AxisSettings）
    /// </summary>
    public static Vector2 ApplyDeadZone(float x, float y, float deadZone)
    {
        float magnitude = MathF.Sqrt(x * x + y * y);
        if (magnitude <= deadZone) return Vector2.Zero;
        // 线性重映射: [deadZone, 1.0] → [0.0, 1.0]
        float remapped = (magnitude - deadZone) / (1.0f - deadZone);
        float scale = remapped / magnitude;
        return new Vector2(x * scale, y * scale);
    }

    /// <summary>
    /// 按钮迟滞（参考 Bevy ButtonSettings）
    /// pressThreshold=0.75, releaseThreshold=0.65
    /// 防止模拟触发器在阈值边界来回抖动
    /// </summary>
    public static bool ApplyHysteresis(float value, bool currentState, float pressThreshold, float releaseThreshold)
    {
        if (currentState) return value >= releaseThreshold;
        return value >= pressThreshold;
    }
}
```

---

## 五、Layer 2 — Action Mapping 系统

### 5.1 核心类型

参考 Unreal 的 InputAction + InputMappingContext，简化设计：

```csharp
// ── Kilo.Input/Actions/InputAction.cs ──

/// <summary>
/// 输入动作值类型
/// </summary>
public enum ActionType
{
    Button,  // bool — 跳跃、交互、开火
    Axis1D,  // float — 缩放、油门
    Axis2D,  // Vector2 — 移动、瞄准
}

/// <summary>
/// 表示一个逻辑输入动作（参考 Unreal UInputAction + Bevy InputAction）
/// </summary>
public sealed class InputAction
{
    public string Name { get; init; }
    public ActionType Type { get; init; }

    /// <summary>当前帧的值</summary>
    public float FloatValue;
    public Vector2 Vector2Value;
    public bool BoolValue;

    /// <summary>上一帧是否激活（用于检测 JustPressed / JustReleased）</summary>
    public bool WasActive;
    public bool IsActive;

    // 状态查询（类似 Stride 的简洁 API）
    public bool IsPressed => IsActive;
    public bool JustPressed => IsActive && !WasActive;
    public bool JustReleased => !IsActive && WasActive;
}
```

### 5.2 Binding — 物理按键绑定

```csharp
// ── Kilo.Input/Bindings/InputBinding.cs ──

/// <summary>
/// 绑定源类型
/// </summary>
public enum BindingSourceType
{
    Keyboard,
    Mouse,
    GamepadButton,
    GamepadAxis,      // 单轴 (LeftTrigger)
    GamepadThumbstick, // 双轴 (LeftStick → Vector2)
    CompositeWASD,    // WASD 组合 → Vector2
    CompositeArrow,   // 方向键组合 → Vector2
}

/// <summary>
/// 单个物理绑定（参考 Unreal FInputMappingContext 的单条映射）
/// </summary>
public struct InputBinding
{
    public BindingSourceType SourceType;
    public int KeyCode;          // Keyboard: Silk.NET Key; Mouse: button index
    public int GamepadIndex;     // -1 = any gamepad
    public GamepadState.Button GamepadButton;
    public GamepadAxis GamepadAxis;
    public GamepadThumbstick GamepadThumbstick;

    // 绑定级 Modifier 链（可选）
    public IInputModifier[] Modifiers;
}

public enum GamepadAxis
{
    LeftTrigger,
    RightTrigger,
}

public enum GamepadThumbstick
{
    LeftStick,
    RightStick,
}
```

### 5.3 Composite Binding — WASD → Vector2

参考 Unity 的 CompositeBinding 和 Unreal 的 Swizzle+Negate 方案：

```csharp
/// <summary>
/// WASD / 方向键 组合绑定，输出 Vector2
/// 参考 Unity CompositeBinding("WASD")
/// Unreal 的方案需要 4 条独立绑定 + Swizzle/Negate modifier，太复杂
/// Bevy 用 Cardinal::wasd() 预设
/// Kilo 选择 Unity 的 Composite 方案：一条绑定解决，最直观
/// </summary>
public struct CompositeAxis2DBinding
{
    public int UpKey;
    public int DownKey;
    public int LeftKey;
    public int RightKey;
    public int GamepadIndex;         // -1 = any
    public GamepadThumbstick FallbackStick; // 手柄备选
}
```

### 5.4 Modifier — 输入修饰器

参考 Unreal 的 UInputModifier 链，简化：

```csharp
// ── Kilo.Input/Modifiers/IInputModifier.cs ──

/// <summary>
/// 输入修饰器接口（参考 Unreal UInputModifier）
/// </summary>
public interface IInputModifier
{
    float ModifyFloat(float value, float deltaTime);
    Vector2 ModifyVector2(Vector2 value, float deltaTime);
}

/// <summary>缩放（灵敏度）</summary>
public struct ScaleModifier : IInputModifier
{
    public float Factor;
    public float ModifyFloat(float v, float dt) => v * Factor;
    public Vector2 ModifyVector2(Vector2 v, float dt) => v * Factor;
}

/// <summary>反转轴</summary>
public struct NegateModifier : IInputModifier
{
    public bool NegateX, NegateY;
    public float ModifyFloat(float v, float dt) => -v;
    public Vector2 ModifyVector2(Vector2 v, float dt) => new(NegateX ? -v.X : v.X, NegateY ? -v.Y : v.Y);
}

/// <summary>死区（用于绑定级，处理 Device 层未覆盖的情况）</summary>
public struct DeadZoneModifier : IInputModifier
{
    public float Lower, Upper;
    public float ModifyFloat(float v, float dt) => v < Lower ? 0f : v > Upper ? 1f : (v - Lower) / (Upper - Lower);
    public Vector2 ModifyVector2(Vector2 v, float dt) => v.Length() < Lower ? Vector2.Zero : v;
}

/// <summary>按帧率缩放（帧率无关的输入）</summary>
public struct ScaleByDeltaTimeModifier : IInputModifier
{
    public float ModifyFloat(float v, float dt) => v * dt;
    public Vector2 ModifyVector2(Vector2 v, float dt) => v * dt;
}

/// <summary>指数曲线（手柄非线性手感）</summary>
public struct ExponentialCurveModifier : IInputModifier
{
    public float Exponent = 2.0f;
    public float ModifyFloat(float v, float dt) => MathF.Sign(v) * MathF.Pow(MathF.Abs(v), Exponent);
    public Vector2 ModifyVector2(Vector2 v, float dt) => new(ModifyFloat(v.X, dt), ModifyFloat(v.Y, dt));
}
```

### 5.5 Trigger — 触发模式

参考 Unreal 的 UInputTrigger 链，第一版实现最常用的：

```csharp
// ── Kilo.Input/Triggers/InputTrigger.cs ──

/// <summary>
/// 触发器接口（参考 Unreal UInputTrigger）
/// 决定"何时"触发动作
/// </summary>
public interface IInputTrigger
{
    TriggerState Update(float rawValue, float deltaTime);
}

public enum TriggerState
{
    None,       // 未触发
    Ongoing,    // 持续中（Hold 等待期）
    Triggered,  // 已触发
}

/// <summary>按下即触发（最常见）</summary>
public struct PressTrigger : IInputTrigger
{
    public TriggerState Update(float rawValue, float dt) =>
        rawValue > 0f ? TriggerState.Triggered : TriggerState.None;
}

/// <summary>长按触发</summary>
public struct HoldTrigger : IInputTrigger
{
    public float Duration = 0.5f;
    private float _heldTime;

    public TriggerState Update(float rawValue, float dt)
    {
        if (rawValue <= 0f) { _heldTime = 0f; return TriggerState.None; }
        _heldTime += dt;
        return _heldTime >= Duration ? TriggerState.Triggered : TriggerState.Ongoing;
    }
}

/// <summary>双击触发</summary>
public struct DoubleTapTrigger : IInputTrigger
{
    public float Interval = 0.3f;
    private float _lastTapTime;
    private int _tapCount;

    public TriggerState Update(float rawValue, float dt)
    {
        // 检测按下瞬间
        // _tapCount++ 并检查时间间隔
        // 两次间隔 < Interval → Triggered
        // 超时 → 重置
        // (完整实现需要 JustPressed 信号)
        return TriggerState.None;
    }
}

/// <summary>脉冲触发（连发）</summary>
public struct PulseTrigger : IInputTrigger
{
    public float Interval = 0.1f;
    private float _elapsed;

    public TriggerState Update(float rawValue, float dt)
    {
        if (rawValue <= 0f) { _elapsed = 0f; return TriggerState.None; }
        _elapsed += dt;
        if (_elapsed >= Interval) { _elapsed = 0f; return TriggerState.Triggered; }
        return TriggerState.Ongoing;
    }
}
```

### 5.6 InputMap — 动作映射表

```csharp
// ── Kilo.Input/InputMap.cs ──

/// <summary>
/// 输入映射表（参考 Unreal InputMappingContext + Bevy Context）
/// 定义一组 Action → Binding 的映射关系
/// </summary>
public sealed class InputMap
{
    public string Name { get; init; }
    public int Priority { get; init; } = 0;

    // Action名 → 绑定列表
    private readonly Dictionary<string, ActionDef> _actions = new();

    public InputMap AddAction(string name, ActionType type,
        InputBinding[] bindings,
        IInputModifier[]? modifiers = null,
        IInputTrigger? trigger = null)
    {
        _actions[name] = new ActionDef
        {
            Name = name,
            Type = type,
            Bindings = bindings,
            Modifiers = modifiers ?? [],
            Trigger = trigger ?? new PressTrigger(),
        };
        return this;
    }

    /// <summary>快捷方法：添加 WASD / 手柄摇杆移动</summary>
    public InputMap AddMove2D(string name,
        int upKey, int downKey, int leftKey, int rightKey,
        GamepadThumbstick stick = GamepadThumbstick.LeftStick,
        int gamepadIndex = -1,
        IInputModifier[]? modifiers = null)
    {
        _actions[name] = new ActionDef
        {
            Name = name,
            Type = ActionType.Axis2D,
            Composite = new CompositeAxis2DBinding
            {
                UpKey = upKey, DownKey = downKey,
                LeftKey = leftKey, RightKey = rightKey,
                GamepadIndex = gamepadIndex,
                FallbackStick = stick,
            },
            Modifiers = modifiers ?? [],
            Trigger = new PressTrigger(),
        };
        return this;
    }

    internal IReadOnlyDictionary<string, ActionDef> Actions => _actions;
}

internal struct ActionDef
{
    public string Name;
    public ActionType Type;
    public InputBinding[] Bindings;
    public CompositeAxis2DBinding? Composite;
    public IInputModifier[] Modifiers;
    public IInputTrigger Trigger;
}
```

### 5.7 InputMapSystem — 核心处理系统

```csharp
// ── Kilo.Input/Systems/InputMapSystem.cs ──

/// <summary>
/// 每帧读取 Raw InputState，通过 InputMap 解析为 Action 值。
/// 参考管线：
///   Raw State → Bindings → Modifier 链 → Trigger → Action 值
/// </summary>
public sealed class InputMapSystem
{
    public void Update(KiloWorld world, float deltaTime)
    {
        var inputState = world.GetResource<InputState>();
        var mapStack = world.GetResource<InputMapStack>();

        // 从高优先级到低优先级处理
        // 高优先级 context 的 action 会"消费"对应的物理按键
        var consumedKeys = new HashSet<int>();
        var consumedButtons = new HashSet<(int gp, int btn)>();

        foreach (var map in mapStack.ActiveMaps.OrderByDescending(m => m.Priority))
        {
            foreach (var (actionName, def) in map.Actions)
            {
                // 如果该 action 已被更高优先级 map 激活，跳过
                if (mapStack.IsActionConsumed(actionName)) continue;

                float rawValue = 0f;
                Vector2 rawVec2 = Vector2.Zero;
                bool found = false;

                // ── Composite 绑定（WASD→Vector2）──
                if (def.Composite.HasValue)
                {
                    var c = def.Composite.Value;
                    rawVec2 = EvaluateComposite(c, inputState, consumedKeys);
                    found = rawVec2 != Vector2.Zero;

                    // 手柄摇杆备选
                    if (!found)
                    {
                        for (int gi = 0; gi < inputState.ConnectedGamepadCount; gi++)
                        {
                            if (c.GamepadIndex >= 0 && gi != c.GamepadIndex) continue;
                            var gp = inputState.Gamepads[gi];
                            if (!gp.IsConnected) continue;
                            var stick = c.FallbackStick == GamepadThumbstick.LeftStick
                                ? gp.LeftStick : gp.RightStick;
                            if (stick.LengthSquared() > 0.01f)
                            {
                                rawVec2 = stick;
                                found = true;
                                break;
                            }
                        }
                    }
                }

                // ── 普通绑定 ──
                if (!found)
                {
                    foreach (var binding in def.Bindings)
                    {
                        float value = EvaluateBinding(binding, inputState, consumedKeys, consumedButtons);
                        if (value > 0f)
                        {
                            rawValue = value;
                            found = true;
                            break;
                        }
                    }
                }

                // ── Modifier 链 ──
                foreach (var mod in def.Modifiers)
                {
                    rawValue = mod.ModifyFloat(rawValue, deltaTime);
                    rawVec2 = mod.ModifyVector2(rawVec2, deltaTime);
                }

                // ── Trigger ──
                var triggerState = def.Trigger.Update(found ? 1f : 0f, deltaTime);
                bool isActive = triggerState == TriggerState.Triggered;

                // ── 写入 Action 结果 ──
                mapStack.SetActionState(actionName, def.Type, isActive,
                    def.Type == ActionType.Axis2D ? rawVec2 :
                    Vector2.Zero,
                    rawValue);

                if (isActive) mapStack.ConsumeAction(actionName);
            }
        }
    }

    private static float EvaluateBinding(InputBinding b, InputState input,
        HashSet<int> consumedKeys, HashSet<(int, int)> consumedButtons)
    {
        // 检查是否已被消费
        if (b.SourceType == BindingSourceType.Keyboard && consumedKeys.Contains(b.KeyCode))
            return 0f;

        return b.SourceType switch
        {
            BindingSourceType.Keyboard => input.KeysDown[b.KeyCode] ? 1f : 0f,
            BindingSourceType.Mouse => input.MouseButtonsDown[b.KeyCode] ? 1f : 0f,
            BindingSourceType.MouseAxis => b.GamepadAxis switch { /* scroll → float */ _ => 0f },
            BindingSourceType.GamepadButton => EvaluateGamepadButton(b, input, consumedButtons),
            BindingSourceType.GamepadAxis => EvaluateGamepadAxis(b, input),
            _ => 0f,
        };
    }

    private static Vector2 EvaluateComposite(CompositeAxis2DBinding c, InputState input,
        HashSet<int> consumedKeys)
    {
        float x = 0f, y = 0f;
        if (!consumedKeys.Contains(c.RightKey) && input.KeysDown[c.RightKey]) x += 1f;
        if (!consumedKeys.Contains(c.LeftKey) && input.KeysDown[c.LeftKey]) x -= 1f;
        if (!consumedKeys.Contains(c.UpKey) && input.KeysDown[c.UpKey]) y += 1f;
        if (!consumedKeys.Contains(c.DownKey) && input.KeysDown[c.DownKey]) y -= 1f;
        return new Vector2(x, y);
    }
    // ...
}
```

### 5.8 InputMapStack — 上下文堆栈

```csharp
// ── Kilo.Input/InputMapStack.cs ──

/// <summary>
/// 输入映射上下文堆栈（参考 Unreal EnhancedInputLocalPlayerSubsystem）
/// 管理多个 InputMap 的激活/停用，按优先级处理冲突
/// </summary>
public sealed class InputMapStack
{
    private readonly List<InputMap> _maps = new();
    private readonly Dictionary<string, InputAction> _actionStates = new();

    /// <summary>注册一个 InputMap（可随时注册，不影响激活状态）</summary>
    public void RegisterMap(InputMap map) => _maps.Add(map);

    /// <summary>激活指定 InputMap</summary>
    public void EnableMap(string name) { /* 将标记为 active */ }

    /// <summary>停用指定 InputMap</summary>
    public void DisableMap(string name) { /* 将标记为 inactive */ }

    /// <summary>获取活跃的 Map 列表</summary>
    internal IEnumerable<InputMap> ActiveMaps => _maps.Where(m => /* active */);

    /// <summary>查询 Action 状态（游戏代码使用）</summary>
    public InputAction? GetAction(string name) =>
        _actionStates.TryGetValue(name, out var a) ? a : null;

    // 便捷查询（Stride 风格的简洁 API）
    public bool IsPressed(string action) => GetAction(action)?.IsPressed ?? false;
    public bool JustPressed(string action) => GetAction(action)?.JustPressed ?? false;
    public bool JustReleased(string action) => GetAction(action)?.JustReleased ?? false;
    public float GetFloat(string action) => GetAction(action)?.FloatValue ?? 0f;
    public Vector2 GetVector2(string action) => GetAction(action)?.Vector2Value ?? Vector2.Zero;

    // 内部使用
    internal void SetActionState(string name, ActionType type, bool active, Vector2 vec2, float floatValue) { ... }
    internal bool IsActionConsumed(string name) { ... }
    internal void ConsumeAction(string name) { ... }
}
```

---

## 六、Layer 3 — 游戏代码使用方式

### 6.1 注册（Startup 阶段）

```csharp
// 当前 RenderDemo 的硬编码方式：
if (input.IsKeyDown((int)Key.W) || input.IsKeyDown((int)Key.Up)) pos.Z -= speed;

// 新方式：Startup 阶段注册 InputMap
app.AddSystem(KiloStage.Startup, world =>
{
    var mapStack = world.GetResource<InputMapStack>();

    // 玩家操控映射
    var playerMap = new InputMap { Name = "Player", Priority = 0 };
    playerMap
        .AddMove2D("Move",
            upKey: (int)Key.W, downKey: (int)Key.S,
            leftKey: (int)Key.A, rightKey: (int)Key.D,
            stick: GamepadThumbstick.LeftStick,
            modifiers: [new ScaleModifier { Factor = 5.0f }])
        .AddAction("Jump", ActionType.Button,
            bindings:
            [
                new InputBinding { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.Space },
                new InputBinding { SourceType = BindingSourceType.GamepadButton, GamepadButton = GamepadState.Button.South },
            ])
        .AddAction("Fire", ActionType.Button,
            bindings:
            [
                new InputBinding { SourceType = BindingSourceType.Mouse, KeyCode = 0 /* Left */ },
                new InputBinding { SourceType = BindingSourceType.GamepadButton, GamepadButton = GamepadState.Button.RightShoulder },
            ],
            trigger: new HoldTrigger { Duration = 0.2f })
        .AddAction("ToggleBlur", ActionType.Button,
            bindings:
            [
                new InputBinding { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.B },
            ])
        .AddAction("Screenshot", ActionType.Button,
            bindings:
            [
                new InputBinding { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.P },
            ])
        .AddAction("MoveUp", ActionType.Axis1D,
            bindings:
            [
                new InputBinding { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.E },
            ])
        .AddAction("MoveDown", ActionType.Axis1D,
            bindings:
            [
                new InputBinding { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.Q },
            ]);
    mapStack.RegisterMap(playerMap);
    mapStack.EnableMap("Player");

    // UI 映射（优先级更高，打开菜单时屏蔽玩家操控）
    var uiMap = new InputMap { Name = "UI", Priority = 10 };
    uiMap.AddAction("Pause", ActionType.Button,
        bindings: [new InputBinding { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.Escape }]);
    mapStack.RegisterMap(uiMap);
});
```

### 6.2 查询（Update 阶段）

```csharp
// ── 使用 Action 查询（简洁 Stride 风格）──
app.AddSystem(KiloStage.Update, world =>
{
    var input = world.GetResource<InputMapStack>();

    // 移动（Vector2，帧率无关）
    var move = input.GetVector2("Move");
    pos += new Vector3(move.X, 0, move.Y) * deltaTime;

    // 跳跃
    if (input.JustPressed("Jump")) Jump();

    // 开火（Hold trigger）
    if (input.IsPressed("Fire")) Fire();

    // 切换 Bloom
    if (input.JustPressed("ToggleBlur")) ToggleBloom();

    // 上下
    if (input.GetFloat("MoveUp") > 0f) pos.Y += speed * deltaTime;
    if (input.GetFloat("MoveDown") > 0f) pos.Y -= speed * deltaTime;
});
```

### 6.3 上下文切换

```csharp
// 进入车辆时：禁用步行操控，启用车辆操控
mapStack.DisableMap("Player");
mapStack.EnableMap("Vehicle");

// 打开菜单时：UI 优先级高于玩家操控（通过 Priority 自动屏蔽冲突 Action）
mapStack.EnableMap("UI");
```

### 6.4 运行时重绑定

```csharp
// 玩家在设置菜单中把跳跃改成左 Ctrl
var playerMap = mapStack.GetMap("Player");
playerMap.Rebind("Jump", new InputBinding
{
    SourceType = BindingSourceType.Keyboard,
    KeyCode = (int)Key.ControlLeft,
});
```

---

## 七、Plugin 结构

```
Kilo.Input/                         ← 新项目
├── Actions/
│   ├── InputAction.cs              ← Action 定义 + 状态
│   └── ActionType.cs
├── Bindings/
│   ├── InputBinding.cs             ← 绑定定义
│   └── CompositeAxis2DBinding.cs   ← WASD 组合绑定
├── Modifiers/
│   ├── IInputModifier.cs           ← 修饰器接口
│   ├── ScaleModifier.cs
│   ├── NegateModifier.cs
│   ├── DeadZoneModifier.cs
│   ├── ScaleByDeltaTimeModifier.cs
│   └── ExponentialCurveModifier.cs
├── Triggers/
│   ├── IInputTrigger.cs            ← 触发器接口
│   ├── PressTrigger.cs
│   ├── HoldTrigger.cs
│   ├── DoubleTapTrigger.cs
│   └── PulseTrigger.cs
├── InputMap.cs                     ← 映射表
├── InputMapStack.cs                ← 上下文堆栈
├── Systems/
│   └── InputMapSystem.cs           ← 核心处理系统
└── InputPlugin.cs                  ← IKiloPlugin 实现

Kilo.Window/                        ← 修改现有项目
├── Resources/
│   ├── InputState.cs               ← 增加 Gamepad / MouseButton pressed/released
│   └── GamepadState.cs             ← 新增
├── InputWiring.cs                  ← 增加 Gamepad 事件
├── InputProcessing.cs              ← 新增：死区/迟滞处理
└── WindowPlugin.cs                 ← 无变化
```

---

## 八、实施计划

### Phase 1：Raw Input 增强（1-2 天）

1. `GamepadState` 结构体 + `InputState` 扩展 Gamepad 字段
2. `InputWiring` 接入 Silk.NET Gamepad 事件
3. `InputProcessing` 死区 + 迟滞
4. 鼠标按键增加 Pressed/Released

### Phase 2：Action Mapping 核心（3-5 天）

1. 创建 `Kilo.Input` 项目
2. `InputAction` + `InputBinding` + `CompositeAxis2DBinding`
3. `InputMap` + `InputMapStack`
4. `InputMapSystem` 核心处理逻辑
5. `InputPlugin` 注册

### Phase 3：Modifier + Trigger（2-3 天）

1. `IInputModifier` 接口 + 5 个内置修饰器
2. `IInputTrigger` 接口 + 4 个内置触发器
3. Modifier 链集成到 InputMapSystem

### Phase 4：迁移 + 测试（1-2 天）

1. RenderDemo 迁移到 Action Mapping
2. 测试键盘 / 鼠标 / 手柄
3. 测试上下文切换（玩家→菜单）

---

## 九、关键设计决策总结

| 决策点 | 选择 | 理由 |
|--------|------|------|
| Action 定义方式 | 代码注册（C# 对象） | 避免序列化格式复杂度；未来可加 |
| Composite 输入 | Unity 式 CompositeBinding | 比 Unreal 4 条 Swizzle/Negate 简单，比 Bevy macro 更通用 |
| 值类型 | Button / Axis1D / Axis2D | 覆盖 99% 用例，Axis3D 留未来 |
| 上下文管理 | Unreal 式优先级堆栈 | 最灵活，支持场景切换 / 载具 / 菜单 |
| 输入消费 | 高优先级 Map 消费按键 | 防止菜单和游戏同时响应 Escape |
| 死区 | Bevy 式径向死区 + 线性重映射 | 数学正确，手感好 |
| 查询 API | Stride 式简洁函数 | `input.JustPressed("Jump")` 比 Unity callback 直观 |
| Gamepad | per-InputState 数组（最多 4） | 比 Bevy per-entity 简单，够用 |
| 触发器 | Unreal 式链式 | Hold / DoubleTap / Pulse 足够，Combo 留未来 |
