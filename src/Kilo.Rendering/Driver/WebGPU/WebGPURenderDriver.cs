using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public sealed unsafe partial class WebGPURenderDriver : IRenderDriver
{
    internal readonly WgpuApi Wgpu;
    internal readonly Instance* Instance;
    internal readonly Adapter* Adapter;
    internal readonly Device* Device;
    private readonly Surface* _surface;
    private readonly Queue* _queue;
    private SurfaceConfiguration _surfaceConfig;
    private TextureFormat _swapchainFormat;
    private WebGPUTexture? _currentFrameTexture;
    private bool _disposed;

    public DriverPixelFormat SwapchainFormat => _swapchainFormat == TextureFormat.Bgra8UnormSrgb
        ? DriverPixelFormat.BGRA8UnormSrgb
        : DriverPixelFormat.BGRA8Unorm;

    public WebGPURenderDriver(WgpuApi wgpu, Instance* instance, Adapter* adapter,
                               Device* device, Surface* surface)
    {
        Wgpu = wgpu;
        Instance = instance;
        Adapter = adapter;
        Device = device;
        _surface = surface;
        _queue = wgpu.DeviceGetQueue(device);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentFrameTexture?.Dispose();
        Wgpu.DeviceRelease(Device);
        Wgpu.AdapterRelease(Adapter);
        Wgpu.SurfaceRelease(_surface);
        Wgpu.InstanceRelease(Instance);
    }
}
