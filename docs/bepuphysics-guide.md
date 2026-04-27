# BepuPhysics v2 指南

## 目录

1. [概述与安装](#1-概述与安装)
2. [Simulation 设置与 Timestep](#2-simulation-设置与-timestep)
3. [静态体、动态体和运动学体](#3-静态体动态体和运动学体)
4. [碰撞体](#4-碰撞体)
5. [约束](#5-约束)
6. [角色控制器](#6-角色控制器)
7. [射线检测与查询](#7-射线检测与查询)
8. [接触事件与碰撞过滤](#8-接触事件与碰撞过滤)
9. [与 ECS 集成](#9-与-ecs-集成)
10. [性能与最佳实践](#10-性能与最佳实践)

---

## 1. 概述与安装

### 什么是 BepuPhysics v2？

BepuPhysics v2 是一个**纯 C# 3D 实时物理模拟库**，专为在现代 CPU 上实现最大性能而设计。它是对原始 BEPUphysics v1 的完全重写，专注于速度、灵活性和底层控制。

### 核心特性

- **纯 C# 实现** - 无原生依赖
- **高性能** - 针对 CPU 进行 SIMD 优化
- **丰富的形状支持** - 球体、胶囊体、盒子、三角形、圆柱体、凸包、复合体、网格
- **丰富的约束系统** - 铰链、球窝关节、角电机等
- **连续碰撞检测** - 防止高速物体穿透
- **高效休眠** - 静止物体进入低开销的休眠状态
- **场景级查询** - 射线检测、扫描测试、AABB 查询
- **灵活的回调** - 完全控制碰撞过滤、材质和积分

### 系统要求

- **.NET 8+** - 目标为现代 .NET 以获得最佳性能
- **支持 SIMD 的编译器** - 推荐使用 RyuJIT 以优化 `System.Numerics.Vectors`
- **多核 CPU** - 支持多线程以提升性能

### 安装

#### NuGet 包

```xml
<ItemGroup>
  <PackageReference Include="BepuPhysics" Version="2.5.0-beta.28" />
  <PackageReference Include="BepuUtilities" Version="2.5.0-beta.28" />
</ItemGroup>
```

#### 从源码构建

```bash
git clone https://github.com/bepu/bepuphysics2.git
cd bepuphysics2
dotnet build Library.sln
```

### 运行示例

```bash
# Windows DirectX 11 版本
dotnet run --project Demos/Demos.csproj -c Release

# 跨平台 OpenGL 版本
dotnet run --project Demos.GL/Demos.csproj -c Release
```

---

## 2. Simulation 设置与 Timestep

### 基本模拟设置

BepuPhysics 需要几种回调类型才能运行。它们以结构体（而非类）实现，以便编译器进行优化和内联。

```csharp
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using System.Numerics;

public class PhysicsWorld : IDisposable
{
    private BufferPool _bufferPool;
    private Simulation _simulation;
    private SimpleThreadDispatcher _threadDispatcher;

    public PhysicsWorld()
    {
        // 创建用于所有物理分配的内存池
        _bufferPool = new BufferPool();

        // 创建带回调的模拟
        _simulation = Simulation.Create(
            _bufferPool,
            new NarrowPhaseCallbacks(),
            new PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0))
        );

        // 创建用于多线程的线程调度器
        _threadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);
    }

    public void Update(float deltaTime)
    {
        // 推进模拟
        _simulation.Timestep(deltaTime, _threadDispatcher);
    }

    public void Dispose()
    {
        _simulation?.Dispose();
        _threadDispatcher?.Dispose();
        _bufferPool?.Clear();
    }
}
```

### NarrowPhase 回调

处理碰撞检测、材质属性和过滤。

```csharp
using BepuPhysics.CollisionDetection;
using System.Runtime.CompilerServices;

public struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public void Initialize(Simulation simulation)
    {
        // 模拟就绪时调用
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
    {
        // 在此过滤碰撞
        // 返回 false 可阻止 a 和 b 之间的碰撞
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        // 用于复合形状
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool ConfigureContactManifold(
        int workerIndex,
        CollidablePair pair,
        ConvexContactManifold* manifold,
        out PairMaterialProperties pairMaterial)
    {
        ConfigureMaterial(out pairMaterial);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool ConfigureContactManifold(
        int workerIndex,
        CollidablePair pair,
        NonconvexContactManifold* manifold,
        out PairMaterialProperties pairMaterial)
    {
        ConfigureMaterial(out pairMaterial);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigureMaterial(out PairMaterialProperties pairMaterial)
    {
        pairMaterial.FrictionCoefficient = 0.5f;
        pairMaterial.MaximumRecoveryVelocity = 2f;
        pairMaterial.SpringSettings = new SpringSettings(30, 1);
    }

    public void Dispose()
    {
    }
}
```

### Pose Integrator 回调

处理逐刚体的积分，通常用于重力和阻尼。

```csharp
using BepuUtilities;
using System.Numerics;
using System.Runtime.CompilerServices;

public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public Vector3 Gravity;
    private Vector3 _gravityDt;
    private Vector3 _linearDampingDt;
    private Vector3 _angularDampingDt;

    public AngularIntegrationMode AngularIntegrationMode =>
        AngularIntegrationMode.Nonconserving;

    public PoseIntegratorCallbacks(Vector3 gravity) : this()
    {
        Gravity = gravity;
    }

    public void PrepareForIntegration(float dt)
    {
        _gravityDt = Gravity * dt;
        _linearDampingDt = new Vector3(MathF.Pow(0.99f, dt));
        _angularDampingDt = new Vector3(MathF.Pow(0.98f, dt));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IntegrateVelocity(
        int bodyIndex,
        in RigidPose pose,
        in BodyInertia localInertia,
        int workerIndex,
        ref BodyVelocity velocity)
    {
        // 仅对动态体施加重力
        if (localInertia.InverseMass > 0)
        {
            velocity.Linear += _gravityDt;
            velocity.Linear *= _linearDampingDt;
            velocity.Angular *= _angularDampingDt;
        }
    }
}
```

### Timestepping 策略

#### 固定 Timestep

```csharp
private float _accumulator;
private readonly float _fixedDt = 1f / 60f;

public void Update(float deltaTime)
{
    _accumulator += deltaTime;

    while (_accumulator >= _fixedDt)
    {
        _simulation.Timestep(_fixedDt, _threadDispatcher);
        _accumulator -= _fixedDt;
    }
}
```

#### 可变 Timestep（不推荐）

```csharp
// 避免 - 会导致不稳定
public void Update(float deltaTime)
{
    _simulation.Timestep(deltaTime, _threadDispatcher);
}
```

#### SolveDescription 配置

```csharp
var solveDescription = new SolveDescription(
    velocityIterations: 8,
    substeps: 2
);

_simulation = Simulation.Create(
    _bufferPool,
    new NarrowPhaseCallbacks(),
    new PoseIntegratorCallbacks(gravity),
    solveDescription
);
```

---

## 3. 静态体、动态体和运动学体

### 创建动态体

```csharp
// 创建球体形状
var sphere = new Sphere(0.5f);
sphere.ComputeInertia(1f, out var sphereInertia);

// 添加动态体
var bodyHandle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
    position: new Vector3(0, 10, 0),
    localInertia: sphereInertia,
    collidable: new CollidableDescription(
        _simulation.Shapes.Add(sphere),
        0.1f  // 碰撞边距
    ),
    activity: new BodyActivityDescription(0.01f)
));
```

### 创建静态体

```csharp
// 静态体不可移动 - 适合关卡几何体
var box = new Box(10, 1, 10);

_simulation.Statics.Add(new StaticDescription(
    position: new Vector3(0, -0.5f, 0),
    collidable: new CollidableDescription(
        _simulation.Shapes.Add(box),
        0.1f
    )
));
```

### 创建运动学体

```csharp
// 运动学体可移动但不受力的影响
var kinematicBody = BodyDescription.CreateKinematic(
    pose: new RigidPose(new Vector3(5, 5, 5)),
    collidable: new CollidableDescription(
        _simulation.Shapes.Add(new Box(2, 2, 2)),
        0.1f
    )
);

var handle = _simulation.Bodies.Add(kinematicBody);

// 手动更新运动学体的速度
var bodyReference = _simulation.Bodies[handle];
bodyReference.Velocity.Linear = new Vector3(1, 0, 0);
```

### 刚体类型对比

| 类型 | 受力影响 | 控制方式 | 使用场景 |
|------|----------|----------|----------|
| 静态体 | 否 | 不受控制 | 地面、墙壁、关卡几何体 |
| 动态体 | 是 | 力/冲量 | 玩家、敌人、碎片 |
| 运动学体 | 否 | 速度/位置 | 移动平台、电梯 |

### 访问刚体数据

```csharp
// 获取刚体引用
BodyReference body = _simulation.Bodies[bodyHandle];

// 读取属性
Vector3 position = body.Pose.Position;
Quaternion orientation = body.Pose.Orientation;
Vector3 linearVelocity = body.Velocity.Linear;
Vector3 angularVelocity = body.Velocity.Angular;

// 修改属性
body.Pose.Position = newPosition;
body.Velocity.Linear = newVelocity;

// 施加力（需要在姿态回调中进行积分）
// 力通常通过 PoseIntegratorCallbacks 施加
```

### 刚体活动性（休眠）

```csharp
// 配置休眠阈值
var activity = new BodyActivityDescription(
    sleepThreshold: 0.01f,
    minimumTimestepCount: 8
);

// 唤醒休眠的刚体
BodyReference body = _simulation.Bodies[handle];
body.Awake = true;

// 检查刚体是否处于唤醒状态
bool isAwake = body.Awake;
```

---

## 4. 碰撞体

### 基本形状

#### 球体

```csharp
var sphere = new Sphere(radius: 1f);
sphere.ComputeInertia(mass: 1f, out var inertia);
```

#### 盒子

```csharp
var box = new Box(width: 2, height: 1, length: 3);
box.ComputeInertia(mass: 1f, out var inertia);
```

#### 胶囊体

```csharp
var capsule = new Capsule(radius: 0.5f, length: 2f);
capsule.ComputeInertia(mass: 1f, out var inertia);
```

#### 圆柱体

```csharp
var cylinder = new Cylinder(radius: 0.5f, length: 2f);
cylinder.ComputeInertia(mass: 1f, out var inertia);
```

### 凸包

```csharp
// 从点集创建凸包
var points = new[]
{
    new Vector3(-1, -1, -1),
    new Vector3(1, -1, -1),
    new Vector3(1, 1, -1),
    new Vector3(-1, 1, -1),
    new Vector3(0, 0, 1)
};

var convexHull = new ConvexHull(points, _bufferPool, out var hull);
hull.ComputeInertia(1f, out var inertia);
```

### 复合形状

```csharp
// 从多个子形状创建复合体
var compoundBuilder = new CompoundBuilder(_bufferPool, 4);

// 添加子形状
compoundBuilder.Add(
    new Box(1, 1, 1),
    RigidPose.Identity,
    1f
);

compoundBuilder.Add(
    new Sphere(0.5f),
    new RigidPose(new Vector3(0, 1, 0)),
    0.5f
);

var compound = compoundBuilder.BuildCompound(out var childrenInertia, out var center);
var inertia = childrenInertia.ComputeInertia(center);

// 清理
compoundBuilder.Dispose();
```

### 网格碰撞体

```csharp
// 为静态几何体创建三角形网格
var mesh = new TriangleMesh();

// 添加三角形
mesh.Add(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));

// 构建网格
var meshIndex = _simulation.Shapes.Add(mesh);

// 用于静态体
_simulation.Statics.Add(new StaticDescription(
    Vector3.Zero,
    new CollidableDescription(meshIndex, 0.1f)
));
```

### 形状性能排序

从最快到最慢：
1. **球体** - 最简单的碰撞检测
2. **胶囊体** - 仍然非常快
3. **盒子** - 良好的性能
4. **三角形** - 较慢，用于网格
5. **圆柱体** - 更复杂
6. **凸包** - 开销最大，但最灵活

### 碰撞边距

```csharp
// 碰撞边距提高稳定性
var collidable = new CollidableDescription(
    shapeIndex,
    collisionMargin: 0.1f  // 0.1 单位边距
);

// 较小的边距 = 更精确但不太稳定
// 较大的边距 = 更稳定但不太精确
```

---

## 5. 约束

### 约束基础

约束控制刚体之间的相对运动。它们通过描述创建并添加到求解器中。

### 球窝约束

```csharp
// 在单一点连接两个刚体
var ballSocket = new BallSocket(
    localAnchorA: new Vector3(0, 1, 0),
    localAnchorB: new Vector3(0, -1, 0),
    bodyA: bodyHandleA,
    bodyB: bodyHandleB
);

_simulation.Solver.Add(ballSocket);
```

### 铰链约束

```csharp
// 允许绕单个轴旋转
var hinge = new Hinge(
    localHingeAxisA: Vector3.UnitX,
    localHingeAxisB: Vector3.UnitX,
    localAnchorA: Vector3.Zero,
    localAnchorB: Vector3.Zero,
    bodyA: bodyHandleA,
    bodyB: bodyHandleB,
    springSettings: new SpringSettings(30, 1)
);

_simulation.Solver.Add(hinge);
```

### 角伺服约束

```csharp
// 驱动旋转到目标角度
var servo = new AngularServo(
    localOffsetAxisA: Vector3.UnitX,
    localOffsetAxisB: Vector3.UnitX,
    targetRelativeRotation: Quaternion.Identity,
    bodyA: bodyHandleA,
    bodyB: bodyHandleB,
    servoSettings: new ServoSettings(
        maximumSpeed: 10f,
        baseForceSettings: new SpringSettings(100, 1)
    )
);

_simulation.Solver.Add(servo);
```

### 距离限制约束

```csharp
// 在限制范围内保持两个刚体之间的距离
var distanceLimit = new DistanceLimit(
    localAnchorA: Vector3.Zero,
    localAnchorB: Vector3.Zero,
    minimumDistance: 0.5f,
    maximumDistance: 2f,
    bodyA: bodyHandleA,
    bodyB: bodyHandleB,
    springSettings: new SpringSettings(30, 1)
);

_simulation.Solver.Add(distanceLimit);
```

### 中心距离约束

```csharp
// 保持两个刚体中心之间的固定距离
var centerDistance = new CenterDistanceConstraint(
    targetDistance: 3f,
    bodyA: bodyHandleA,
    bodyB: bodyHandleB,
    springSettings: new SpringSettings(50, 1)
);

_simulation.Solver.Add(centerDistance);
```

### 角电机约束

```csharp
// 施加扭矩以达到目标角速度
var angularMotor = new AngularMotor(
    localBasisA: Matrix3x3.Identity,
    localBasisB: Matrix3x3.Identity,
    targetRelativeAngularVelocity: new Vector3(0, 5f, 0), // 绕 Y 轴 5 rad/s
    bodyA: bodyHandleA,
    bodyB: bodyHandleB,
    motorSettings: new MotorSettings(
        maximumForce: 100f,
        baseSettings: new SpringSettings(50, 1)
    )
);

_simulation.Solver.Add(angularMotor);
```

### 焊接约束

```csharp
// 完全锁定两个刚体之间的相对运动
var weld = new Weld(
    localOffsetA: new Vector3(0, 1, 0),
    localOffsetB: new Vector3(0, -1, 0),
    localOrientationA: Quaternion.Identity,
    localOrientationB: Quaternion.Identity,
    bodyA: bodyHandleA,
    bodyB: bodyHandleB,
    springSettings: new SpringSettings(30, 1),
    maximumForce: float.MaxValue
);

_simulation.Solver.Add(weld);
```

### 访问和修改约束

```csharp
// 获取约束句柄
var constraintHandle = _simulation.Solver.Add(ballSocket);

// 访问约束数据
var description = new BallSocketDescription();
_simulation.Solver.GetDescription(constraintHandle, ref description);

// 修改并应用
description.LocalAnchorA = new Vector3(0, 0.5f, 0);
_simulation.Solver.ApplyDescription(constraintHandle, ref description);

// 移除约束
_simulation.Solver.Remove(constraintHandle);
```

---

## 6. 角色控制器

### 简单角色控制器

BepuPhysics 不包含内置的角色控制器，但你可以使用约束和运动学体来创建一个。

```csharp
public class CharacterController
{
    private BodyHandle _bodyHandle;
    private Simulation _simulation;
    private float _moveSpeed = 5f;
    private float _jumpForce = 8f;

    public CharacterController(Simulation simulation, Vector3 position)
    {
        _simulation = simulation;

        // 为角色创建胶囊体
        var capsule = new Capsule(0.4f, 1.8f);
        capsule.ComputeInertia(70f, out var inertia);

        _bodyHandle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
            position,
            inertia,
            new CollidableDescription(_simulation.Shapes.Add(capsule), 0.1f),
            new BodyActivityDescription(0.01f)
        ));

        // 锁定旋转（胶囊体保持直立）
        var body = _simulation.Bodies[_bodyHandle];
        body.LocalInertia.InverseInertiaTensor = new Matrix3x3();
    }

    public void Move(Vector3 direction, float deltaTime, bool jump)
    {
        var body = _simulation.Bodies[_bodyHandle];

        // 施加移动力
        if (direction != Vector3.Zero)
        {
            direction = Vector3.Normalize(direction);
            body.Velocity.Linear = new Vector3(
                direction.X * _moveSpeed,
                body.Velocity.Linear.Y,
                direction.Z * _moveSpeed
            );
        }

        // 跳跃
        if (jump && IsGrounded())
        {
            body.Velocity.Linear = new Vector3(
                body.Velocity.Linear.X,
                _jumpForce,
                body.Velocity.Linear.Z
            );
        }
    }

    public bool IsGrounded()
    {
        // 检查角色是否在地面
        // 实现取决于你的碰撞检测设置
        return true; // 简化实现
    }

    public Vector3 Position
    {
        get => _simulation.Bodies[_bodyHandle].Pose.Position;
        set => _simulation.Bodies[_bodyHandle].Pose.Position = value;
    }
}
```

### 带碰撞检测的高级角色控制器

```csharp
public class AdvancedCharacterController
{
    private BodyHandle _bodyHandle;
    private Simulation _simulation;
    private float _maxSlope = 0.7f; // ~45 度

    public Vector3 Position => _simulation.Bodies[_bodyHandle].Pose.Position;

    public AdvancedCharacterController(Simulation simulation, Vector3 position)
    {
        _simulation = simulation;

        var capsule = new Capsule(0.4f, 1.8f);
        capsule.ComputeInertia(70f, out var inertia);

        _bodyHandle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
            position,
            inertia,
            new CollidableDescription(_simulation.Shapes.Add(capsule), 0.1f),
            new BodyActivityDescription(0.01f)
        ));
    }

    public void Move(Vector3 direction, float deltaTime, bool jump, bool sprint)
    {
        var body = _simulation.Bodies[_bodyHandle];
        float speed = sprint ? 8f : 5f;

        if (direction != Vector3.Zero)
        {
            direction = Vector3.Normalize(direction);
            Vector3 targetVelocity = direction * speed;

            // 平滑插值速度
            body.Velocity.Linear = Vector3.Lerp(
                body.Velocity.Linear,
                new Vector3(targetVelocity.X, body.Velocity.Linear.Y, targetVelocity.Z),
                10f * deltaTime
            );
        }

        if (jump && IsGrounded())
        {
            body.Velocity.Linear = new Vector3(
                body.Velocity.Linear.X,
                8f,
                body.Velocity.Linear.Z
            );
        }
    }

    private bool IsGrounded()
    {
        var body = _simulation.Bodies[_bodyHandle];
        var ray = new Ray(body.Pose.Position, Vector3.UnitY * -1);

        bool grounded = false;
        var handler = new CustomRayHitHandler
        {
            OnHit = (hit) =>
            {
                if (hit.T <= 1.0f) // 在胶囊体高度范围内
                {
                    grounded = true;
                    return true; // 继续检测
                }
                return false;
            }
        };

        _simulation.RayCast(ray, 1.5f, ref handler);
        return grounded;
    }

    private struct CustomRayHitHandler : IRayHitHandler
    {
        public Func<RayHit, bool> OnHit;

        public bool AllowTest(CollidableReference collidable)
        {
            return true;
        }

        public bool AllowTest(CollidableReference collidable, int childIndex)
        {
            return true;
        }
    }
}
```

---

## 7. 射线检测与查询

### 射线检测

```csharp
// 简单射线检测
var ray = new Ray(origin: new Vector3(0, 10, 0), direction: Vector3.UnitY * -1);
float maxT = 20f;

var hitHandler = new SimpleRayHitHandler();
_simulation.RayCast(ray, maxT, ref hitHandler);

public struct SimpleRayHitHandler : IRayHitHandler
{
    public float SmallestT { get; private set; }
    public RayHit Hit { get; private set; }

    public SimpleRayHitHandler()
    {
        SmallestT = float.MaxValue;
    }

    public bool AllowTest(CollidableReference collidable)
    {
        return true; // 测试所有碰撞体
    }

    public bool AllowTest(CollidableReference collidable, int childIndex)
    {
        return true;
    }

    public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, CollidableReference collidable, int childIndex)
    {
        if (t < SmallestT)
        {
            SmallestT = t;
            Hit = new RayHit(t, normal, collidable);
            maximumT = t; // 在首次命中时停止
        }
    }
}
```

### 扫描测试（连续碰撞检测）

```csharp
// 在场景中扫描形状
var sphere = new Sphere(0.5f);
var startPose = new RigidPose(new Vector3(0, 10, 0));
var direction = new Vector3(0, -1, 0);
float distance = 15f;

var sweepHandler = new SimpleSweepHitHandler();
_simulation.Sweep(sphere, startPose, direction, distance, 0, ref sweepHandler);

public struct SimpleSweepHitHandler : ISweepHitHandler
{
    public bool HitFound { get; private set; }
    public float T { get; private set; }
    public Vector3 Normal { get; private set; }

    public SimpleSweepHitHandler()
    {
        HitFound = false;
    }

    public bool AllowTest(CollidableReference collidable)
    {
        return true;
    }

    public bool AllowTest(CollidableReference collidable, int childIndex)
    {
        return true;
    }

    public void OnHit(ref float maximumT, float t, Vector3 normal, CollidableReference collidable, int childIndex)
    {
        HitFound = true;
        T = t;
        Normal = normal;
        maximumT = t;
    }
}
```

### 点查询

```csharp
// 查找特定点处的碰撞体
var point = new Vector3(0, 0, 0);

var pointQueryHandler = new PointQueryHandler();
_simulation.BroadPhase.Query(point, ref pointQueryHandler);

public struct PointQueryHandler : IPointQueryHandler
{
    public List<CollidableReference> FoundCollidables;

    public PointQueryHandler()
    {
        FoundCollidables = new List<CollidableReference>();
    }

    public bool Test(CollidableReference collidable)
    {
        FoundCollidables.Add(collidable);
        return true; // 继续检测
    }
}
```

### AABB 查询

```csharp
// 查找包围盒内的碰撞体
var min = new Vector3(-10, 0, -10);
var max = new Vector3(10, 5, 10);

var aabbHandler = new AABBQueryHandler();
_simulation.BroadPhase.Query(new BoundingBox(min, max), ref aabbHandler);

public struct AABBQueryHandler : IAABBQueryHandler
{
    public List<CollidableReference> FoundCollidables;

    public AABBQueryHandler()
    {
        FoundCollidables = new List<CollidableReference>();
    }

    public bool Test(CollidableReference collidable)
    {
        FoundCollidables.Add(collidable);
        return true;
    }
}
```

---

## 8. 接触事件与碰撞过滤

### 使用分类进行碰撞过滤

```csharp
public struct CollisionCategories
{
    public const uint Default = 1 << 0;
    public const uint Player = 1 << 1;
    public const uint Enemy = 1 << 2;
    public const uint Ground = 1 << 3;
    public const uint Trigger = 1 << 4;
}

public struct CollisionFilter
{
    public uint Category;
    public uint Mask;

    public bool ShouldCollide(uint otherCategory, uint otherMask)
    {
        return (Category & otherMask) != 0 && (otherCategory & Mask) != 0;
    }
}

// 在碰撞体的用户数据中存储碰撞过滤器
public struct CollidableUserData
{
    public CollisionFilter Filter;
    public string Tag;
}
```

### 接触事件

```csharp
public struct ContactEventCallbacks : INarrowPhaseCallbacks
{
    private List<ContactEvent> _contactEvents;

    public List<ContactEvent> ContactEvents => _contactEvents;

    public ContactEventCallbacks()
    {
        _contactEvents = new List<ContactEvent>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool ConfigureContactManifold(
        int workerIndex,
        CollidablePair pair,
        ConvexContactManifold* manifold,
        out PairMaterialProperties pairMaterial)
    {
        ConfigureMaterial(out pairMaterial);

        // 收集接触事件
        for (int i = 0; i < manifold->ContactCount; i++)
        {
            var contact = manifold->Contact0;
            _contactEvents.Add(new ContactEvent
            {
                CollidableA = pair.A,
                CollidableB = pair.B,
                ContactPoint = manifold->Contact0,
                Normal = manifold->Normal
            });
        }

        return true;
    }

    // ... 其他必需方法

    private void ConfigureMaterial(out PairMaterialProperties pairMaterial)
    {
        pairMaterial.FrictionCoefficient = 0.5f;
        pairMaterial.MaximumRecoveryVelocity = 2f;
        pairMaterial.SpringSettings = new SpringSettings(30, 1);
    }

    public void Dispose()
    {
    }
}

public struct ContactEvent
{
    public CollidableReference CollidableA;
    public CollidableReference CollidableB;
    public Vector3 ContactPoint;
    public Vector3 Normal;
}
```

### 碰撞过滤实现

```csharp
public struct FilteredNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
    {
        // 获取两个碰撞体的用户数据
        var userDataA = GetUserData(a);
        var userDataB = GetUserData(b);

        // 应用碰撞过滤
        if (userDataA != null && userDataB != null)
        {
            if (!userDataA.Filter.ShouldCollide(userDataB.Category, userDataB.Mask))
                return false;
        }

        return true;
    }

    private CollidableUserData GetUserData(CollidableReference collidable)
    {
        // 实现取决于你如何存储用户数据
        return null;
    }

    // ... 其他必需方法
}
```

### 触发器检测

```csharp
// 使用无物理响应的运动学体作为触发器
public struct TriggerCallbacks : INarrowPhaseCallbacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool ConfigureContactManifold(
        int workerIndex,
        CollidablePair pair,
        ConvexContactManifold* manifold,
        out PairMaterialProperties pairMaterial)
    {
        // 检查任一碰撞体是否为触发器
        if (IsTrigger(pair.A) || IsTrigger(pair.B))
        {
            // 不创建物理约束
            pairMaterial = default;
            return false;
        }

        ConfigureMaterial(out pairMaterial);
        return true;
    }

    private bool IsTrigger(CollidableReference collidable)
    {
        // 检查用户数据或自定义属性
        return false;
    }

    // ... 其他方法
}
```

---

## 9. 与 ECS 集成

### 物理相关 ECS 组件

```csharp
// 变换组件（与渲染共享）
public struct TransformComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}

// 物理刚体组件
public struct PhysicsBodyComponent
{
    public BodyHandle BodyHandle;
    public bool IsKinematic;
}

// 物理形状组件
public struct PhysicsShapeComponent
{
    public TypedIndex ShapeIndex;
    public float Mass;
    public float CollisionMargin;
}

// 物理速度组件（用于手动控制）
public struct PhysicsVelocityComponent
{
    public Vector3 Linear;
    public Vector3 Angular;
}

// 物理触发器组件
public struct PhysicsTriggerComponent
{
    public bool IsTrigger;
    public List<BodyHandle> OverlappingBodies;
}
```

### 物理同步系统

```csharp
public class PhysicsSyncSystem : IKiloSystem
{
    private Simulation _simulation;

    public PhysicsSyncSystem(Simulation simulation)
    {
        _simulation = simulation;
    }

    public void Update(KiloWorld world)
    {
        // 将变换同步到物理刚体
        foreach (ref var entity in world.Query<TransformComponent, PhysicsBodyComponent>()
            .With<PhysicsBodyComponent, PhysicsVelocityComponent>()
            .Build())
        {
            ref var transform = ref entity.Get<TransformComponent>();
            ref var body = ref entity.Get<PhysicsBodyComponent>();
            ref var velocity = ref entity.Get<PhysicsVelocityComponent>();

            var bodyRef = _simulation.Bodies[body.BodyHandle];
            bodyRef.Pose.Position = transform.Position;
            bodyRef.Pose.Orientation = transform.Rotation;
            bodyRef.Velocity.Linear = velocity.Linear;
            bodyRef.Velocity.Angular = velocity.Angular;
        }

        // 将物理刚体同步到变换
        foreach (ref var entity in world.Query<TransformComponent, PhysicsBodyComponent>()
            .Without<PhysicsVelocityComponent>()
            .Build())
        {
            ref var transform = ref entity.Get<TransformComponent>();
            ref var body = ref entity.Get<PhysicsBodyComponent>();

            var bodyRef = _simulation.Bodies[body.BodyHandle];
            transform.Position = bodyRef.Pose.Position;
            transform.Rotation = bodyRef.Pose.Orientation;
        }
    }
}
```

### 物理世界插件

```csharp
public class PhysicsPlugin : IKiloPlugin
{
    private PhysicsWorld _physicsWorld;
    private PhysicsSyncSystem _syncSystem;

    public void Load(KiloApp app)
    {
        _physicsWorld = new PhysicsWorld();
        _syncSystem = new PhysicsSyncSystem(_physicsWorld.Simulation);

        app.AddSystem<PhysicsSyncSystem>(_syncSystem);

        // 注册物理世界以供其他系统访问
        app.AddResource(_physicsWorld);
    }

    public void Update(KiloApp app)
    {
        _physicsWorld.Update(1f / 60f);
    }

    public void Unload(KiloApp app)
    {
        _physicsWorld.Dispose();
    }
}
```

### 实体创建辅助工具

```csharp
public static class PhysicsEntityFactory
{
    public static KiloEntity CreatePhysicsBody(
        KiloWorld world,
        Vector3 position,
        IShape shape,
        float mass,
        bool isStatic = false)
    {
        var entity = world.CreateEntity();

        // 添加变换
        world.AddComponent(entity, new TransformComponent
        {
            Position = position,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        // 添加物理组件
        world.AddComponent(entity, new PhysicsShapeComponent
        {
            ShapeIndex = world.GetResource<Simulation>().Shapes.Add(shape),
            Mass = mass,
            CollisionMargin = 0.1f
        });

        // 在模拟中创建刚体
        var simulation = world.GetResource<Simulation>();
        shape.ComputeInertia(mass, out var inertia);

        BodyHandle bodyHandle;
        if (isStatic)
        {
            bodyHandle = simulation.Statics.Add(new StaticDescription(
                position,
                new CollidableDescription(world.GetComponent<PhysicsShapeComponent>(entity).ShapeIndex, 0.1f)
            ));
        }
        else
        {
            bodyHandle = simulation.Bodies.Add(BodyDescription.CreateDynamic(
                position,
                inertia,
                new CollidableDescription(world.GetComponent<PhysicsShapeComponent>(entity).ShapeIndex, 0.1f),
                new BodyActivityDescription(0.01f)
            ));
        }

        world.AddComponent(entity, new PhysicsBodyComponent
        {
            BodyHandle = bodyHandle,
            IsKinematic = false
        });

        return entity;
    }
}
```

---

## 10. 性能与最佳实践

### 1. 使用合适的形状

```csharp
// 好：尽可能使用简单形状
var sphere = new Sphere(1f);           // 最快
var capsule = new Capsule(0.5f, 2f);   // 非常快
var box = new Box(1, 1, 1);            // 快

// 避免：对简单物体使用过于复杂的凸包
var complexHull = new ConvexHull(thousandsOfPoints, bufferPool, out var hull);
```

### 2. 对关卡几何体使用静态体

```csharp
// 好：关卡几何体使用静态体
foreach (var meshPart in levelMeshes)
{
    var mesh = new TriangleMesh();
    // 添加三角形...
    simulation.Statics.Add(new StaticDescription(
        position,
        new CollidableDescription(simulation.Shapes.Add(mesh), 0.1f)
    ));
}

// 差：关卡几何体使用动态体
simulation.Bodies.Add(BodyDescription.CreateDynamic(...)); // 浪费计算资源
```

### 3. 启用多线程

```csharp
// 根据 CPU 核心数创建线程调度器
var threadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);

// 在 Timestep 中使用
simulation.Timestep(deltaTime, threadDispatcher);
```

### 4. 适当配置求解器迭代次数

```csharp
// 在精度和性能之间平衡
var solveDescription = new SolveDescription(
    velocityIterations: 8,    // 越多越精确，但越慢
    substeps: 2               // 越多子步骤越稳定，但越慢
);
```

### 5. 对不活跃物体使用休眠

```csharp
// 配置休眠阈值
var activity = new BodyActivityDescription(
    sleepThreshold: 0.01f,      // 越低越早休眠
    minimumTimestepCount: 8     // 休眠前的非活跃帧数
);

// 需要时手动唤醒刚体
bodyReference.Awake = true;
```

### 6. 批量创建形状

```csharp
// 好：批量创建形状，然后创建刚体
var shapes = new List<TypedIndex>();
for (int i = 0; i < 100; i++)
{
    shapes.Add(simulation.Shapes.Add(new Box(1, 1, 1)));
}

// 然后创建刚体
for (int i = 0; i < 100; i++)
{
    simulation.Bodies.Add(BodyDescription.CreateDynamic(
        positions[i],
        inertias[i],
        new CollidableDescription(shapes[i], 0.1f),
        new BodyActivityDescription(0.01f)
    ));
}
```

### 7. 复用缓冲区

```csharp
// 为多个模拟复用缓冲区池
var bufferPool = new BufferPool();

var simulation1 = Simulation.Create(bufferPool, ...);
var simulation2 = Simulation.Create(bufferPool, ...);

// 完成后清空池
simulation1.Dispose();
simulation2.Dispose();
bufferPool.Clear();
```

### 8. 尽量减少约束复杂度

```csharp
// 好：尽可能使用简单约束
var ballSocket = new BallSocket(...);  // 简单，快速

// 避免：过度约束的系统
var weld = new Weld(...);  // 开销大，谨慎使用

// 对于刚性连接，考虑合并刚体
```

### 9. 优化 Broad Phase 查询

```csharp
// 好：使用特定查询
simulation.RayCast(ray, maxT, ref handler);          // 精确
simulation.BroadPhase.Query(aabb, ref handler);      // 快速但不精确

// 避免：不必要地查询整个场景
```

### 10. 性能分析与监控

```csharp
// 监控模拟性能
using var sw = System.Diagnostics.Stopwatch.StartNew();
simulation.Timestep(deltaTime, threadDispatcher);
sw.Stop();

if (sw.ElapsedMilliseconds > 16) // >60fps 阈值
{
    // 性能警告 - 减少复杂度
}
```

### 11. 内存管理

```csharp
// 始终释放模拟以将缓冲区归还给池
simulation.Dispose();

// 不再需要时清空缓冲区池
bufferPool.Clear();

// 对于长时间运行的应用程序，考虑定期压缩
if (frameCount % 1000 == 0)
{
    simulation.Compact();
}
```

### 12. 避免频繁添加/移除刚体

```csharp
// 好：池化和复用刚体
private Queue<BodyHandle> _bodyPool = new Queue<BodyHandle>();

public BodyHandle GetPooledBody()
{
    if (_bodyPool.Count > 0)
    {
        return _bodyPool.Dequeue();
    }
    return simulation.Bodies.Add(BodyDescription.CreateDynamic(...));
}

public void ReturnBodyToPool(BodyHandle handle)
{
    // 重置刚体状态
    var body = simulation.Bodies[handle];
    body.Pose = new RigidPose(default, Quaternion.Identity);
    body.Velocity = default;

    _bodyPool.Enqueue(handle);
}

// 差：每帧创建和销毁刚体
```

### 13. 合理使用碰撞边距

```csharp
// 较大的边距 = 更稳定但不太精确
var collidable = new CollidableDescription(
    shapeIndex,
    collisionMargin: 0.05f  // 从 0.05-0.1 开始
);

// 根据需求调整
// 高速物体：较大的边距 (0.1-0.2)
// 精确场景：较小的边距 (0.01-0.05)
```

### 14. 批量更新操作

```csharp
// 好：批量修改速度
foreach (var body in bodiesToMove)
{
    body.Velocity.Linear = targetVelocity;
}

// 差：每次修改之间更新模拟
foreach (var body in bodiesToMove)
{
    body.Velocity.Linear = targetVelocity;
    simulation.Timestep(dt);  // 不要这样做！
}
```

---

## 结论

BepuPhysics v2 是一个功能强大、高性能的 .NET 物理引擎。其优势包括：

1. **纯 C# 实现** - 无原生依赖，跨平台
2. **高性能** - SIMD 优化、多线程支持
3. **灵活的架构** - 丰富的回调系统用于自定义
4. **丰富的功能集** - 多种形状、约束和查询类型

在与 Kilo 等 ECS 系统集成时：
- 将物理状态与 ECS 组件分离
- 使用同步系统桥接物理和 ECS
- 池化和复用物理刚体以提升性能
- 实现正确的释放和清理

更多信息：
- [BepuPhysics GitHub 仓库](https://github.com/bepu/bepuphysics2)
- [BepuPhysics 文档](https://github.com/bepu/bepuphysics2/blob/master/Documentation/)
- [BepuPhysics 示例](https://github.com/bepu/bepuphysics2/tree/master/Demos)
- [BepuPhysics 问答](https://github.com/bepu/bepuphysics2/blob/master/Documentation/QuestionsAndAnswers.md)
