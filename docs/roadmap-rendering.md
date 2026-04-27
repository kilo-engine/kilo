# Kilo.Rendering Roadmap

基于 WebGPU 的前向渲染管线，采用 RenderGraph 架构管理 GPU 资源生命周期。

## 已完成

### 核心管线
- [x] Forward Rendering Pipeline
- [x] RenderGraph（自动资源回收、Pass依赖排序）
- [x] Compute Shader 支持

### 光照与阴影
- [x] PBR 光照模型（CookTorrance BRDF, Metallic-Roughness 工作流）
- [x] 方向光 (DirectionalLight)
- [x] 点光源 (PointLight)，最多 64 个
- [x] Shadow Map (2048x2048, 单级级联)

### 模型与动画
- [x] GLTF/GLB 模型加载（网格、材质、纹理）
- [x] 骨骼动画 (SkinnedMesh + AnimationPlayer)
- [x] 骨骼权重蒙皮 (GPU Joint Matrices)

### 材质系统
- [x] Material / MaterialInstance
- [x] Pipeline Cache / Shader Cache
- [x] 动态 Uniform Offset（实例化）
- [x] 法线贴图 (Normal Mapping)
- [x] 天空盒 (Skybox)

### 优化
- [x] 视锥剔除 (FrustumCullingSystem)
- [x] GPU Scene Data 批量上传
- [x] RenderGraph Resource Pool

### 其他
- [x] 2D 精灵渲染 (SpriteRenderSystem)
- [x] GPU 文字渲染 (HarfBuzz 字体整形)
- [x] Compute Blur 后处理
- [x] HDR 渲染 (RGBA16Float 中间纹理)
- [x] 后处理管线 — ACES ToneMapping + Bloom + FXAA，接入 RenderGraph

---

## P1 — 游戏必需功能（无这些功能无法开发完整3D游戏）

> 标记 ★ 的为最高优先级，属于"没有就做不了游戏"的硬依赖。

- [ ] ★ **粒子系统** — GPU Compute Particle，基础发射器。爆炸、火焰、烟雾、魔法效果全部依赖此功能
- [ ] ★ **多相机 / RenderTexture** — UI 叠加层、小地图、分屏、CCTV、镜面反射。没有此功能无法正确分层渲染 UI 与 3D 场景
- [ ] ★ **Debug 绘制** — 线框/碰撞盒/坐标轴/BoundingSphere 可视化。无此功能无法调试物理和场景
- [ ] ★ **雾效 (Fog)** — 线性雾/指数雾，集成到 Forward pass。户外场景氛围 + 遮挡远处 LOD 切换
- [ ] ★ **级联阴影 (CSM)** — 户外大场景阴影质量。单级 Shadow Map 无法覆盖大场景
- [ ] **Color Grading / LUT** — 色彩风格化。没有色调控制，画面始终"引擎默认"感觉
- [ ] **渲染统计面板** — Draw Call 数、三角形数、GPU 时间、FPS。没有性能数据无法优化
- [ ] **动态天空系统** — 日夜循环、大气散射（至少 Preetham/HosekWilkie 模型）。户外游戏必备
- [ ] **GPU Instancing** — 相同网格批量绘制。植被、道具、子弹等大量重复物体必备
- [ ] **Texture Streaming / Mipmap 自动生成** — 大纹理加载与 LOD。大场景内存管理必需

## P2 — 场景表现力

- [ ] **环境贴图 / IBL** — 基于图像的环境光照，金属材质反射
- [ ] **地形渲染 (Terrain)** — 高度图 + LOD + 层叠材质。RPG/开放世界必需
- [ ] **植被/草渲染** — GPU Instanced 草/树，视锥剔除 + 距离淡出
- [ ] **曝光控制 / 自动曝光** — Eye Adaptation，HDR 场景明暗适应
- [ ] **抗锯齿扩展** — MSAA 多重采样 / TAA 时间抗锯齿（FXAA 已有）

## P3 — 渲染质量提升

- [ ] **Screen Space Ambient Occlusion (SSAO)** — 环境遮蔽
- [ ] **Screen Space Reflections (SSR)** — 实时反射
- [ ] **景深 (DOF)** — 相机焦点效果，电影感
- [ ] **运动模糊 (Motion Blur)** — 速度缓冲 + 后处理
- [ ] **透明物体排序** — OIT 或 Back-to-front 排序

## P4 — 性能与高级特性

- [ ] **GPU Driven Pipeline** — Indirect Draw + Compute Culling
- [ ] **多线程 Command Buffer 提交**
- [ ] **Deferred Rendering Path** — G-Buffer + 延迟光照
- [ ] **Screen Space Global Illumination (SSGI)**
- [ ] **Virtual Shadow Maps**
- [ ] **Mesh Shader / Amplification Shader** (WebGPU 扩展)

## P5 — 工具链

- [ ] **Shader 热重载** — 开发迭代效率
- [ ] **Material Editor** — 可视化材质编辑器
- [ ] **电影/过场动画系统** — Timeline / Camera Rig / Cutscene

---

> P0 已全部完成。**P1 为游戏开发硬依赖**，建议按 ★ 顺序依次实现：粒子系统 → 多相机/RenderTexture → Debug绘制 → 雾效 → CSM。
