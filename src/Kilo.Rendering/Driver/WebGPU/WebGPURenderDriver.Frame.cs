using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

public sealed unsafe partial class WebGPURenderDriver
{
    // --- Frame lifecycle ---

    public ITexture GetCurrentSwapchainTexture()
    {
        if (_currentFrameTexture != null) return _currentFrameTexture;

        SurfaceTexture surfaceTexture;
        Wgpu.SurfaceGetCurrentTexture(_surface, &surfaceTexture);

        if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
        {
            Wgpu.TextureRelease(surfaceTexture.Texture);
            ConfigureSurface((int)_surfaceConfig.Width, (int)_surfaceConfig.Height);
            Wgpu.SurfaceGetCurrentTexture(_surface, &surfaceTexture);
        }

        var driverFormat = _swapchainFormat == TextureFormat.Bgra8UnormSrgb
            ? DriverPixelFormat.BGRA8UnormSrgb
            : DriverPixelFormat.BGRA8Unorm;
        _currentFrameTexture = new WebGPUTexture(Wgpu, surfaceTexture.Texture,
            (int)_surfaceConfig.Width, (int)_surfaceConfig.Height, driverFormat);
        return _currentFrameTexture;
    }

    public void BeginFrame()
    {
        // Don't dispose the swapchain texture here — it's released after Present()
        _currentFrameTexture = null;
    }

    public IRenderCommandEncoder BeginCommandEncoding()
    {
        var encoder = Wgpu.DeviceCreateCommandEncoder(Device, new CommandEncoderDescriptor());
        return new WebGPUCommandEncoder(Wgpu, Device, encoder);
    }

    public void EndFrame() { }

    public void Present()
    {
        Wgpu.SurfacePresent(_surface);
        _currentFrameTexture?.Dispose();
        _currentFrameTexture = null;
    }

    // --- Surface management ---

    public void ConfigureSurface(int width, int height)
    {
        _surfaceConfig = new SurfaceConfiguration
        {
            Usage = Silk.NET.WebGPU.TextureUsage.RenderAttachment | Silk.NET.WebGPU.TextureUsage.CopySrc,
            Format = _swapchainFormat,
            PresentMode = PresentMode.Fifo,
            Device = Device,
            Width = (uint)width,
            Height = (uint)height,
        };
        Wgpu.SurfaceConfigure(_surface, in _surfaceConfig);
    }

    public void ResizeSurface(int width, int height)
    {
        ConfigureSurface(width, height);
    }

    internal void SetSurfaceFormat(TextureFormat format)
    {
        _swapchainFormat = format;
    }
}
