# Kilo.Input — 实现总结

## 项目结构

```
src/Kilo.Input/
├── Actions/
│   ├── ActionDef.cs           ← 动作定义（内部类型）
│   ├── InputAction.cs         ← 运行时动作状态（JustPressed/JustReleased/IsPressed）
│   └── ActionType.cs          ← Button / Axis1D / Axis2D
├── Bindings/
│   ├── BindingSourceType.cs   ← Keyboard / Mouse / GamepadButton / GamepadAxis / GamepadThumbstick
│   ├── CompositeAxis2D.cs     ← WASD→Vector2 组合绑定
│   ├── GamepadAxis.cs         ← LeftTrigger / RightTrigger
│   ├── GamepadThumbstick.cs   ← LeftStick / RightStick
│   └── InputBinding.cs        ← 单个物理绑定
├── Contexts/
│   ├── InputMap.cs            ← 动作映射表（builder 模式）
│   └── InputMapStack.cs       ← 上下文堆栈 + 查询 API
├── Modifiers/
│   ├── IInputModifier.cs      ← 修饰器接口
│   ├── ScaleModifier.cs       ← 缩放（灵敏度）
│   ├── NegateModifier.cs      ← 反转轴
│   ├── DeadZoneModifier.cs    ← 死区
│   └── ScaleByDeltaModifier.cs ← 帧率无关缩放
├── Processing/
│   └── DeadZone.cs            ← 径向死区 + 迟滞
├── Systems/
│   └── InputMapSystem.cs      ← 核心管线：Raw→Binding→Modifier→Trigger→Action
├── Triggers/
│   ├── IInputTrigger.cs       ← 触发器接口
│   ├── TriggerState.cs        ← None / Ongoing / Triggered
│   ├── PressTrigger.cs        ← 按下即触发
│   ├── HoldTrigger.cs         ← 长按触发
│   └── PulseTrigger.cs        ← 脉冲连发
└── InputPlugin.cs             ← IKiloPlugin 实现

src/Kilo.Window/（增强）
├── Resources/
│   ├── InputState.cs          ← 扩展：Gamepad数组 + 鼠标Pressed/Released
│   └── GamepadState.cs        ← 新增：摇杆/扳机/按钮/死区/振动
├── InputWiring.cs             ← 扩展：Gamepad事件 + 鼠标Pressed/Released
└── InputProcessing.cs         ← 新增：径向死区 + 迟滞

tests/Kilo.Input.Tests/（51 项测试）
├── DeadZoneTests.cs
├── ModifierTests.cs
├── TriggerTests.cs
├── InputMapTests.cs
├── InputMapStackTests.cs
└── InputMapSystemTests.cs
```

## 使用方式

```csharp
// 1. 注册 InputMap（Startup 阶段）
var playerMap = new InputMap("Player", priority: 0);
playerMap
    .AddAxis2D("Move", (int)Key.W, (int)Key.S, (int)Key.A, (int)Key.D,
        stick: GamepadThumbstick.LeftStick,
        modifiers: [new ScaleModifier { Factor = 5.0f }])
    .AddAction("Jump", ActionType.Button,
    [
        new() { SourceType = BindingSourceType.Keyboard, KeyCode = (int)Key.Space },
        new() { SourceType = BindingSourceType.GamepadButton, GamepadButton = (int)GamepadButton.South },
    ]);

var stack = world.GetResource<InputMapStack>();
stack.Register(playerMap);
stack.Enable("Player");

// 2. 每帧处理（系统自动执行）
stack.BeginFrame();
inputMapSystem.Update(inputState, stack, deltaTime);

// 3. 查询（Stride 风格简洁 API）
var move = stack.GetVector2("Move");   // Vector2
if (stack.JustPressed("Jump")) Jump();
```

## 设计决策

| 决策 | 选择 | 参考 |
|------|------|------|
| 管线架构 | 4 层: 平台→原始状态→动作映射→游戏逻辑 | 用户蓝图 |
| Composite 输入 | Unity 式 CompositeAxis2D (WASD→Vector2) | 比 Unreal 4 条 Swizzle/Negate 简单 |
| 上下文管理 | Unreal 式优先级堆栈 + 输入消费 | 最灵活 |
| 查询 API | Stride 式 `stack.JustPressed("Jump")` | 最简洁 |
| 死区 | Bevy 式径向死区 + 线性重映射 | 数学正确 |
| 触发器 | Unreal 式 IInputTrigger 链 | Hold/Pulse |
| 修饰器 | Unreal 式 IInputModifier 链 | Scale/Negate/DeadZone |
| 值类型 | 全 struct | ECS 友好，零 GC |
