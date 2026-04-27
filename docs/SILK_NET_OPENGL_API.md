# Silk.NET OpenGL API 参考手册（源码级）

> 本文档基于 Silk.NET 官方源码（`Silk.NET.OpenGL/GL.gen.cs`、`GL.cs`、`GLOverloads.gen.cs` 及官方示例）整理，面向 `Kilo.Rendering` 及底层渲染系统开发，提供可直接落地的 API 签名、调用模式与可复用抽象。

---

## 目录

1. [GL 上下文获取](#1-gl-上下文获取)
2. [缓冲区对象（Buffer）](#2-缓冲区对象buffer)
3. [顶点数组对象（VAO）](#3-顶点数组对象vao)
4. [着色器与程序（Shader / Program）](#4-着色器与程序shader--program)
5. [纹理（Texture）](#5-纹理texture)
6. [帧缓冲对象（Framebuffer）](#6-帧缓冲对象framebuffer)
7. [绘制命令（Draw Calls）](#7-绘制命令draw-calls)
8. [管线状态管理](#8-管线状态管理)
9. [Uniform 与矩阵传递](#9-uniform-与矩阵传递)
10. [官方示例抽象复用](#10-官方示例抽象复用)
11. [与 Kilo.Rendering 的集成建议](#11-与-kilorendering-的集成建议)
12. [常见陷阱与排查](#12-常见陷阱与排查)

---

## 1. GL 上下文获取

Silk.NET 的 OpenGL 入口是 `Silk.NET.OpenGL.GL` 类，它继承自 `NativeAPI`。

### 1.1 工厂方法（`GL.cs`）

```csharp
// 从窗口上下文获取（最常用）
public static GL GetApi(IGLContextSource contextSource)

// 直接传入上下文
public static GL GetApi(IGLContext ctx)
public static GL GetApi(INativeContext ctx)
public static GL GetApi(Func<string, nint> getProcAddress)

// 扩展方法
public static GL CreateOpenGL(this IGLContextSource src) => GL.GetApi(src);
```

**标准初始化流程（配合 Silk.NET.Windowing）**

```csharp
using Silk.NET.Windowing;
using Silk.NET.OpenGL;

IWindow window = Window.Create(WindowOptions.Default);
GL gl = GL.GetApi(window);   // 或 window.CreateOpenGL();
```

### 1.2 扩展探测

```csharp
public bool TryGetExtension<T>(out T ext) where T : NativeExtension<GL>
public override bool IsExtensionPresent(string extension)
```

---

## 2. 缓冲区对象（Buffer）

### 2.1 核心 API 签名（`GL.gen.cs`）

```csharp
// 生成 / 删除
public partial void GenBuffers(uint n, out uint buffers);
public partial void DeleteBuffers(uint n, ref readonly uint buffers);

// 绑定
public partial void BindBuffer(BufferTargetARB target, uint buffer);

// 数据上传
public partial void BufferData<T0>(
    BufferTargetARB target,
    nuint size,
    ref readonly T0 data,
    BufferUsageARB usage) where T0 : unmanaged;

// 子数据更新
public partial void BufferSubData<T0>(
    BufferTargetARB target,
    nint offset,
    nuint size,
    ref readonly T0 data) where T0 : unmanaged;
```

### 2.2 常用枚举

| 枚举 | 用途 |
|------|------|
| `BufferTargetARB.ArrayBuffer` | 顶点数据（VBO） |
| `BufferTargetARB.ElementArrayBuffer` | 索引数据（EBO/IBO） |
| `BufferTargetARB.UniformBuffer` | UBO |
| `BufferTargetARB.ShaderStorageBuffer` | SSBO |
| `BufferUsageARB.StaticDraw` | 一次性上传，多次绘制 |
| `BufferUsageARB.DynamicDraw` | 频繁修改 |
| `BufferUsageARB.StreamDraw` | 每帧修改 |

### 2.3 使用示例

```csharp
// VBO
float[] vertices = { /* ... */ };
uint vbo = gl.GenBuffer();
gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
unsafe
{
    fixed (void* p = vertices)
        gl.BufferData(BufferTargetARB.ArrayBuffer,
                      (nuint)(vertices.Length * sizeof(float)),
                      p,
                      BufferUsageARB.StaticDraw);
}

// EBO
uint[] indices = { 0, 1, 3, 1, 2, 3 };
uint ebo = gl.GenBuffer();
gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
unsafe
{
    fixed (void* p = indices)
        gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                      (nuint)(indices.Length * sizeof(uint)),
                      p,
                      BufferUsageARB.StaticDraw);
}
```

---

## 3. 顶点数组对象（VAO）

### 3.1 核心 API 签名

```csharp
public partial void GenVertexArrays(uint n, out uint arrays);
public partial void DeleteVertexArrays(uint n, ref readonly uint arrays);
public partial void BindVertexArray(uint array);

public partial void VertexAttribPointer(
    uint index,               // layout(location = index)
    int size,                 // 分量数（1~4）
    VertexAttribPointerType type,
    bool normalized,
    uint stride,              // 字节跨度
    nint pointer);            // 偏移（字节）

public partial void EnableVertexAttribArray(uint index);
public partial void DisableVertexAttribArray(uint index);
```

### 3.2 使用示例

```csharp
uint vao = gl.GenVertexArray();
gl.BindVertexArray(vao);
gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);

// layout(location = 0) in vec3 aPosition;
// 假设顶点结构为：vec3 position + vec2 uv，stride = 5 * sizeof(float)
gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false,
                       5 * sizeof(float), 0);
gl.EnableVertexAttribArray(0);

// layout(location = 1) in vec2 aUv;
gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false,
                       5 * sizeof(float), 3 * sizeof(float));
gl.EnableVertexAttribArray(1);
```

---

## 4. 着色器与程序（Shader / Program）

### 4.1 核心 API 签名

```csharp
// Shader 对象
public partial uint CreateShader(ShaderType type);
public partial void ShaderSource(uint shader, string @string);
public partial void CompileShader(uint shader);
public partial void GetShader(uint shader, GLEnum pname, out int @params);
public partial string GetShaderInfoLog(uint shader);
public partial void DeleteShader(uint shader);

// Program 对象
public partial uint CreateProgram();
public partial void AttachShader(uint program, uint shader);
public partial void LinkProgram(uint program);
public partial void GetProgram(uint program, GLEnum pname, out int @params);
public partial string GetProgramInfoLog(uint program);
public partial void UseProgram(uint program);
public partial void DetachShader(uint program, uint shader);
public partial void DeleteProgram(uint program);

// Uniform 位置查询
public partial int GetUniformLocation(uint program, string name);
```

### 4.2 常用枚举

| 枚举 | 说明 |
|------|------|
| `ShaderType.VertexShader` | 顶点着色器 |
| `ShaderType.FragmentShader` | 片段着色器 |
| `ShaderType.GeometryShader` | 几何着色器 |
| `GLEnum.CompileStatus` | 编译状态查询 |
| `GLEnum.LinkStatus` | 链接状态查询 |

### 4.3 完整编译-链接流程

```csharp
uint CompileShader(GL gl, ShaderType type, string source)
{
    uint shader = gl.CreateShader(type);
    gl.ShaderSource(shader, source);
    gl.CompileShader(shader);

    gl.GetShader(shader, GLEnum.CompileStatus, out int status);
    if (status == 0)
        throw new InvalidOperationException(gl.GetShaderInfoLog(shader));

    return shader;
}

uint LinkProgram(GL gl, uint vs, uint fs)
{
    uint program = gl.CreateProgram();
    gl.AttachShader(program, vs);
    gl.AttachShader(program, fs);
    gl.LinkProgram(program);

    gl.GetProgram(program, GLEnum.LinkStatus, out int status);
    if (status == 0)
        throw new InvalidOperationException(gl.GetProgramInfoLog(program));

    gl.DetachShader(program, vs);
    gl.DetachShader(program, fs);
    gl.DeleteShader(vs);
    gl.DeleteShader(fs);

    return program;
}
```

---

## 5. 纹理（Texture）

### 5.1 核心 API 签名

```csharp
public partial void GenTextures(uint n, out uint textures);
public partial void DeleteTextures(uint n, ref readonly uint textures);
public partial void BindTexture(TextureTarget target, uint texture);
public partial void ActiveTexture(TextureUnit texture);

public partial void TexImage2D<T0>(
    TextureTarget target,
    int level,
    InternalFormat internalformat,
    uint width,
    uint height,
    int border,
    PixelFormat format,
    PixelType type,
    ref readonly T0 pixels) where T0 : unmanaged;

public partial void TexParameter(TextureTarget target, TextureParameterName pname, int param);
public partial void GenerateMipmap(TextureTarget target);
```

### 5.2 常用枚举

| 枚举 | 说明 |
|------|------|
| `TextureTarget.Texture2D` | 2D 纹理 |
| `InternalFormat.Rgba` / `Rgb` / `Rgba8` | 内部格式 |
| `PixelFormat.Rgba` / `Rgb` | 像素布局 |
| `PixelType.UnsignedByte` | 像素数据类型 |
| `TextureParameterName.TextureWrapS/T` | 环绕方式 |
| `TextureParameterName.TextureMinFilter` | 缩小过滤 |
| `TextureParameterName.TextureMagFilter` | 放大过滤 |
| `TextureUnit.Texture0~Texture31` | 纹理单元 |

### 5.3 完整 2D 纹理上传示例

```csharp
using StbImageSharp; // 需要 Silk.NET 生态常用的 StbImageSharp

uint tex = gl.GenTexture();
gl.BindTexture(TextureTarget.Texture2D, tex);

ImageResult image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
unsafe
{
    fixed (byte* ptr = image.Data)
    {
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                      (uint)image.Width, (uint)image.Height,
                      0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
    }
}

gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
gl.GenerateMipmap(TextureTarget.Texture2D);
```

### 5.4 纹理绑定到指定槽

```csharp
gl.ActiveTexture(TextureUnit.Texture0);
gl.BindTexture(TextureTarget.Texture2D, tex);
// 在 shader 中设置 sampler uniform = 0
shader.SetUniform("uTexture0", 0);
```

---

## 6. 帧缓冲对象（Framebuffer）

### 6.1 核心 API 签名

```csharp
public partial void GenFramebuffers(uint n, out uint framebuffers);
public partial void DeleteFramebuffers(uint n, ref readonly uint framebuffers);
public partial void BindFramebuffer(FramebufferTarget target, uint framebuffer);

public partial void FramebufferTexture2D(
    FramebufferTarget target,
    FramebufferAttachment attachment,
    TextureTarget textarget,
    uint texture,
    int level);

public partial GLEnum CheckFramebufferStatus(FramebufferTarget target);
```

### 6.2 常用枚举

| 枚举 | 说明 |
|------|------|
| `FramebufferTarget.Framebuffer` | 读写帧缓冲 |
| `FramebufferTarget.DrawFramebuffer` | 仅写 |
| `FramebufferTarget.ReadFramebuffer` | 仅读 |
| `FramebufferAttachment.ColorAttachment0` | 颜色附件 0 |
| `FramebufferAttachment.DepthAttachment` | 深度附件 |
| `FramebufferAttachment.DepthStencilAttachment` | 深度+模板附件 |
| `GLEnum.FramebufferComplete` | 状态完整 |

### 6.3 离屏渲染目标（FBO）示例

```csharp
// 创建 FBO
uint fbo = gl.GenFramebuffer();
gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

// 附加颜色纹理
uint colorTex = gl.GenTexture();
gl.BindTexture(TextureTarget.Texture2D, colorTex);
gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
    width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref readonly default(byte));
gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
    TextureTarget.Texture2D, colorTex, 0);

// 检查完整性
var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
if (status != GLEnum.FramebufferComplete)
    throw new InvalidOperationException($"Framebuffer incomplete: {status}");

// 解绑
gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
```

---

## 7. 绘制命令（Draw Calls）

### 7.1 核心 API 签名

```csharp
// 顶点数组绘制
public partial void DrawArrays(PrimitiveType mode, int first, uint count);
public partial void DrawArraysInstanced(PrimitiveType mode, int first, uint count, uint instancecount);

// 索引绘制
public partial void DrawElements<T0>(
    PrimitiveType mode,
    uint count,
    DrawElementsType type,
    ref readonly T0 indices) where T0 : unmanaged;

public partial void DrawElementsInstanced<T0>(
    PrimitiveType mode,
    uint count,
    DrawElementsType type,
    ref readonly T0 indices,
    uint instancecount) where T0 : unmanaged;
```

### 7.2 常用枚举

| 枚举 | 说明 |
|------|------|
| `PrimitiveType.Triangles` | 三角形列表 |
| `PrimitiveType.TriangleStrip` | 三角形条带 |
| `PrimitiveType.Lines` | 线段 |
| `PrimitiveType.Points` | 点 |
| `DrawElementsType.UnsignedInt` | `uint` 索引 |
| `DrawElementsType.UnsignedShort` | `ushort` 索引 |

### 7.3 调用示例

```csharp
// 使用 VAO + EBO 时，indices 参数传 null（因为数据已在 EBO 中绑定）
gl.BindVertexArray(vao);
gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Length,
    DrawElementsType.UnsignedInt, null);

// 纯顶点数组绘制（无索引）
gl.DrawArrays(PrimitiveType.Triangles, 0, 36);

// Instanced
uint instanceCount = 100;
gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 36, instanceCount);
```

---

## 8. 管线状态管理

### 8.1 核心 API 签名

```csharp
public partial void Enable(EnableCap target);
public partial void Disable(EnableCap target);
public partial void BlendFunc(BlendingFactor sfactor, BlendingFactor dfactor);
public partial void BlendFuncSeparate(BlendingFactor srcRGB, BlendingFactor dstRGB,
                                      BlendingFactor srcAlpha, BlendingFactor dstAlpha);
public partial void CullFace(TriangleFace mode);
public partial void FrontFace(FrontFaceDirection mode);
public partial void DepthFunc(DepthFunction func);
public partial void DepthMask(bool flag);
public partial void PolygonMode(TriangleFace face, PolygonMode mode);
public partial void Viewport(int x, int y, uint width, uint height);
public partial void Clear(ClearBufferMask mask);
public partial void ClearColor(float r, float g, float b, float a);
```

### 8.2 状态配置速查

```csharp
// 深度测试
gl.Enable(EnableCap.DepthTest);
gl.DepthFunc(DepthFunction.Less);
gl.DepthMask(true);

// 背面剔除
gl.Enable(EnableCap.CullFace);
gl.CullFace(TriangleFace.Back);
gl.FrontFace(FrontFaceDirection.CCW);

// Alpha 混合
gl.Enable(EnableCap.Blend);
gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

// 清屏
gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

// 视口
gl.Viewport(0, 0, (uint)width, (uint)height);
```

---

## 9. Uniform 与矩阵传递

### 9.1 核心 API 签名

```csharp
// 标量 / 向量
public partial void Uniform1(int location, int v0);
public partial void Uniform1(int location, float v0);
public partial void Uniform2(int location, float v0, float v1);
public partial void Uniform3(int location, float v0, float v1, float v2);
public partial void Uniform4(int location, float v0, float v1, float v2, float v3);

// 矩阵（重点）
public partial void UniformMatrix4(int location, uint count, bool transpose,
    ref readonly float value);
```

### 9.2 矩阵传递最佳实践

Silk.NET 示例中直接使用 `System.Numerics.Matrix4x4`，并通过 `unsafe` 取地址传递：

```csharp
public unsafe void SetUniform(string name, Matrix4x4 value)
{
    int loc = gl.GetUniformLocation(_handle, name);
    if (loc == -1) throw new InvalidOperationException($"Uniform {name} not found.");
    gl.UniformMatrix4(loc, 1, false, (float*)&value);
}
```

> **注意**：`Matrix4x4` 在 `System.Numerics` 中是行优先布局（Row-Major），但 GLSL 默认按列优先（Column-Major）读取。若你在 C# 中按常规数学顺序构建矩阵，通常应设置 `transpose = false`，因为 `Matrix4x4` 的内存布局恰好与 GLSL `mat4` 的列优先布局兼容（`.M11` 到 `.M44` 的线性展开实际上符合列优先）。如果你自行构建 float[16] 并按行优先填充，则需要 `transpose = true`。

### 9.3 完整的 Shader Uniform 封装（推荐）

```csharp
public unsafe class Shader : IDisposable
{
    private uint _handle;
    private GL _gl;

    public Shader(GL gl, string vertSource, string fragSource)
    {
        _gl = gl;
        uint vs = Compile(ShaderType.VertexShader, vertSource);
        uint fs = Compile(ShaderType.FragmentShader, fragSource);
        _handle = _gl.CreateProgram();
        _gl.AttachShader(_handle, vs);
        _gl.AttachShader(_handle, fs);
        _gl.LinkProgram(_handle);
        CheckLink();
        _gl.DetachShader(_handle, vs);
        _gl.DetachShader(_handle, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
    }

    public void Use() => _gl.UseProgram(_handle);

    public void SetUniform(string name, int v)      => _gl.Uniform1(GetLoc(name), v);
    public void SetUniform(string name, float v)    => _gl.Uniform1(GetLoc(name), v);
    public void SetUniform(string name, Vector3 v)  => _gl.Uniform3(GetLoc(name), v.X, v.Y, v.Z);
    public void SetUniform(string name, Vector4 v)  => _gl.Uniform4(GetLoc(name), v.X, v.Y, v.Z, v.W);
    public void SetUniform(string name, Matrix4x4 m)=> _gl.UniformMatrix4(GetLoc(name), 1, false, (float*)&m);

    private int GetLoc(string name)
    {
        int loc = _gl.GetUniformLocation(_handle, name);
        if (loc == -1) throw new InvalidOperationException($"Uniform '{name}' not found.");
        return loc;
    }

    private uint Compile(ShaderType type, string src)
    {
        uint s = _gl.CreateShader(type);
        _gl.ShaderSource(s, src);
        _gl.CompileShader(s);
        _gl.GetShader(s, GLEnum.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(_gl.GetShaderInfoLog(s));
        return s;
    }

    private void CheckLink()
    {
        _gl.GetProgram(_handle, GLEnum.LinkStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(_gl.GetProgramInfoLog(_handle));
    }

    public void Dispose() => _gl.DeleteProgram(_handle);
}
```

---

## 10. 官方示例抽象复用

Silk.NET 官方 OpenGL Tutorial 1.4 提供了可直接搬到生产中的三层抽象：**BufferObject**、**VertexArrayObject**、**Shader**。下面给出整理后的完整代码。

### 10.1 BufferObject<T>

```csharp
public class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    private uint _handle;
    private BufferTargetARB _bufferType;
    private GL _gl;

    public unsafe BufferObject(GL gl, Span<TDataType> data, BufferTargetARB bufferType)
    {
        _gl = gl;
        _bufferType = bufferType;
        _handle = _gl.GenBuffer();
        Bind();
        fixed (void* d = data)
        {
            _gl.BufferData(bufferType, (nuint)(data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }
    }

    public void Bind() => _gl.BindBuffer(_bufferType, _handle);
    public void Dispose() => _gl.DeleteBuffer(_handle);
}
```

### 10.2 VertexArrayObject<TVertex, TIndex>

```csharp
public class VertexArrayObject<TVertexType, TIndexType> : IDisposable
    where TVertexType : unmanaged where TIndexType : unmanaged
{
    private uint _handle;
    private GL _gl;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vbo, BufferObject<TIndexType> ebo)
    {
        _gl = gl;
        _handle = _gl.GenVertexArray();
        Bind();
        vbo.Bind();
        ebo.Bind();
    }

    public unsafe void VertexAttributePointer(uint index, int count, VertexAttribPointerType type,
                                              uint vertexSize, int offSet)
    {
        _gl.VertexAttribPointer(index, count, type, false,
                                vertexSize * (uint)sizeof(TVertexType),
                                (void*)(offSet * sizeof(TVertexType)));
        _gl.EnableVertexAttribArray(index);
    }

    public void Bind() => _gl.BindVertexArray(_handle);
    public void Dispose() => _gl.DeleteVertexArray(_handle);
}
```

### 10.3 Texture（官方示例扩展版）

```csharp
public class Texture : IDisposable
{
    private uint _handle;
    private GL _gl;

    public unsafe Texture(GL gl, string path)
    {
        _gl = gl;
        _handle = _gl.GenTexture();
        Bind();

        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        fixed (byte* ptr = image.Data)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                           (uint)image.Width, (uint)image.Height,
                           0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
        SetParameters();
    }

    private void SetParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(slot);
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
    }

    public void Dispose() => _gl.DeleteTexture(_handle);
}
```

---

## 11. 与 Kilo.Rendering 的集成建议

### 11.1 架构定位

| 层级 | 职责 | 对应 Silk.NET API |
|------|------|-------------------|
| **RHI (Rendering Hardware Interface)** | 直接调用 OpenGL，封装对象生命周期 | `GL` 类、Buffer/VAO/Texture/Shader 抽象 |
| **Resource Manager** | 跟踪 GPU 资源句柄、热重载、释放 | `Gen*/Delete*` |
| **Render Pass / FrameGraph** | 管理 FBO、Clear、State 切换 | `BindFramebuffer`、`Enable`、`BlendFunc` 等 |
| **Material / Shader** | Uniform 缓存、Pipline State 对象 | `UseProgram`、`Uniform*` |

### 11.2 推荐的 RHI 接口形状

```csharp
public interface IRenderDevice
{
    GL Context { get; }
    IBuffer CreateBuffer<T>(ReadOnlySpan<T> data, BufferTargetARB target, BufferUsageARB usage) where T : unmanaged;
    IVertexArray CreateVertexArray(IBuffer vbo, IBuffer ebo, VertexLayout layout);
    IShader CreateShader(string vertexSource, string fragmentSource);
    ITexture CreateTexture2D(int width, int height, ReadOnlySpan<byte> data);
    IFramebuffer CreateFramebuffer(int width, int height, FramebufferAttachment[] attachments);

    void SetViewport(int x, int y, int width, int height);
    void Clear(ClearBufferMask mask, Color clearColor);
    void DrawIndexed(uint indexCount, DrawElementsType type, nint offset = 0);
    void DrawArrays(uint vertexCount);
}
```

### 11.3 性能注意事项

1. **减少 GL 状态切换**：按 `Shader -> Texture -> VAO` 排序渲染队列，最小化 `UseProgram` 和 `BindTexture` 调用次数。
2. **Uniform 缓存**：`GetUniformLocation` 较慢，应在 Shader 链接后预取所有 Uniform Location，渲染时直接传 `int location`。
3. **BufferSubData vs MapBuffer**：对于每帧更新的 Uniform/Dynamic VBO，优先使用 `BufferSubData` 或 DSA（Direct State Access）接口；若数据量极大，可考虑 `MapBufferRange`。
4. **Texture Upload**：StbImageSharp 返回的像素数组是 `byte[]`，上传后尽快释放，避免托管堆压力。
5. **FBO 状态检查**：`CheckFramebufferStatus` 仅在创建时调用即可，渲染循环中不要重复调用。

---

## 12. 常见陷阱与排查

| 现象 | 可能原因 | 排查方法 |
|------|----------|----------|
| 黑屏 / 无输出 | VAO 未绑定即 Draw；Shader 未 `UseProgram`；Viewport 尺寸为 0 | 检查 `BindVertexArray`、`UseProgram`、`Viewport` 调用顺序 |
| 纹理全黑 | 未 `ActiveTexture` + `BindTexture`；Uniform sampler 值未设置 | 确认 `SetUniform("uTexture", 0)` 与 `ActiveTexture(Texture0)` 配对 |
| 深度冲突（Z-fighting） | 未开启 `DepthTest`；`Clear` 未带 `DepthBufferBit` | `Enable(DepthTest)` + `Clear(Color\|Depth)` |
| 编译/链接报错 | GLSL 版本与上下文不匹配；uniform 名拼写错误 | 读取 `GetShaderInfoLog` / `GetProgramInfoLog` |
| 性能骤降 | 每帧重复 `GetUniformLocation`；频繁 `Gen/Delete` | 预缓存 location；使用对象池/资源管理器 |
| 矩阵显示错乱 | `transpose` 参数与 C# 矩阵布局不一致 | 确认 `Matrix4x4` 布局与 GLSL `mat4` 的列主序匹配 |

---

## 附录：核心类型与命名空间速查

```csharp
using Silk.NET.OpenGL;           // GL, GLEnum, PrimitiveType, BufferTargetARB, ...
using Silk.NET.Windowing;        // IWindow, WindowOptions
using Silk.NET.Maths;            // Vector2D<T>, MathHelper
using System.Numerics;           // Matrix4x4, Vector3, Vector4
```

---

*文档版本：基于 Silk.NET OpenGL 源码（最新 `main` 分支 shallow clone）整理*
*适用目标：`Kilo.Rendering` 渲染后端开发*
