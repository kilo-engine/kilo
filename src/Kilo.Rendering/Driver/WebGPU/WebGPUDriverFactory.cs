using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;

public static unsafe class WebGPUDriverFactory
{
    public static WebGPURenderDriver Create(IWindow window, RenderSettings settings)
    {
        var wgpu = WgpuApi.GetApi();

        // Create instance
        InstanceDescriptor instanceDesc = new();
        var instance = wgpu.CreateInstance(&instanceDesc);

        // Create surface from window
        var surface = window.CreateWebGPUSurface(wgpu, instance);

        // Request adapter
        Adapter* adapter = null;
        RequestAdapterOptions adapterOpts = new() { CompatibleSurface = surface };
        wgpu.InstanceRequestAdapter(instance, in adapterOpts,
            new PfnRequestAdapterCallback((_, a, _, _) => adapter = a), null);

        if (adapter == null)
            throw new InvalidOperationException("Failed to get WebGPU adapter.");

        // Get surface capabilities
        SurfaceCapabilities surfaceCaps = new();
        wgpu.SurfaceGetCapabilities(surface, adapter, ref surfaceCaps);
        var swapchainFormat = *surfaceCaps.Formats; // typically Bgra8Unorm

        // Get adapter limits and raise MaxBindGroups for skinned mesh (needs 5 bind groups)
        Device* device = null;
        SupportedLimits supportedLimits = new();
        wgpu.AdapterGetLimits(adapter, ref supportedLimits);
        supportedLimits.Limits.MaxBindGroups = Math.Max(supportedLimits.Limits.MaxBindGroups, 5);
        var requiredLimits = new RequiredLimits
        {
            Limits = supportedLimits.Limits,
        };
        DeviceDescriptor deviceDesc = new()
        {
            RequiredLimits = &requiredLimits,
            DeviceLostCallback = new PfnDeviceLostCallback((reason, msg, _) =>
                Console.WriteLine($"[WebGPU] Device lost: {reason} - {SilkMarshal.PtrToString((nint)msg)}")),
        };
        wgpu.AdapterRequestDevice(adapter, in deviceDesc,
            new PfnRequestDeviceCallback((_, d, _, _) => device = d), null);

        if (device == null)
            throw new InvalidOperationException("Failed to get WebGPU device.");

        // Set error callback
        wgpu.DeviceSetUncapturedErrorCallback(device,
            new PfnErrorCallback((type, msg, _) =>
                Console.WriteLine($"[WebGPU] Error: {type} - {SilkMarshal.PtrToString((nint)msg)}")),
            null);

        var driver = new WebGPURenderDriver(wgpu, instance, adapter, device, surface);
        driver.SetSurfaceFormat(swapchainFormat);
        driver.ConfigureSurface(settings.Width, settings.Height);

        return driver;
    }
}
