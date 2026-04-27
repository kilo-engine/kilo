# Kilo Engine — Complete Roadmap

> 目标：让 Kilo 支持制作 2D/3D 游戏的所有基础功能。
> 原则：**框架保证可扩展性，向后兼容一切。**
> 已有模块：`Kilo.ECS` · `Kilo.Rendering` · `Kilo.Physics` · `Kilo.Window` · `Kilo.Assets`

---

## 现状总结

| 模块 | 完成度 | 说明 |
|------|--------|------|
| ECS | ✅ 99% | Friflo 后端封装，完整 Bevy 对标：Query Filter / Changed+Added / State Callback / Run Condition / EntityCommands / EventBus / SystemSet |
| Rendering | 🔄 80% | WebGPU Forward + PBR + Shadow + Particle + MultiCamera + PostProcess + Skybox + Text + Sprite + SkeletalAnim |
| Physics | ✅ 基础完成 | BepuPhysics 刚体 + 碰撞体 + 同步系统 |
| Window/Input | 🔄 基础 | 仅键盘输入，缺鼠标/手柄 |
| Assets | 🔄 基础 | GLTF/Texture 加载，缺通用管线 |
| Audio | ❌ 未开始 | — |
| Scene | ❌ 未开始 | — |
| UI | ❌ 未开始 | — |
| Scripting | ❌ 未开始 | — |

---

## Tier 1 — 基础功能（没有就无法做游戏）

> 每个 Feature 标注影响范围：`2D` / `3D` / `All`

### 1. 输入系统补全 `All`

现在只有键盘，缺少：

- [ ] **鼠标输入** — 位置、按键、滚轮、鼠标锁定（第一人称）
- [ ] **手柄输入** — Gamepad 振动、摇杆、扳机
- [ ] **输入映射层 (InputAction)** — 抽象"跳跃=Space/X键/A键"映射，支持运行时重绑定
- [ ] **触摸输入** — 移动端适配预留接口

> 扩展点：`InputAction` 作为抽象层，底层可插接任何设备

### 2. 场景管理 `All`

- [ ] **Scene 定义与切换** — 场景资源（实体+组件快照），加载/卸载/切换
- [ ] **实体层级序列化** — 将 ECS World 中指定的实体/组件序列化为可持久化格式
- [ ] **场景生命周期** — OnSceneLoad / OnSceneUnload 回调

> 扩展点：场景格式可版本化，未来可加编辑器专用元数据而不破坏运行时

### 3. 音频系统 `All`

- [ ] **Audio Plugin 架构** — `Kilo.Audio` 作为新 Plugin
- [ ] **音频播放** — 播放 WAV/OGG/MP3，支持音量、循环、淡入淡出
- [ ] **AudioSource / AudioListener 组件** — 3D 空间音效（距离衰减、多普勒）
- [ ] **音效分组与总线** — BGM / SFX / Voice 分离控制主音量

> 候选后端：Silk.NET.Audio（跨平台）/ NAudio（Windows 快速验证）
> 扩展点：后端可替换，组件接口稳定

### 4. UI 系统 `All`

- [ ] **UI 布局基础** — 锚点、对齐、边距、自适应（类似 Unity RectTransform 概念）
- [ ] **核心 UI 组件** — Panel、Button、Label、Image、Toggle、Slider
- [ ] **UI 事件系统** — 点击、拖拽、悬停、焦点
- [ ] **UI Canvas / 屏幕空间渲染** — 独立于 3D 相机的正交渲染层
- [ ] **文字输入框** — 文本编辑、IME 支持（中文输入法）

> 扩展点：UI 组件可扩展，未来加 ScrollView、DropDown 等复合组件不影响基类

### 5. 物理 — 碰撞事件与触发器 `2D/3D`

现在有 BepuPhysics 同步，但缺少事件层：

- [ ] **碰撞回调组件** — OnCollisionEnter / OnCollisionExit / OnTriggerExit
- [ ] **Trigger Zone** — 不产生物理推力但检测重叠的碰撞体
- [ ] **Raycast / ShapeCast 查询** — 射线检测（鼠标拾取、弹道检测、视线判断）
- [ ] **物理层 (Layer Mask)** — 按层过滤碰撞对

### 6. 2D 游戏基础 `2D`

- [ ] **SpriteSheet / Atlas** — 精灵图集加载与切割
- [ ] **帧动画系统** — SpriteAnimation 组件，基于 SpriteSheet 的逐帧动画
- [ ] **Tilemap** — 瓦片地图渲染 + 简单碰撞（与物理系统集成）
- [ ] **2D 相机** — 正交投影（现有 Camera 可能需要 Ortho 模式）
- [ ] **2D 物理辅助** — BepuPhysics 2D 模式或 2D 碰撞体快捷封装

### 7. 游戏流程基础设施 `All`

- [ ] **计时器 / 协程** — 异步等待、延迟执行、Cooldown 计时器（不依赖 Unity 式协程，可用 C# async/task 或 ECS State）
- [ ] **游戏状态机** — GameState (MainMenu → Loading → Playing → Paused → GameOver)
- [ ] **相机控制器** — Follow Camera（跟随角色）、Orbit Camera（编辑器式旋转）、FirstPerson Controller

### 8. 渲染 — 游戏必需补全 `3D`

已在 `roadmap-rendering.md` 中列出，此处合并优先级：

- [ ] **Debug 绘制** — 线框/碰撞盒/坐标轴/BoundingSphere 可视化（调试物理/场景硬依赖）
- [ ] **雾效 (Fog)** — 线性雾/指数雾，集成 Forward pass
- [ ] **级联阴影 (CSM)** — 户外大场景阴影质量
- [ ] **透明物体排序** — Back-to-front 或 OIT，当前半透明渲染可能不正确

### 9. 资产管线增强 `All`

- [ ] **资产引用解析** — 通过路径/UUID 引用资产，而非手动传 Handle
- [ ] **资产热加载** — 运行时重新加载修改过的资产（开发期调试用）
- [ ] **异步加载** — 后台加载大资产，提供加载进度

---

## Tier 2 — 便利性功能（提升开发效率，非硬依赖）

### 编辑器工具链

- [ ] **运行时 Inspector** — 显示/编辑选中实体的组件属性
- [ ] **Scene Hierarchy 视图** — 实体树形列表，选中高亮
- [ ] **Gizmo 工具** — 平移/旋转/缩放操作手柄
- [ ] **Stats 叠加层** — FPS、DrawCall、三角形数、GPU 时间
- [ ] **Console 日志面板** — 运行时查看 Log 输出

### 资产/工作流

- [ ] **Shader 热重载** — 修改 WGSL 后自动重编译
- [ ] **Prefab / 模板系统** — 实体模板，实例化复用
- [ ] **动画状态机 (Animator)** — 动画片段之间的过渡条件、混合树
- [ ] **粒子效果参数调节器** — 实时预览粒子效果

### 脚本/逻辑

- [x] **Commands 延迟执行 API** — `KiloCommands` + `KiloEntityCommands` 类型化延迟操作
- [ ] **Query Entities() 零分配优化** — int→EntityId 转换导致堆分配，改为 caller-provided buffer 或 ref struct 包装
- [ ] **可视化脚本节点** — 如果需要非程序员参与（优先级极低）
- [x] **事件总线 (EventBus)** — `KiloEvents<T>` 双缓冲事件总线 + `SendEvent<T>` / `ReadEvents<T>` 快捷方法

### ECS 核心能力补全（Bevy 对标）

- [x] **Query Filter (With/Without)** — `KiloQuery<T1>.With<TFilter>()` / `.Without<TExclude>()`
- [x] **Query Filter (Changed/Added)** — `Changed()` / `Added()` query 级过滤
- [x] **State Enter/Exit/Transition 回调** — `OnEnter<S>` / `OnExit<S>` / `OnTransition<S>`
- [x] **Run Condition** — `.run_if(condition)` + `resource_exists` + `state_equals`
- [x] **EntityCommands 类型化延迟** — `KiloCommands` + `KiloEntityCommands`
- [x] **Remove Resource** — `KiloWorld.RemoveResource<T>()`
- [x] **World Entity Iteration** — `KiloWorld.IterEntities()`
- [x] **Dynamic Component (Get/Set by ID)** — `Get(entity, componentId)` / `Set(entity, componentId, boxed)` + `GetComponentType()`
- [x] **System Set** — `AddSystemToSet()` + `ConfigureSet()` 含状态守卫
- [x] **Or Filter** — `Or<TA, TB>()` 组合过滤
- [x] **One-shot Systems** — `KiloApp.RunSystemOnce()`
- [x] **Entity Clone** — `KiloWorld.CloneEntity()` 基于 Friflo 原生
- [x] **Event Bus** — `KiloEvents<T>` 双缓冲事件总线 + `SendEvent<T>` / `ReadEvents<T>`
- [ ] **Query Transmutation** — `Query::transmute_lens()` 高级优化（低优先级，Friflo 不直接支持）

### 网络

- [ ] **网络抽象层** — Client/Server 架构接口
- [ ] **状态同步** — 实体组件的网络同步
- [ ] **远程过程调用 (RPC)** — 网络函数调用

---

## Tier 3 — 性能与画质优化

> 这些功能不影响可扩展性框架设计，可在任意时间点插入。

### 渲染性能

- [ ] **GPU Instancing** — 相同 Mesh 批量绘制（植被、道具、子弹）
- [ ] **GPU Driven Pipeline** — Indirect Draw + Compute Culling
- [ ] **多线程 Command Buffer 提交**
- [ ] **Texture Streaming / Mipmap 自动生成**

### 渲染质量

- [ ] **环境贴图 / IBL** — 基于图像的环境光照
- [ ] **SSAO** — 屏幕空间环境遮蔽
- [ ] **SSR** — 屏幕空间反射
- [ ] **DOF** — 景深
- [ ] **运动模糊** — 速度缓冲后处理
- [ ] **Color Grading / LUT** — 色彩风格化
- [ ] **曝光控制 / 自动曝光** — HDR Eye Adaptation
- [ ] **MSAA / TAA** — 抗锯齿升级
- [ ] **动态天空** — 日夜循环 / 大气散射

### 高级特性

- [ ] **延迟渲染路径 (Deferred Rendering)** — G-Buffer + 延迟光照
- [ ] **SSGI** — 屏幕空间全局光照
- [ ] **Virtual Shadow Maps**
- [ ] **Mesh Shader** (WebGPU 扩展)
- [ ] **地形渲染** — 高度图 + LOD + 层叠材质
- [ ] **植被/草渲染** — GPU Instanced + 距离淡出
- [ ] **电影/过场动画系统** — Timeline / Camera Rig / Cutscene

---

## 建议实施顺序

```
Phase 1 — 让一个简单游戏跑起来（约 4-6 周）
├── 输入补全（鼠标 + 手柄）
├── UI 系统（Canvas + Button + Label）
├── 音频系统（基础播放 + 3D 音效）
├── 物理 Raycast + 碰撞事件
├── SpriteSheet + 帧动画
└── 相机控制器（Follow + Orbit）

Phase 2 — 让一个完整游戏可发布（约 6-8 周）
├── 场景管理（加载/切换/序列化）
├── Tilemap 系统
├── 渲染 Debug 绘制
├── 雾效 + CSM 阴影
├── 透明排序
├── 资产异步加载 + 引用解析
├── 游戏状态机 + 计时器
└── 物理层过滤

Phase 3 — 开发体验提升（持续迭代）
├── Shader 热重载
├── Prefab 模板
├── 运时 Inspector / Hierarchy
├── 动画状态机
└── Stats + Console 面板

Phase 4 — 按需优化（根据实际游戏需求选择）
├── GPU Instancing
├── IBL / SSAO / SSR
├── CSM
├── 地形渲染
├── 网络层
└── ...（其余 Tier 3 项目按需）
```

---

## 架构扩展性检查清单

为确保未来所有 Tier 2/3 功能可无缝加入，当前框架应满足：

- [x] Plugin 架构 — 新功能以 Plugin 形式注入
- [x] ECS 组件可自由扩展
- [x] RenderGraph 支持 Pass 动态注册
- [ ] **Driver 抽象层验证** — 确保 IRenderDriver 接口可支持未来 Vulkan/DX12 后端
- [ ] **Audio Backend 抽象** — 音频后端可替换
- [ ] **Scene 序列化格式版本化** — 向后兼容旧场景文件
- [ ] **InputAction 映射层** — 输入与逻辑解耦
