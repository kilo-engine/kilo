using System.Numerics;
using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class RenderGraph : IDisposable
{
    private readonly List<RenderPass> _passes = [];
    private readonly Dictionary<int, object> _resourceDescriptors = [];
    private readonly Dictionary<int, object> _resolvedResources = [];
    private readonly Dictionary<string, RenderResourceHandle> _importedResources = [];
    private readonly Dictionary<string, ITexture> _externalTextures = [];
    private readonly Dictionary<int, ITextureView> _textureViews = [];
    private readonly RenderGraphResourcePool _resourcePool = new();
    private int _nextHandleId;
    private int _structureVersion;
    private int _lastCompiledStructureVersion;
    private bool _isCompiled;
    private int _lastWindowWidth;
    private int _lastWindowHeight;

    public RenderPass AddPass(string name, Action<PassBuilder> setup, Action<RenderPassExecutionContext> execute)
    {
        var pass = new RenderPass(name, false, setup, execute);
        _passes.Add(pass);
        _structureVersion++;
        _isCompiled = false;
        return pass;
    }

    public RenderPass AddComputePass(string name, Action<ComputePassBuilder> setup, Action<RenderPassExecutionContext> execute)
    {
        var pass = new RenderPass(name, true, builder => setup(new ComputePassBuilder(builder)), execute);
        _passes.Add(pass);
        _structureVersion++;
        _isCompiled = false;
        return pass;
    }

    internal RenderResourceHandle AllocateHandle(RenderResourceType type, object descriptor)
    {
        int id = _nextHandleId++;
        var handle = new RenderResourceHandle(id, type);
        _resourceDescriptors[id] = descriptor;
        return handle;
    }

    internal RenderResourceHandle ImportResource(string name, RenderResourceType type, object descriptor)
    {
        if (_importedResources.TryGetValue(name, out var existing))
            return existing;
        var handle = AllocateHandle(type, descriptor);
        _importedResources[name] = handle;
        return handle;
    }

    internal ITexture GetResolvedTexture(RenderResourceHandle handle)
        => (ITexture)_resolvedResources[handle.Id];

    internal IBuffer GetResolvedBuffer(RenderResourceHandle handle)
        => (IBuffer)_resolvedResources[handle.Id];

    /// <summary>
    /// Registers an externally managed texture that can be imported by name in render passes.
    /// The texture is resolved each frame during Execute().
    /// </summary>
    public void RegisterExternalTexture(string name, ITexture texture)
    {
        _externalTextures[name] = texture;
    }

    internal ITexture GetResolvedTextureByName(string name)
    {
        if (_importedResources.TryGetValue(name, out var handle))
            return (ITexture)_resolvedResources[handle.Id];
        throw new InvalidOperationException($"Imported resource '{name}' not found.");
    }

    internal RenderResourceHandle GetImportedHandle(string name)
    {
        if (_importedResources.TryGetValue(name, out var handle))
            return handle;
        throw new InvalidOperationException($"Imported resource '{name}' not found.");
    }

    internal ITextureView GetOrCreateTextureView(IRenderDriver driver, RenderResourceHandle handle, ITexture texture)
    {
        if (_textureViews.TryGetValue(handle.Id, out var view))
            return view;

        var descriptor = (TextureDescriptor)_resourceDescriptors[handle.Id];
        view = driver.CreateTextureView(texture, new TextureViewDescriptor
        {
            Format = texture.Format,
            MipLevelCount = descriptor.MipLevelCount,
            Dimension = TextureViewDimension.View2D,
        });
        _textureViews[handle.Id] = view;
        return view;
    }

    public void Compile(IRenderDriver driver)
    {
        if (_isCompiled && _structureVersion == _lastCompiledStructureVersion)
            return;

        // Reset transient resources so setup is deterministic across compiles
        ClearTransientResources();

        // Phase 1: Run setup on all passes to collect resource declarations
        foreach (var pass in _passes)
        {
            pass.ReadResources.Clear();
            pass.WrittenResources.Clear();
            pass.CreatedResources.Clear();
            pass.ColorAttachments.Clear();
            pass.DepthStencilAttachment = null;
            pass.Viewport = null;
            pass.Scissor = null;
            var builder = new PassBuilder(this, pass);
            pass.RunSetup(builder);
        }

        // Phase 2: Topological sort based on resource dependencies
        var sorted = RenderGraphCompiler.Compile(_passes);
        _passes.Clear();
        _passes.AddRange(sorted);

        // Phase 3: Create GPU resources via driver (reusing pooled resources when possible)
        var swapchain = driver.GetCurrentSwapchainTexture();
        if (swapchain.Width != _lastWindowWidth || swapchain.Height != _lastWindowHeight)
        {
            _resourcePool.InvalidateForSize(swapchain.Width, swapchain.Height);
            _lastWindowWidth = swapchain.Width;
            _lastWindowHeight = swapchain.Height;
        }

        var importedIds = new HashSet<int>(_importedResources.Values.Select(h => h.Id));
        foreach (var (id, descriptor) in _resourceDescriptors)
        {
            if (_resolvedResources.ContainsKey(id)) continue;
            if (importedIds.Contains(id)) continue; // Imported resources are resolved at Execute time, not created here
            _resolvedResources[id] = descriptor switch
            {
                TextureDescriptor td => _resourcePool.GetTexture(driver, td),
                BufferDescriptor bd => _resourcePool.GetBuffer(driver, bd),
                _ => throw new InvalidOperationException($"Unknown descriptor type: {descriptor.GetType()}")
            };
        }

        _lastCompiledStructureVersion = _structureVersion;
        _isCompiled = true;
    }

    public void Execute(IRenderDriver driver)
    {
        Compile(driver);

        // Ensure Backbuffer imported resource exists
        if (!_importedResources.ContainsKey("Backbuffer"))
        {
            var swapchain = driver.GetCurrentSwapchainTexture();
            ImportResource("Backbuffer", RenderResourceType.Texture, new TextureDescriptor
            {
                Width = swapchain.Width,
                Height = swapchain.Height,
                Format = swapchain.Format,
                Usage = TextureUsage.RenderAttachment,
            });
        }
        // Resolve the backbuffer each frame
        if (_importedResources.TryGetValue("Backbuffer", out var backbufferHandle))
        {
            var swapchainTexture = driver.GetCurrentSwapchainTexture();
            _resolvedResources[backbufferHandle.Id] = swapchainTexture;
        }

        // Resolve other external textures registered via RegisterExternalTexture
        foreach (var (name, texture) in _externalTextures)
        {
            if (_importedResources.TryGetValue(name, out var handle))
            {
                _resolvedResources[handle.Id] = texture;
            }
        }

        using var encoder = driver.BeginCommandEncoding();

        try
        {
            foreach (var pass in _passes)
            {
                if (pass.IsCompute)
                {
                    encoder.BeginComputePass();
                }
                else
                {
                    BeginRenderPassForPass(driver, encoder, pass);
                }

                var ctx = new RenderPassExecutionContext(this, driver, encoder);
                pass.RunExecute(ctx);

                if (pass.IsCompute)
                {
                    encoder.EndComputePass();
                }
                else
                {
                    encoder.EndRenderPass();
                }
            }

            encoder.Submit();
        }
        finally
        {
            ClearTransientResources();
        }
    }

    private void BeginRenderPassForPass(IRenderDriver driver, IRenderCommandEncoder encoder, RenderPass pass)
    {
        if (pass.Viewport.HasValue)
        {
            var vp = pass.Viewport.Value;
            encoder.SetViewport(vp.X, vp.Y, vp.Z, vp.W);
        }
        if (pass.Scissor.HasValue)
        {
            var sc = pass.Scissor.Value;
            encoder.SetScissor(sc.X, sc.Y, (uint)sc.Z, (uint)sc.W);
        }

        // Depth-only passes (no color attachments but has depth) still need BeginRenderPass
        if (pass.ColorAttachments.Count == 0 && pass.DepthStencilAttachment == null)
            return;

        var colorAttachments = new ColorAttachmentDescriptor[pass.ColorAttachments.Count];
        for (int i = 0; i < pass.ColorAttachments.Count; i++)
        {
            var ca = pass.ColorAttachments[i];
            var texture = GetResolvedTexture(ca.Target);
            var view = GetOrCreateTextureView(driver, ca.Target, texture);
            colorAttachments[i] = new ColorAttachmentDescriptor
            {
                RenderTarget = view,
                LoadAction = ca.LoadAction,
                StoreAction = ca.StoreAction,
                ClearColor = ca.ClearColor,
            };
        }

        DepthStencilAttachmentDescriptor? depthAttachment = null;
        if (pass.DepthStencilAttachment != null)
        {
            var ds = pass.DepthStencilAttachment;
            var texture = GetResolvedTexture(ds.Target);
            var view = GetOrCreateTextureView(driver, ds.Target, texture);
            depthAttachment = new DepthStencilAttachmentDescriptor
            {
                View = view,
                DepthLoadAction = ds.DepthLoadAction,
                DepthStoreAction = ds.DepthStoreAction,
                ClearDepth = ds.ClearDepth,
            };
        }

        encoder.BeginRenderPass(new RenderPassDescriptor
        {
            ColorAttachments = colorAttachments,
            DepthStencilAttachment = depthAttachment,
        });
    }

    private void ClearTransientResources()
    {
        var importedIds = new HashSet<int>(_importedResources.Values.Select(h => h.Id));

        // Return transient resources to the pool for reuse
        foreach (var (id, resource) in _resolvedResources.ToList())
        {
            if (importedIds.Contains(id)) continue;
            if (_resourceDescriptors.TryGetValue(id, out var descriptor))
            {
                switch (resource)
                {
                    case ITexture tex when descriptor is TextureDescriptor td:
                        _resourcePool.ReturnTexture(tex, td);
                        break;
                    case IBuffer buf when descriptor is BufferDescriptor bd:
                        _resourcePool.ReturnBuffer(buf, bd);
                        break;
                }
            }
        }

        foreach (var id in _resourceDescriptors.Keys.Where(k => !importedIds.Contains(k)).ToList())
        {
            _resourceDescriptors.Remove(id);
            _resolvedResources.Remove(id);
            if (_textureViews.TryGetValue(id, out var view))
            {
                view.Dispose();
                _textureViews.Remove(id);
            }
        }
        _nextHandleId = importedIds.Count > 0 ? importedIds.Max() + 1 : 0;
    }

    public void Reset()
    {
        _passes.Clear();
        _importedResources.Clear();
        _structureVersion++;
        ClearTransientResources();
        _isCompiled = false;
    }

    /// <summary>
    /// Prepares the graph for a new frame: clears passes and texture views but preserves the resource pool.
    /// </summary>
    public void BeginFrame()
    {
        _passes.Clear();
        _structureVersion++;
        _isCompiled = false;

        // Clear cached texture views — swapchain texture changes every frame
        foreach (var view in _textureViews.Values)
            view.Dispose();
        _textureViews.Clear();

        // Clear resolved resources for imported handles so they get re-resolved
        var importedIds = new HashSet<int>(_importedResources.Values.Select(h => h.Id));
        foreach (var id in importedIds)
            _resolvedResources.Remove(id);
    }

    public void Dispose()
    {
        ClearTransientResources();
        _resourcePool.Clear();
    }
}

internal static class RenderGraphExtensions
{
}
