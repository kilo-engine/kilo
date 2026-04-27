# Stride 引擎使用文档

> 面向开发者的 Stride 游戏引擎使用指南  
> 基于官方文档与源码 API 整理

---

## 一、安装与项目创建

### 1.1 系统要求

- **操作系统**：Windows 10/11（编辑器）、Linux（运行时）、Android、iOS、macOS
- **开发工具**：Visual Studio 2022+（Community 版即可）
- **工作负载**：
  - .NET desktop development（含 .NET Framework 4.7.2 targeting pack）
  - Desktop development with C++（含 Windows SDK、MSVC v143）
  - 可选：.NET MAUI（用于 Android/iOS 开发）
- **运行时**：.NET 10.0 SDK

### 1.2 安装 Stride

1. 访问 [stride3d.net](https://stride3d.net) 下载 Stride Launcher
2. 通过 Launcher 安装最新版 Stride 编辑器（Game Studio）
3. 首次启动时配置默认 IDE（Visual Studio / VS Code）

### 1.3 创建新项目

**通过 Game Studio：**
1. 打开 Game Studio → `New Project`
2. 选择模板：
   - **Empty Project**：空白项目，适合自定义开发
   - **First Person Shooter**：第一人称射击模板
   - **Third Person Platformer**：第三人称平台跳跃模板
   - **Top Down RPG**：俯视角色扮演模板
   - **Physics Sample**：物理示例模板
3. 输入项目名称与路径，点击 `Create`

**通过命令行（Code-Only 方式）：**
```bash
dotnet new install Stride.Templates
dotnet new stride-game -o MyGame
```

---

## 二、Game Studio 编辑器概览

### 2.1 主界面布局

| 面板 | 功能 |
|------|------|
| **Scene Editor** | 3D 场景视口，支持实体选择、变换操作、摄像机导航 |
| **Entity Hierarchy** | 场景中所有实体的层级树 |
| **Property Grid** | 选中实体/组件的属性编辑 |
| **Asset View** | 项目资产列表（模型、材质、纹理、脚本等） |
| **Asset Preview** | 资产预览窗口 |
| **Toolbar** | 播放/暂停、变换工具（移动/旋转/缩放）、渲染模式切换 |

### 2.2 常用快捷键

| 快捷键 | 功能 |
|--------|------|
| `F` | 聚焦选中实体 |
| `W` / `E` / `R` | 切换移动 / 旋转 / 缩放工具 |
| `Ctrl + D` | 复制选中实体 |
| `Delete` | 删除选中实体 |
| `Ctrl + P` | 播放场景 |
| `Alt + 鼠标左键` | 旋转视口 |
| `Alt + 鼠标中键` | 平移视口 |
| `Alt + 鼠标右键` | 缩放视口 |

### 2.3 资产管理

- 支持拖拽导入外部资源（FBX、PNG、JPG、WAV、MP3 等）
- 资产在项目中以 `.sd*` 格式存储（如 `.sdmat` 材质、`.sdscene` 场景）
- 编译时自动转换为运行时二进制格式

---

## 三、Entity-Component-System (ECS) 使用

### 3.1 实体 (Entity)

实体是场景中的基本对象，本身不包含逻辑，仅作为组件的容器。

**在 Game Studio 中：**
- 右键 Scene → `Add Entity`
- 在 Entity Hierarchy 中管理父子关系

**在代码中：**

```csharp
// 创建实体
var entity = new Entity("MyEntity")
{
    Transform =
    {
        Position = new Vector3(0, 1, 0),
        Rotation = Quaternion.RotationY(MathUtil.DegreesToRadians(45)),
        Scale = new Vector3(1, 1, 1)
    }
};

// 添加到场景
SceneSystem.SceneInstance.RootScene.Entities.Add(entity);

// 查找实体
var found = SceneSystem.SceneInstance.RootScene.Entities.FirstOrDefault(e => e.Name == "MyEntity");
```

### 3.2 组件 (Component)

Stride 内置常用组件：

| 组件 | 功能 |
|------|------|
| `TransformComponent` | 位置、旋转、缩放（每个实体默认自带） |
| `ModelComponent` | 渲染 3D 模型 |
| `CameraComponent` | 摄像机 |
| `LightComponent` | 光源（方向光、点光源、聚光灯） |
| `SpriteComponent` | 2D 精灵渲染 |
| `RigidbodyComponent` | 物理刚体（Bullet） |
| `BodyComponent` | 物理刚体（BepuPhysics） |
| `AudioEmitterComponent` | 3D 音频发射器 |
| `BackgroundComponent` | 天空盒/背景 |

**添加组件示例：**

```csharp
// 添加模型组件
var modelComponent = new ModelComponent
{
    Model = Content.Load<Model>("MyModel")
};
entity.Add(modelComponent);

// 添加碰撞体
var collider = new StaticColliderComponent
{
    ColliderShape = new BoxColliderShapeDesc
    {
        Size = new Vector3(1, 1, 1)
    }
};
entity.Add(collider);
```

### 3.3 场景 (Scene) 与场景实例

```csharp
// 获取当前场景实例
var sceneInstance = SceneSystem.SceneInstance;

// 获取根场景
var rootScene = sceneInstance.RootScene;

// 场景嵌套
var subScene = new Scene();
rootScene.Children.Add(subScene);

// 场景偏移
subScene.Offset = new Vector3(100, 0, 0);
```

---

## 四、脚本系统

Stride 脚本使用 C# 编写，通过附加到实体上的 `ScriptComponent` 实现游戏逻辑。

### 4.1 脚本类型

| 脚本基类 | 执行时机 | 用途 |
|----------|----------|------|
| `SyncScript` | 每帧 `Update()` | 常规游戏逻辑 |
| `AsyncScript` | 独立异步方法 | 长时间运行、协程式逻辑 |
| `StartupScript` | 场景加载时 `Start()` | 初始化逻辑 |

### 4.2 SyncScript 示例

```csharp
using Stride.Engine;
using Stride.Input;
using Stride.Core.Mathematics;

public class PlayerController : SyncScript
{
    public float Speed { get; set; } = 5.0f;

    public override void Update()
    {
        // 读取输入
        var movement = Vector3.Zero;
        if (Input.IsKeyDown(Keys.W)) movement.Z -= 1;
        if (Input.IsKeyDown(Keys.S)) movement.Z += 1;
        if (Input.IsKeyDown(Keys.A)) movement.X -= 1;
        if (Input.IsKeyDown(Keys.D)) movement.X += 1;

        // 移动实体
        if (movement != Vector3.Zero)
        {
            movement.Normalize();
            Entity.Transform.Position += movement * Speed * (float)Game.UpdateTime.Elapsed.TotalSeconds;
        }
    }
}
```

### 4.3 AsyncScript 示例

```csharp
using System.Threading.Tasks;
using Stride.Engine;

public class DoorScript : AsyncScript
{
    public override async Task Execute()
    {
        while (Game.IsRunning)
        {
            // 等待 3 秒
            await Task.Delay(3000);
            
            // 开门动画
            Entity.Transform.Rotation *= Quaternion.RotationY(MathUtil.PiOverTwo);
            
            // 再等待 3 秒
            await Task.Delay(3000);
            
            // 关门
            Entity.Transform.Rotation *= Quaternion.RotationY(-MathUtil.PiOverTwo);
        }
    }
}
```

### 4.4 StartupScript 示例

```csharp
using Stride.Engine;

public class GameInitializer : StartupScript
{
    public override void Start()
    {
        // 游戏启动时执行一次
        Log.Info("Game started!");
        
        // 初始化随机种子
        RandomSeed = 12345;
    }
}
```

### 4.5 脚本生命周期与可用服务

`ScriptComponent` 提供以下便捷属性：

```csharp
public abstract class ScriptComponent : EntityComponent
{
    public IGame Game { get; }
    public GraphicsDevice GraphicsDevice { get; }
    public ContentManager Content { get; }
    public InputManager Input { get; }
    public SceneSystem SceneSystem { get; }
    public EffectSystem EffectSystem { get; }
    public AudioSystem Audio { get; }
    public ScriptSystem Script { get; }
    public DebugTextSystem DebugText { get; }
    public StreamingManager Streaming { get; }
}
```

### 4.6 脚本优先级

通过 `Priority` 属性控制脚本执行顺序（值越小越先执行）：

```csharp
[DefaultValue(0)]
public int Priority { get; set; }
```

---

## 五、资源加载与管理

### 5.1 ContentManager

```csharp
// 加载模型
var model = Content.Load<Model>("MyModel");

// 加载纹理
var texture = Content.Load<Texture>("MyTexture");

// 加载材质
var material = Content.Load<Material>("MyMaterial");

// 加载场景
var scene = Content.Load<Scene>("MyScene");
SceneSystem.SceneInstance.RootScene = scene;
```

### 5.2 运行时创建材质

```csharp
var material = Material.New(GraphicsDevice, new MaterialDescriptor
{
    Attributes =
    {
        Diffuse = new MaterialDiffuseMapFeature(new ComputeTextureColor()),
        DiffuseModel = new MaterialDiffuseLambertModelFeature()
    }
});

// 设置漫反射颜色
material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, new Color4(1, 0, 0, 1));
```

### 5.3 运行时创建模型

```csharp
var meshDraw = new MeshDraw
{
    PrimitiveType = PrimitiveType.TriangleList,
    DrawCount = indices.Length,
    IndexBuffer = new IndexBufferBinding(indexBuffer, false, indices.Length),
    VertexBuffers = new[] { new VertexBufferBinding(vertexBuffer, VertexPositionNormalTexture.Layout, vertexCount) }
};

var mesh = new Mesh { Draw = meshDraw };
var model = new Model { Meshes = { mesh } };
entity.Add(new ModelComponent { Model = model });
```

---

## 六、物理系统

> **注意**：Bullet Physics 正在逐步淘汰，官方推荐使用 Bepu Physics。

### 6.1 Bepu Physics 基础

**静态碰撞体（不可移动）：**
```csharp
var staticCollider = new StaticColliderComponent
{
    ColliderShape = new BoxColliderShapeDesc { Size = new Vector3(10, 1, 10) }
};
groundEntity.Add(staticCollider);
```

**动态刚体：**
```csharp
var body = new BodyComponent
{
    ColliderShape = new SphereColliderShapeDesc { Radius = 0.5f }
};
ballEntity.Add(body);

// 施加冲量
body.ApplyImpulse(new Vector3(0, 5, 0));
```

**运动学物体（代码控制）：**
```csharp
var kinematic = new BodyComponent
{
    Kinematic = true,
    ColliderShape = new BoxColliderShapeDesc { Size = new Vector3(2, 0.2f, 2) }
};
platformEntity.Add(kinematic);
```

### 6.2 触发器

```csharp
public class TriggerScript : SyncScript
{
    private BodyComponent body;

    public override void Start()
    {
        body = Entity.Get<BodyComponent>();
    }

    public override void Update()
    {
        foreach (var contact in body.Contacts)
        {
            var other = contact.ContactHeader.Body == body 
                ? contact.ContactHeader.OtherBody 
                : contact.ContactHeader.Body;
                
            Log.Info($"Collided with: {other.Entity.Name}");
        }
    }
}
```

### 6.3 射线检测

```csharp
var simulation = SceneSystem.SceneInstance.GetProcessor<PhysicsProcessor>()?.Simulation;
if (simulation != null)
{
    var ray = new Ray(Entity.Transform.Position, Entity.Transform.WorldMatrix.Forward);
    var hit = simulation.RayCast(ray, maxDistance: 100f);
    
    if (hit.Succeeded)
    {
        Log.Info($"Hit: {hit.Collider.Entity.Name}");
    }
}
```

---

## 七、音频系统

### 7.1 播放音效

```csharp
// 加载音频资产
var sound = Content.Load<Sound>("Explosion");

// 创建实例并播放
var instance = sound.CreateInstance();
instance.Play();

// 3D 空间音频
var emitter = Entity.Get<AudioEmitterComponent>();
emitter.Sound = sound;
emitter.Play();
```

### 7.2 背景音乐

```csharp
var music = Content.Load<Sound>("BackgroundMusic");
music.IsCompressed = true;  // 流式播放
var musicInstance = music.CreateInstance();
musicInstance.IsLooped = true;
musicInstance.Play();
```

---

## 八、UI 系统

### 8.1 创建 UI 页面

在 Game Studio 中：
1. Asset View → `Add asset` → `UI` → `UI Page`
2. 双击打开 UI 编辑器
3. 拖拽控件（TextBlock、Button、ImageElement 等）到画布
4. 在脚本中加载并显示：

```csharp
var uiPage = Content.Load<UIPage>("MyUI");
var uiComponent = Entity.GetOrCreate<UIComponent>();
uiComponent.Page = uiPage;
```

### 8.2 代码中操作 UI

```csharp
// 获取根元素
var root = uiPage.RootElement;

// 查找文本控件
var textBlock = root.FindName("ScoreText") as TextBlock;
textBlock.Text = "Score: 100";

// 查找按钮并添加事件
var button = root.FindName("StartButton") as Button;
button.Click += (sender, args) =>
{
    Log.Info("Button clicked!");
};
```

### 8.3 常用 UI 控件

| 控件 | 说明 |
|------|------|
| `TextBlock` | 文本显示 |
| `EditText` | 可编辑文本（移动端自动调出软键盘） |
| `Button` | 按钮 |
| `ImageElement` | 图片 |
| `StackPanel` | 垂直/水平堆叠布局（支持虚拟化） |
| `Grid` | 网格布局 |
| `Canvas` | 绝对定位画布 |
| `ScrollViewer` | 滚动容器 |

---

## 九、摄像机与渲染

### 9.1 摄像机设置

```csharp
var cameraEntity = new Entity("Camera")
{
    Transform = { Position = new Vector3(0, 2, 5) }
};

cameraEntity.Add(new CameraComponent
{
    Projection = CameraProjectionMode.Perspective,
    NearClipPlane = 0.1f,
    FarClipPlane = 1000f,
    VerticalFieldOfView = 60f
});

SceneSystem.SceneInstance.RootScene.Entities.Add(cameraEntity);
```

### 9.2 切换 Graphics Compositor

```csharp
// 运行时切换渲染合成器
var newCompositor = Content.Load<GraphicsCompositor>("MyCompositor");
SceneSystem.SceneInstance.RootScene.Settings.GraphicsCompositor = newCompositor;
```

---

## 十、构建与发布

### 10.1 调试运行

- 在 Game Studio 中点击 `Play` 按钮直接运行
- 或按 `F5` 在 Visual Studio 中启动调试

### 10.2 发布平台

1. Game Studio → `Build` → `Publish`
2. 选择目标平台：
   - Windows Desktop
   - Linux
   - Android
   - iOS
3. 配置发布选项（代码剥离、AOT 编译等）
4. 输出到 `bin/Release/`

### 10.3 代码剥离与 AOT

- 对于移动端和独立部署，启用 `PublishTrimmed` 减少体积
- 对于 iOS 等平台，可能需要启用 `PublishAot`

---

## 十一、最佳实践

1. **优先使用 Bepu Physics**：Bullet Physics 已停止维护
2. **避免在 Update 中频繁分配对象**：使用对象池或预分配集合
3. **利用 AsyncScript 处理延迟逻辑**：避免在 SyncScript 中使用 `Thread.Sleep`
4. **合理设置脚本 Priority**：确保输入处理在移动逻辑之前执行
5. **Content.Load 结果缓存**：避免每帧重复加载相同资源
6. **使用 EffectSystem 异步编译着色器**：复杂材质可能导致首次加载卡顿
7. **利用 VisibilityGroup 控制渲染层级**：通过 RenderGroup 优化场景剔除

---

## 十二、常用 API 速查

| 功能 | API |
|------|-----|
| 获取输入 | `Input.IsKeyDown(Keys key)` |
| 获取鼠标位置 | `Input.MousePosition` |
| 获取 Delta Time | `(float)Game.UpdateTime.Elapsed.TotalSeconds` |
| 实体前向向量 | `Entity.Transform.WorldMatrix.Forward` |
| 世界坐标转屏幕坐标 | `CameraComponent.WorldToScreenPoint(Vector3 worldPos)` |
| 打印调试文本 | `DebugText.Print("Hello", new Int2(10, 10))` |
| 加载场景 | `Content.Load<Scene>("SceneName")` |
| 实例化预设 | `Content.Load<Prefab>("PrefabName").Instantiate()` |
| 射线检测 | `simulation.RayCast(ray, maxDistance)` |
| 播放动画 | `Entity.Get<AnimationComponent>().Play("Idle")` |
