using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Kilo.Rendering.Driver;
using Kilo.Rendering.RenderGraph;

namespace Kilo.Rendering;

/// <summary>
/// Manages ImGui context, GPU resources, and rendering through IRenderDriver.
/// </summary>
public sealed class ImGuiController : IDisposable
{
    private IRenderDriver? _driver;
    private IRenderPipeline? _pipeline;
    private IBuffer? _uniformBuffer;
    private IBindingSet? _uniformSamplerBindingSet;
    private IBindingSet? _fontTextureBindingSet;
    private ISampler? _sampler;
    private ITexture? _fontTexture;
    private ITextureView? _fontTextureView;
    private IBuffer? _vertexBuffer;
    private IBuffer? _indexBuffer;
    private nuint _vertexBufferSize;
    private nuint _indexBufferSize;
    private bool _initialized;
    private bool _disposed;
    private int _width;
    private int _height;
    private readonly bool[] _prevKeysDown = new bool[512];
    private bool _firstFrame = true;

    /// <summary>Whether ImGui wants to capture mouse input.</summary>
    public bool WantCaptureMouse { get; private set; }

    /// <summary>Whether ImGui wants to capture keyboard input.</summary>
    public bool WantCaptureKeyboard { get; private set; }

    /// <summary>
    /// Initialize ImGui context and GPU resources.
    /// </summary>
    public void Initialize(IRenderDriver driver, int width, int height)
    {
        _driver = driver;
        _width = width;
        _height = height;

        // Create ImGui context
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.DisplaySize = new System.Numerics.Vector2(width, height);
        io.DisplayFramebufferScale = new System.Numerics.Vector2(1f, 1f);

        // Upload font texture
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int texWidth, out int texHeight, out int bytesPerPixel);
        int textureSize = texWidth * texHeight * bytesPerPixel;

        _fontTexture = driver.CreateTexture(new TextureDescriptor
        {
            Width = texWidth,
            Height = texHeight,
            Format = DriverPixelFormat.RGBA8Unorm,
            Usage = TextureUsage.CopyDst | TextureUsage.ShaderBinding,
        });

        var pixelData = new byte[textureSize];
        Marshal.Copy(pixels, pixelData, 0, textureSize);
        _fontTexture.UploadData<byte>(pixelData);

        _fontTextureView = driver.CreateTextureView(_fontTexture, new TextureViewDescriptor
        {
            Format = DriverPixelFormat.RGBA8Unorm,
            Dimension = TextureViewDimension.View2D,
            MipLevelCount = 1,
        });

        io.Fonts.SetTexID((IntPtr)1);
        io.Fonts.ClearTexData();

        // Create sampler
        _sampler = driver.CreateSampler(new SamplerDescriptor
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            AddressModeU = WrapMode.ClampToEdge,
            AddressModeV = WrapMode.ClampToEdge,
            AddressModeW = WrapMode.ClampToEdge,
        });

        // Create shader modules
        var vs = driver.CreateShaderModule(ImGuiShaders.WGSL, "vs_main");
        var fs = driver.CreateShaderModule(ImGuiShaders.WGSL, "fs_main");

        // Create pipeline with ImGui vertex layout (pos:float2 + uv:float2 + col:unorm8x4 = 20 bytes)
        _pipeline = driver.CreateRenderPipeline(new RenderPipelineDescriptor
        {
            VertexShader = vs,
            FragmentShader = fs,
            Topology = DriverPrimitiveTopology.TriangleList,
            SampleCount = 1,
            VertexBuffers =
            [
                new VertexBufferLayout
                {
                    ArrayStride = 20,
                    Attributes =
                    [
                        new VertexAttributeDescriptor { ShaderLocation = 0, Format = VertexFormat.Float32x2, Offset = 0 },
                        new VertexAttributeDescriptor { ShaderLocation = 1, Format = VertexFormat.Float32x2, Offset = 8 },
                        new VertexAttributeDescriptor { ShaderLocation = 2, Format = VertexFormat.Unorm8x4, Offset = 16 },
                    ],
                },
            ],
            ColorTargets =
            [
                new ColorTargetDescriptor
                {
                    Format = driver.SwapchainFormat,
                    Blend = new BlendStateDescriptor
                    {
                        Color = new BlendComponentDescriptor
                        {
                            SrcFactor = DriverBlendFactor.SrcAlpha,
                            DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                            Operation = BlendOperation.Add,
                        },
                        Alpha = new BlendComponentDescriptor
                        {
                            SrcFactor = DriverBlendFactor.One,
                            DstFactor = DriverBlendFactor.OneMinusSrcAlpha,
                            Operation = BlendOperation.Add,
                        },
                    },
                },
            ],
        });

        // Create uniform buffer for MVP matrix (64 bytes)
        _uniformBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = 64,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
        });

        // Create binding sets
        // Group 0: uniform buffer (binding 0) + sampler (binding 1)
        _uniformSamplerBindingSet = driver.CreateBindingSetForPipeline(_pipeline, 0,
            uniformBuffers: [new UniformBufferBinding { Buffer = _uniformBuffer, Binding = 0 }],
            textures: [],
            samplers: [new SamplerBinding { Sampler = _sampler, Binding = 1 }]);

        // Group 1: font texture (binding 0)
        _fontTextureBindingSet = driver.CreateBindingSetForPipeline(_pipeline, 1,
            textures: [new TextureBinding { TextureView = _fontTextureView, Binding = 0 }],
            samplers: []);

        // Initial vertex/index buffers (will grow as needed)
        _vertexBufferSize = 65536;
        _indexBufferSize = 32768;
        _vertexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = _vertexBufferSize,
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
        });
        _indexBuffer = driver.CreateBuffer(new BufferDescriptor
        {
            Size = _indexBufferSize,
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
        });

        _initialized = true;
        Console.WriteLine($"[ImGui] Initialized: font texture {texWidth}x{texHeight}, pipeline created");
    }

    /// <summary>
    /// Update ImGui input state from raw input data.
    /// </summary>
    public void UpdateInput(
        int width, int height,
        Vector2 mousePos, float scrollDelta,
        ReadOnlySpan<bool> mouseButtonsDown,
        ReadOnlySpan<bool> keysDown,
        ReadOnlySpan<bool> keysPressed,
        ReadOnlySpan<bool> keysReleased,
        ReadOnlySpan<char> textInput,
        float deltaTime)
    {
        if (!_initialized) return;

        _width = width;
        _height = height;

        var io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(width > 0 ? width : 1, height > 0 ? height : 1);
        io.DeltaTime = deltaTime > 0 ? deltaTime : 1f / 60f;

        // Mouse
        io.MousePos = new System.Numerics.Vector2(mousePos.X, mousePos.Y);
        io.MouseWheel = scrollDelta;
        for (int i = 0; i < Math.Min(5, mouseButtonsDown.Length); i++)
            io.MouseDown[i] = mouseButtonsDown[i];

        // Keyboard — feed key events to ImGui.
        // Key challenge: fast taps (press+release within one frame) must be split across
        // two frames because ImGui's AddKeyEvent(true) followed by AddKeyEvent(false) in
        // the same call effectively cancels the press (DownDuration reverts to -1).
        for (int i = 0; i < Math.Min(512, keysDown.Length); i++)
        {
            var imguiKey = ImGuiKeyMap.Map(i);
            if (imguiKey == ImGuiKey.None) continue;

            bool pressed = i < keysPressed.Length && keysPressed[i];
            bool released = i < keysReleased.Length && keysReleased[i];

            if (pressed)
            {
                io.AddKeyEvent(imguiKey, true);
                if (released)
                {
                    // Fast tap: defer release to next frame via _prevKeysDown
                    // so ImGui sees the press this frame, release next frame.
                    _prevKeysDown[i] = true;
                    continue;
                }
            }
            else if (released)
            {
                io.AddKeyEvent(imguiKey, false);
            }
            else if (keysDown[i] != _prevKeysDown[i])
            {
                // Held-key transition (e.g. delayed release from a fast tap)
                io.AddKeyEvent(imguiKey, keysDown[i]);
            }

            _prevKeysDown[i] = keysDown[i];
        }

        // Text input
        foreach (char c in textInput)
            io.AddInputCharacter(c);

        WantCaptureMouse = io.WantCaptureMouse;
        WantCaptureKeyboard = io.WantCaptureKeyboard;
    }

    /// <summary>
    /// Call after user ImGui drawing code. Uploads draw data to GPU buffers.
    /// </summary>
    public void UpdateBuffers()
    {
        if (!_initialized) return;

        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount == 0)
        {
            if (_firstFrame) Console.WriteLine("[ImGui] WARNING: No draw data on first frame");
            return;
        }

        if (_firstFrame)
        {
            Console.WriteLine($"[ImGui] First frame: {drawData.CmdListsCount} lists, {drawData.TotalVtxCount} vtx, {drawData.TotalIdxCount} idx");
            _firstFrame = false;
        }

        // Update MVP matrix (orthographic projection matching ImGui's coordinate system)
        float L = 0f, R = _width, T = 0f, B = _height;
        var mvp = Matrix4x4.Identity;
        mvp.M11 = 2f / (R - L);
        mvp.M22 = 2f / (T - B);
        mvp.M33 = 0.5f;
        mvp.M41 = (R + L) / (L - R);
        mvp.M42 = (T + B) / (B - T);
        mvp.M43 = 0.5f;
        mvp.M44 = 1f;

        var mvpData = new byte[64];
        MemoryMarshal.Write(mvpData, in mvp);
        _uniformBuffer!.UploadData<byte>(mvpData);

        // Merge all command lists into contiguous vertex/index arrays
        int totalVtx = drawData.TotalVtxCount;
        int totalIdx = drawData.TotalIdxCount;
        if (totalVtx == 0 || totalIdx == 0) return;

        int vtxBytes = totalVtx * 20; // 20 bytes per ImDrawVert
        int idxBytes = totalIdx * 4;   // Expand uint16 → uint32 (driver uses Uint32 index format)

        // Grow buffers if needed
        if ((nuint)vtxBytes > _vertexBufferSize)
        {
            _vertexBufferSize = (nuint)(vtxBytes * 2);
            _vertexBuffer = _driver!.CreateBuffer(new BufferDescriptor
            {
                Size = _vertexBufferSize,
                Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
            });
        }
        if ((nuint)idxBytes > _indexBufferSize)
        {
            _indexBufferSize = (nuint)(idxBytes * 2);
            _indexBuffer = _driver!.CreateBuffer(new BufferDescriptor
            {
                Size = _indexBufferSize,
                Usage = BufferUsage.Index | BufferUsage.CopyDst,
            });
        }

        // Copy vertex data
        var vtxData = new byte[vtxBytes];
        var idxData = new uint[totalIdx];
        int vtxOffset = 0;
        int idxOffset = 0;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            int cmdVtxBytes = cmdList.VtxBuffer.Size * 20;

            // Copy vertex data from ImGui's native buffer
            Marshal.Copy(cmdList.VtxBuffer.Data, vtxData, vtxOffset, cmdVtxBytes);

            // Expand uint16 indices to uint32
            unsafe
            {
                var src = (ushort*)cmdList.IdxBuffer.Data;
                for (int i = 0; i < cmdList.IdxBuffer.Size; i++)
                    idxData[idxOffset + i] = (uint)src[i];
            }

            vtxOffset += cmdVtxBytes;
            idxOffset += cmdList.IdxBuffer.Size;
        }

        _vertexBuffer!.UploadData<byte>(vtxData);
        _indexBuffer!.UploadData<uint>(idxData);
    }

    /// <summary>
    /// Render ImGui draw data through the command encoder.
    /// Must be called inside a render pass.
    /// </summary>
    public void Render(IRenderCommandEncoder encoder)
    {
        if (!_initialized) return;

        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount == 0) return;

        encoder.SetPipeline(_pipeline!);
        encoder.SetVertexBuffer(0, _vertexBuffer!);
        encoder.SetIndexBuffer(_indexBuffer!);
        encoder.SetBindingSet(0, _uniformSamplerBindingSet!);
        encoder.SetBindingSet(1, _fontTextureBindingSet!);

        int globalIdxOffset = 0;
        int globalVtxOffset = 0;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            for (int cmdIdx = 0; cmdIdx < cmdList.CmdBuffer.Size; cmdIdx++)
            {
                var cmd = cmdList.CmdBuffer[cmdIdx];

                // Set scissor rect (clip in ImGui coords, offset by DisplayPos)
                float clipMinX = cmd.ClipRect.X - drawData.DisplayPos.X;
                float clipMinY = cmd.ClipRect.Y - drawData.DisplayPos.Y;
                float clipMaxX = cmd.ClipRect.Z - drawData.DisplayPos.X;
                float clipMaxY = cmd.ClipRect.W - drawData.DisplayPos.Y;

                // Clamp to framebuffer bounds
                if (clipMinX < 0f) clipMinX = 0f;
                if (clipMinY < 0f) clipMinY = 0f;
                if (clipMaxX > _width) clipMaxX = _width;
                if (clipMaxY > _height) clipMaxY = _height;
                if (clipMaxX <= clipMinX || clipMaxY <= clipMinY) continue;

                encoder.SetScissor(
                    (int)clipMinX,
                    (int)clipMinY,
                    (uint)(clipMaxX - clipMinX),
                    (uint)(clipMaxY - clipMinY));

                encoder.DrawIndexed(
                    (int)cmd.ElemCount,
                    1,
                    globalIdxOffset + (int)cmd.IdxOffset,
                    globalVtxOffset + (int)cmd.VtxOffset);
            }

            globalIdxOffset += cmdList.IdxBuffer.Size;
            globalVtxOffset += cmdList.VtxBuffer.Size;
        }
    }

    /// <summary>
    /// Handle window resize — updates display size.
    /// </summary>
    public void Resize(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _uniformBuffer?.Dispose();
        _fontTextureView?.Dispose();
        _fontTexture?.Dispose();
        _sampler?.Dispose();
        // Pipeline and binding sets are managed by the driver
    }
}
