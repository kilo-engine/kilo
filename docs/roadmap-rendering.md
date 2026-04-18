# Kilo.Rendering Roadmap

基于 WebGPU 的前向渲染管线，采用 RenderGraph 架构管理 GPU 资源生命周期。

## 已完成

### 核心管线
- [x] Forward Rendering Pipeline
- [x] RenderGraph（自动资源回收、Pass依赖排序）
- [x] Compute Shader 支持

### 光照与阴影
- [x] Phong 光照模型
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

### 优化
- [x] 视锥剔除 (FrustumCullingSystem)
- [x] GPU Scene Data 批量上传
- [x] RenderGraph Resource Pool

### 其他
- [x] 2D 精灵渲染 (SpriteRenderSystem)
- [x] GPU 文字渲染 (HarfBuzz 字体整形)
- [x] Compute Blur 后处理

## 进行中 / 短期

### P0 — 视觉质量基线
- [ ] **PBR 材质** — Metallic-Roughness 工作流，替代当前 Phong 光照
- [ ] **法线贴图 (Normal Mapping)** — 表面细节，依赖切线数据
- [ ] **后处理管线** — ToneMapping + Bloom + FXAA/TAA，接入 RenderGraph

### P1 — 场景表现力
- [ ] **级联阴影 (CSM)** — 户外大场景阴影质量
- [ ] **天空盒 / 环境贴图** — Skybox + IBL 环境光照
- [ ] **粒子系统** — GPU Compute Particle，基础发射器

## 中期

### P2 — 渲染质量提升
- [ ] **Screen Space Ambient Occlusion (SSAO)** — 环境遮蔽
- [ ] **Screen Space Reflections (SSR)** — 实时反射
- [ ] **HDR 渲染** — HDR color buffer + ACES ToneMapping
- [ ] **透明物体排序** — OIT 或 Back-to-front 排序

### P3 — 性能与扩展
- [ ] **GPU Instancing** — 相同网格批量绘制
- [ ] **GPU Driven Pipeline** — Indirect Draw + Compute Culling
- [ ] **多线程 Command Buffer 提交**
- [ ] **Texture Streaming / Mipmap 自动生成**

## 远期

### P4 — 高级特性
- [ ] **Deferred Rendering Path** — G-Buffer + 延迟光照
- [ ] **Image-Based Lighting (IBL)** — 基于图像的全局光照
- [ ] **Screen Space Global Illumination (SSGI)**
- [ ] **Virtual Shadow Maps**
- [ ] **Mesh Shader / Amplification Shader** (WebGPU 扩展)

---

> MVP 阶段建议优先完成 **P0**（PBR + 法线贴图 + 后处理），即可达到大多数 3D 游戏的视觉基线。
