using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using RgTextureDescriptor = Kilo.Rendering.RenderGraph.TextureDescriptor;
using RgBufferDescriptor = Kilo.Rendering.RenderGraph.BufferDescriptor;
using RgTextureUsage = Kilo.Rendering.RenderGraph.TextureUsage;
using RgBufferUsage = Kilo.Rendering.RenderGraph.BufferUsage;

public sealed unsafe class WebGPURenderDriver : IRenderDriver
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

    internal void SetSurfaceFormat(TextureFormat format)
    {
        _swapchainFormat = format;
    }

    // --- Resource creation ---

    public ITexture CreateTexture(RgTextureDescriptor descriptor)
    {
        var desc = new Silk.NET.WebGPU.TextureDescriptor
        {
            Size = new Extent3D { Width = (uint)descriptor.Width, Height = (uint)descriptor.Height, DepthOrArrayLayers = 1 },
            MipLevelCount = (uint)descriptor.MipLevelCount,
            SampleCount = (uint)descriptor.SampleCount,
            Dimension = TextureDimension.Dimension2D,
            Format = MapPixelFormat(descriptor.Format),
            Usage = MapTextureUsage(descriptor.Usage),
        };
        var tex = Wgpu.DeviceCreateTexture(Device, in desc);
        return new WebGPUTexture(Wgpu, Device, tex, descriptor.Width, descriptor.Height, descriptor.Format);
    }

    public ITextureView CreateTextureView(ITexture texture, TextureViewDescriptor descriptor)
    {
        var wgpuTexture = (WebGPUTexture)texture;
        var mappedFormat = MapPixelFormat(descriptor.Format);
        var isDepthFormat = descriptor.Format == DriverPixelFormat.Depth24Plus
                         || descriptor.Format == DriverPixelFormat.Depth24PlusStencil8
                         || descriptor.Format == DriverPixelFormat.Depth32Float;
        var viewDesc = new Silk.NET.WebGPU.TextureViewDescriptor
        {
            Format = mappedFormat,
            Dimension = MapTextureViewDimension(descriptor.Dimension),
            BaseMipLevel = (uint)descriptor.BaseMipLevel,
            MipLevelCount = (uint)descriptor.MipLevelCount,
            BaseArrayLayer = 0,
            ArrayLayerCount = 1,
            Aspect = isDepthFormat ? TextureAspect.DepthOnly : TextureAspect.All,
        };
        var view = Wgpu.TextureCreateView(wgpuTexture.NativePtr, in viewDesc);
        Console.WriteLine($"[WebGPU DEBUG] CreateTextureView: fmt={descriptor.Format} -> mapped={mappedFormat}, isDepth={isDepthFormat}, viewPtr={(nint)view}, texPtr={(nint)wgpuTexture.NativePtr}");
        return new WebGPUTextureView(Wgpu, view);
    }

    public ISampler CreateSampler(SamplerDescriptor descriptor)
    {
        var desc = new Silk.NET.WebGPU.SamplerDescriptor
        {
            MinFilter = (Silk.NET.WebGPU.FilterMode)MapFilterMode(descriptor.MinFilter),
            MagFilter = (Silk.NET.WebGPU.FilterMode)MapFilterMode(descriptor.MagFilter),
            MipmapFilter = (MipmapFilterMode)MapMipmapFilter(descriptor.MipFilter),
            AddressModeU = MapWrapMode(descriptor.AddressModeU),
            AddressModeV = MapWrapMode(descriptor.AddressModeV),
            AddressModeW = MapWrapMode(descriptor.AddressModeW),
            MaxAnisotropy = 1,
        };
        if (descriptor.Compare)
        {
            desc.Compare = MapCompareFunction(descriptor.CompareFunction);
        }
        var sampler = Wgpu.DeviceCreateSampler(Device, in desc);
        return new WebGPUSampler(Wgpu, sampler);
    }

    public IBuffer CreateBuffer(RgBufferDescriptor descriptor)
    {
        var desc = new Silk.NET.WebGPU.BufferDescriptor
        {
            Size = descriptor.Size,
            Usage = MapBufferUsage(descriptor.Usage),
            MappedAtCreation = false,
        };
        var buf = Wgpu.DeviceCreateBuffer(Device, in desc);
        return new WebGPUBuffer(Wgpu, Device, buf, descriptor.Size);
    }

    public IShaderModule CreateShaderModule(string source, string entryPoint)
    {
        var codePtr = SilkMarshal.StringToPtr(source);
        var wgslDesc = new ShaderModuleWGSLDescriptor
        {
            Code = (byte*)codePtr,
            Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor }
        };
        var desc = new Silk.NET.WebGPU.ShaderModuleDescriptor
        {
            NextInChain = (ChainedStruct*)&wgslDesc,
        };
        var module = Wgpu.DeviceCreateShaderModule(Device, in desc);
        SilkMarshal.Free((nint)codePtr);
        return new WebGPUShaderModule(Wgpu, module, entryPoint);
    }

    public IComputeShaderModule CreateComputeShaderModule(string source, string entryPoint)
    {
        var codePtr = SilkMarshal.StringToPtr(source);
        var wgslDesc = new ShaderModuleWGSLDescriptor
        {
            Code = (byte*)codePtr,
            Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor }
        };
        var desc = new Silk.NET.WebGPU.ShaderModuleDescriptor
        {
            NextInChain = (ChainedStruct*)&wgslDesc,
        };
        var module = Wgpu.DeviceCreateShaderModule(Device, in desc);
        SilkMarshal.Free((nint)codePtr);
        return new WebGPUComputeShaderModule(Wgpu, module, entryPoint);
    }

    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescriptor descriptor)
    {
        var vsModule = ((WebGPUShaderModule)descriptor.VertexShader).NativePtr;
        var vsEntryPoint = (byte*)SilkMarshal.StringToPtr(descriptor.VertexShader.EntryPoint);

        // Build fragment state (null for depth-only passes)
        FragmentState* fragmentPtr = null;
        FragmentState fragmentState;
        ColorTargetState colorTarget;
        BlendState* blendPtr = null;
        BlendState blendState;
        byte* fsEntryPoint = null;
        if (descriptor.FragmentShader != null)
        {
            var fsModule = ((WebGPUShaderModule)descriptor.FragmentShader).NativePtr;
            fsEntryPoint = (byte*)SilkMarshal.StringToPtr(descriptor.FragmentShader.EntryPoint);

            if (descriptor.ColorTargets.Length > 0 && descriptor.ColorTargets[0].Blend != null)
            {
                var blend = descriptor.ColorTargets[0].Blend;
                blendState = new BlendState
                {
                    Color = new BlendComponent
                    {
                        SrcFactor = MapBlendFactor(blend.Color.SrcFactor),
                        DstFactor = MapBlendFactor(blend.Color.DstFactor),
                        Operation = MapBlendOperation(blend.Color.Operation),
                    },
                    Alpha = new BlendComponent
                    {
                        SrcFactor = MapBlendFactor(blend.Alpha.SrcFactor),
                        DstFactor = MapBlendFactor(blend.Alpha.DstFactor),
                        Operation = MapBlendOperation(blend.Alpha.Operation),
                    },
                };
                blendPtr = &blendState;
            }

            colorTarget = new ColorTargetState
            {
                Format = descriptor.ColorTargets.Length > 0 ? MapPixelFormat(descriptor.ColorTargets[0].Format) : _swapchainFormat,
                Blend = blendPtr,
                WriteMask = ColorWriteMask.All,
            };

            fragmentState = new FragmentState
            {
                Module = fsModule,
                EntryPoint = fsEntryPoint,
                TargetCount = 1,
                Targets = &colorTarget,
            };
            fragmentPtr = &fragmentState;
        }

        // Build vertex buffer layouts — allocate native Silk.NET structs
        Silk.NET.WebGPU.VertexBufferLayout* vertexLayoutPtr = null;
        int vertexBufferCount = descriptor.VertexBuffers.Length;
        if (vertexBufferCount > 0)
        {
            vertexLayoutPtr = (Silk.NET.WebGPU.VertexBufferLayout*)NativeMemory.Alloc(
                (nuint)(vertexBufferCount * sizeof(Silk.NET.WebGPU.VertexBufferLayout)));
            for (int i = 0; i < vertexBufferCount; i++)
            {
                var vb = descriptor.VertexBuffers[i];
                var attrCount = vb.Attributes.Length;
                var attrs = (Silk.NET.WebGPU.VertexAttribute*)NativeMemory.Alloc(
                    (nuint)(attrCount * sizeof(Silk.NET.WebGPU.VertexAttribute)));
                for (int j = 0; j < attrCount; j++)
                {
                    attrs[j] = new Silk.NET.WebGPU.VertexAttribute
                    {
                        ShaderLocation = (uint)vb.Attributes[j].ShaderLocation,
                        Format = MapVertexFormat(vb.Attributes[j].Format),
                        Offset = vb.Attributes[j].Offset,
                    };
                }
                vertexLayoutPtr[i] = new Silk.NET.WebGPU.VertexBufferLayout
                {
                    ArrayStride = vb.ArrayStride,
                    AttributeCount = (uint)attrCount,
                    Attributes = attrs,
                };
            }
        }

        DepthStencilState* depthStencilPtr = null;
        DepthStencilState depthStencilState = default;
        if (descriptor.DepthStencil != null)
        {
            depthStencilState = new DepthStencilState
            {
                Format = MapPixelFormat(descriptor.DepthStencil.Format),
                DepthWriteEnabled = descriptor.DepthStencil.DepthWriteEnabled ? 1u : 0u,
                DepthCompare = MapCompareFunction(descriptor.DepthStencil.DepthCompare),
                StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                StencilBack = new StencilFaceState { Compare = CompareFunction.Always },
            };
            depthStencilPtr = &depthStencilState;
        }

        var pipelineDesc = new Silk.NET.WebGPU.RenderPipelineDescriptor
        {
            Vertex = new VertexState
            {
                Module = vsModule,
                EntryPoint = vsEntryPoint,
                BufferCount = (uint)vertexBufferCount,
                Buffers = vertexLayoutPtr,
            },
            Primitive = new PrimitiveState
            {
                Topology = MapTopology(descriptor.Topology),
                StripIndexFormat = IndexFormat.Undefined,
                FrontFace = FrontFace.Ccw,
                CullMode = CullMode.None,
            },
            Multisample = new MultisampleState
            {
                Count = (uint)descriptor.SampleCount,
                Mask = ~0u,
                AlphaToCoverageEnabled = false,
            },
            Fragment = fragmentPtr,
            DepthStencil = depthStencilPtr,
        };

        var pipeline = Wgpu.DeviceCreateRenderPipeline(Device, in pipelineDesc);

        // Cleanup temporary allocations
        SilkMarshal.Free((nint)vsEntryPoint);
        if (fsEntryPoint != null) SilkMarshal.Free((nint)fsEntryPoint);
        if (vertexLayoutPtr != null)
        {
            for (int i = 0; i < vertexBufferCount; i++)
            {
                NativeMemory.Free((void*)vertexLayoutPtr[i].Attributes);
            }
            NativeMemory.Free(vertexLayoutPtr);
        }

        return new WebGPUPipeline(Wgpu, pipeline);
    }

    public IComputePipeline CreateComputePipeline(IComputeShaderModule shader, string entryPoint)
    {
        var wgpuShader = ((WebGPUComputeShaderModule)shader).NativePtr;
        var entryPointPtr = (byte*)SilkMarshal.StringToPtr(entryPoint);

        var desc = new ComputePipelineDescriptor
        {
            Compute = new ProgrammableStageDescriptor
            {
                Module = wgpuShader,
                EntryPoint = entryPointPtr,
            },
        };

        var pipeline = Wgpu.DeviceCreateComputePipeline(Device, in desc);
        SilkMarshal.Free((nint)entryPointPtr);

        return new WebGPUComputePipeline(Wgpu, pipeline);
    }

    public IBindingSet CreateBindingSet(BindingSetDescriptor descriptor)
    {
        var layoutEntries = (BindGroupLayoutEntry*)NativeMemory.Alloc(
            (nuint)(descriptor.Layout.Entries.Length * sizeof(BindGroupLayoutEntry)));
        for (int i = 0; i < descriptor.Layout.Entries.Length; i++)
        {
            var entry = descriptor.Layout.Entries[i];
            var layoutEntry = new BindGroupLayoutEntry
            {
                Binding = (uint)entry.Binding,
                Visibility = MapShaderVisibility(entry.Visibility),
            };

            switch (entry.Type)
            {
                case BindingType.UniformBuffer:
                    layoutEntry.Buffer = new BufferBindingLayout
                    {
                        Type = BufferBindingType.Uniform,
                    };
                    break;
                case BindingType.Texture:
                    layoutEntry.Texture = new TextureBindingLayout
                    {
                        SampleType = TextureSampleType.Float,
                        ViewDimension = Silk.NET.WebGPU.TextureViewDimension.Dimension2D,
                    };
                    break;
                case BindingType.Sampler:
                    layoutEntry.Sampler = new SamplerBindingLayout
                    {
                        Type = SamplerBindingType.Filtering,
                    };
                    break;
                case BindingType.StorageTexture:
                    var st = descriptor.StorageTextures.First(s => s.Binding == entry.Binding);
                    layoutEntry.StorageTexture = new StorageTextureBindingLayout
                    {
                        Access = StorageTextureAccess.WriteOnly,
                        Format = MapPixelFormat(st.Format),
                        ViewDimension = Silk.NET.WebGPU.TextureViewDimension.Dimension2D,
                    };
                    break;
                case BindingType.StorageBuffer:
                    layoutEntry.Buffer = new BufferBindingLayout
                    {
                        Type = BufferBindingType.Storage,
                    };
                    break;
            }

            layoutEntries[i] = layoutEntry;
        }

        var layoutDesc = new BindGroupLayoutDescriptor
        {
            Entries = layoutEntries,
            EntryCount = (uint)descriptor.Layout.Entries.Length,
        };
        var layout = Wgpu.DeviceCreateBindGroupLayout(Device, in layoutDesc);

        int totalBindings = descriptor.UniformBuffers.Length + descriptor.Textures.Length + descriptor.Samplers.Length
            + descriptor.StorageTextures.Length + descriptor.StorageBuffers.Length;
        var bgEntries = (BindGroupEntry*)NativeMemory.Alloc(
            (nuint)(totalBindings * sizeof(BindGroupEntry)));

        int bgIndex = 0;
        foreach (var ub in descriptor.UniformBuffers)
        {
            bgEntries[bgIndex++] = new BindGroupEntry
            {
                Binding = (uint)ub.Binding,
                Buffer = ((WebGPUBuffer)ub.Buffer).NativePtr,
                Offset = 0,
                Size = ub.Buffer.Size,
            };
        }
        foreach (var tex in descriptor.Textures)
        {
            bgEntries[bgIndex++] = new BindGroupEntry
            {
                Binding = (uint)tex.Binding,
                TextureView = ((WebGPUTextureView)tex.TextureView).NativePtr,
            };
        }
        foreach (var smp in descriptor.Samplers)
        {
            bgEntries[bgIndex++] = new BindGroupEntry
            {
                Binding = (uint)smp.Binding,
                Sampler = ((WebGPUSampler)smp.Sampler).NativePtr,
            };
        }
        foreach (var st in descriptor.StorageTextures)
        {
            bgEntries[bgIndex++] = new BindGroupEntry
            {
                Binding = (uint)st.Binding,
                TextureView = ((WebGPUTextureView)st.TextureView).NativePtr,
            };
        }
        foreach (var sb in descriptor.StorageBuffers)
        {
            bgEntries[bgIndex++] = new BindGroupEntry
            {
                Binding = (uint)sb.Binding,
                Buffer = ((WebGPUBuffer)sb.Buffer).NativePtr,
                Offset = 0,
                Size = sb.Buffer.Size,
            };
        }

        var bgDesc = new BindGroupDescriptor
        {
            Layout = layout,
            Entries = bgEntries,
            EntryCount = (uint)totalBindings,
        };
        var bindGroup = Wgpu.DeviceCreateBindGroup(Device, in bgDesc);

        NativeMemory.Free(layoutEntries);
        NativeMemory.Free(bgEntries);
        Wgpu.BindGroupLayoutRelease(layout);

        return new WebGPUBindingSet(Wgpu, bindGroup);
    }

    public IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, UniformBufferBinding[] uniformBuffers)
    {
        var wgpuPipeline = ((WebGPUPipeline)pipeline).NativePtr;
        var layout = Wgpu.RenderPipelineGetBindGroupLayout(wgpuPipeline, (uint)groupIndex);

        var bgEntries = (BindGroupEntry*)NativeMemory.Alloc(
            (nuint)(uniformBuffers.Length * sizeof(BindGroupEntry)));
        for (int i = 0; i < uniformBuffers.Length; i++)
        {
            bgEntries[i] = new BindGroupEntry
            {
                Binding = (uint)uniformBuffers[i].Binding,
                Buffer = ((WebGPUBuffer)uniformBuffers[i].Buffer).NativePtr,
                Offset = 0,
                Size = uniformBuffers[i].Buffer.Size,
            };
        }

        var bgDesc = new BindGroupDescriptor
        {
            Layout = layout,
            Entries = bgEntries,
            EntryCount = (uint)uniformBuffers.Length,
        };
        var bindGroup = Wgpu.DeviceCreateBindGroup(Device, in bgDesc);

        NativeMemory.Free(bgEntries);
        Wgpu.BindGroupLayoutRelease(layout);

        return new WebGPUBindingSet(Wgpu, bindGroup);
    }

    public IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, TextureBinding[] textures, SamplerBinding[] samplers)
    {
        var wgpuPipeline = ((WebGPUPipeline)pipeline).NativePtr;
        var layout = Wgpu.RenderPipelineGetBindGroupLayout(wgpuPipeline, (uint)groupIndex);

        int totalBindings = textures.Length + samplers.Length;
        var bgEntries = (BindGroupEntry*)NativeMemory.Alloc((nuint)(totalBindings * sizeof(BindGroupEntry)));

        int idx = 0;
        foreach (var t in textures)
        {
            bgEntries[idx++] = new BindGroupEntry
            {
                Binding = (uint)t.Binding,
                TextureView = ((WebGPUTextureView)t.TextureView).NativePtr,
            };
        }
        foreach (var s in samplers)
        {
            bgEntries[idx++] = new BindGroupEntry
            {
                Binding = (uint)s.Binding,
                Sampler = ((WebGPUSampler)s.Sampler).NativePtr,
            };
        }

        var bgDesc = new BindGroupDescriptor
        {
            Layout = layout,
            Entries = bgEntries,
            EntryCount = (uint)totalBindings,
        };
        var bindGroup = Wgpu.DeviceCreateBindGroup(Device, in bgDesc);
        NativeMemory.Free(bgEntries);
        Wgpu.BindGroupLayoutRelease(layout);

        return new WebGPUBindingSet(Wgpu, bindGroup);
    }

    public IBindingSet CreateBindingSetForPipeline(IRenderPipeline pipeline, int groupIndex, UniformBufferBinding[] uniformBuffers, TextureBinding[] textures, SamplerBinding[] samplers)
    {
        var wgpuPipeline = ((WebGPUPipeline)pipeline).NativePtr;
        var layout = Wgpu.RenderPipelineGetBindGroupLayout(wgpuPipeline, (uint)groupIndex);

        int totalBindings = uniformBuffers.Length + textures.Length + samplers.Length;
        var bgEntries = (BindGroupEntry*)NativeMemory.Alloc((nuint)(totalBindings * sizeof(BindGroupEntry)));

        int idx = 0;
        foreach (var buf in uniformBuffers)
        {
            bgEntries[idx] = new BindGroupEntry
            {
                Binding = (uint)buf.Binding,
                Buffer = ((WebGPUBuffer)buf.Buffer).NativePtr,
                Offset = 0,
                Size = buf.Buffer.Size,
            };
            idx++;
        }
        foreach (var t in textures)
        {
            bgEntries[idx++] = new BindGroupEntry
            {
                Binding = (uint)t.Binding,
                TextureView = ((WebGPUTextureView)t.TextureView).NativePtr,
            };
        }
        foreach (var s in samplers)
        {
            bgEntries[idx++] = new BindGroupEntry
            {
                Binding = (uint)s.Binding,
                Sampler = ((WebGPUSampler)s.Sampler).NativePtr,
            };
        }

        var bgDesc = new BindGroupDescriptor
        {
            Layout = layout,
            Entries = bgEntries,
            EntryCount = (uint)totalBindings,
        };
        var bindGroup = Wgpu.DeviceCreateBindGroup(Device, in bgDesc);
        NativeMemory.Free(bgEntries);
        Wgpu.BindGroupLayoutRelease(layout);

        return new WebGPUBindingSet(Wgpu, bindGroup);
    }

    public IRenderPipeline CreateRenderPipelineWithDynamicUniforms(RenderPipelineDescriptor descriptor, nuint minBindingSize, int groupIndex = 0, int bindGroupCount = 1)
    {
        // Step 1: Build native descriptor (same as CreateRenderPipeline)
        var vsModule = ((WebGPUShaderModule)descriptor.VertexShader).NativePtr;
        var vsEntryPoint = (byte*)SilkMarshal.StringToPtr(descriptor.VertexShader.EntryPoint);

        FragmentState* fragmentPtr = null;
        FragmentState fragmentState;
        ColorTargetState colorTarget;
        BlendState* blendPtr = null;
        BlendState blendState;
        byte* fsEntryPoint = null;
        if (descriptor.FragmentShader != null)
        {
            var fsModule = ((WebGPUShaderModule)descriptor.FragmentShader).NativePtr;
            fsEntryPoint = (byte*)SilkMarshal.StringToPtr(descriptor.FragmentShader.EntryPoint);

            if (descriptor.ColorTargets.Length > 0 && descriptor.ColorTargets[0].Blend != null)
            {
                var blend = descriptor.ColorTargets[0].Blend;
                blendState = new BlendState
                {
                    Color = new BlendComponent
                    {
                        SrcFactor = MapBlendFactor(blend.Color.SrcFactor),
                        DstFactor = MapBlendFactor(blend.Color.DstFactor),
                        Operation = MapBlendOperation(blend.Color.Operation),
                    },
                    Alpha = new BlendComponent
                    {
                        SrcFactor = MapBlendFactor(blend.Alpha.SrcFactor),
                        DstFactor = MapBlendFactor(blend.Alpha.DstFactor),
                        Operation = MapBlendOperation(blend.Alpha.Operation),
                    },
                };
                blendPtr = &blendState;
            }

            colorTarget = new ColorTargetState
            {
                Format = descriptor.ColorTargets.Length > 0 ? MapPixelFormat(descriptor.ColorTargets[0].Format) : _swapchainFormat,
                Blend = blendPtr,
                WriteMask = ColorWriteMask.All,
            };

            fragmentState = new FragmentState
            {
                Module = fsModule,
                EntryPoint = fsEntryPoint,
                TargetCount = 1,
                Targets = &colorTarget,
            };
            fragmentPtr = &fragmentState;
        }

        Silk.NET.WebGPU.VertexBufferLayout* vertexLayoutPtr = null;
        int vertexBufferCount = descriptor.VertexBuffers.Length;
        if (vertexBufferCount > 0)
        {
            vertexLayoutPtr = (Silk.NET.WebGPU.VertexBufferLayout*)NativeMemory.Alloc(
                (nuint)(vertexBufferCount * sizeof(Silk.NET.WebGPU.VertexBufferLayout)));
            for (int i = 0; i < vertexBufferCount; i++)
            {
                var vb = descriptor.VertexBuffers[i];
                var attrCount = vb.Attributes.Length;
                var attrs = (Silk.NET.WebGPU.VertexAttribute*)NativeMemory.Alloc(
                    (nuint)(attrCount * sizeof(Silk.NET.WebGPU.VertexAttribute)));
                for (int j = 0; j < attrCount; j++)
                {
                    attrs[j] = new Silk.NET.WebGPU.VertexAttribute
                    {
                        ShaderLocation = (uint)vb.Attributes[j].ShaderLocation,
                        Format = MapVertexFormat(vb.Attributes[j].Format),
                        Offset = vb.Attributes[j].Offset,
                    };
                }
                vertexLayoutPtr[i] = new Silk.NET.WebGPU.VertexBufferLayout
                {
                    ArrayStride = vb.ArrayStride,
                    AttributeCount = (uint)attrCount,
                    Attributes = attrs,
                };
            }
        }

        DepthStencilState* depthStencilPtr2 = null;
        DepthStencilState depthStencilState2 = default;
        if (descriptor.DepthStencil != null)
        {
            depthStencilState2 = new DepthStencilState
            {
                Format = MapPixelFormat(descriptor.DepthStencil.Format),
                DepthWriteEnabled = descriptor.DepthStencil.DepthWriteEnabled ? 1u : 0u,
                DepthCompare = MapCompareFunction(descriptor.DepthStencil.DepthCompare),
                StencilFront = new StencilFaceState { Compare = CompareFunction.Always },
                StencilBack = new StencilFaceState { Compare = CompareFunction.Always },
            };
            depthStencilPtr2 = &depthStencilState2;
        }

        var pipelineDesc = new Silk.NET.WebGPU.RenderPipelineDescriptor
        {
            Layout = default, // inferred layout for temp pipeline
            Vertex = new VertexState
            {
                Module = vsModule,
                EntryPoint = vsEntryPoint,
                BufferCount = (uint)vertexBufferCount,
                Buffers = vertexLayoutPtr,
            },
            Primitive = new PrimitiveState
            {
                Topology = MapTopology(descriptor.Topology),
                StripIndexFormat = IndexFormat.Undefined,
                FrontFace = FrontFace.Ccw,
                CullMode = CullMode.None,
            },
            Multisample = new MultisampleState
            {
                Count = (uint)descriptor.SampleCount,
                Mask = ~0u,
                AlphaToCoverageEnabled = false,
            },
            Fragment = fragmentPtr,
            DepthStencil = depthStencilPtr2,
        };

        // Step 2: Create temporary pipeline to infer bind group layouts
        var tempPipeline = Wgpu.DeviceCreateRenderPipeline(Device, in pipelineDesc);

        // Step 3: Get inferred bind group layouts
        var bgls = stackalloc BindGroupLayout*[bindGroupCount];
        for (int i = 0; i < bindGroupCount; i++)
        {
            bgls[i] = Wgpu.RenderPipelineGetBindGroupLayout(tempPipeline, (uint)i);
        }

        // Step 4: Create dynamic bind group layout for target group
        var dynamicBglEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = Silk.NET.WebGPU.ShaderStage.Vertex | Silk.NET.WebGPU.ShaderStage.Fragment,
            Buffer = new BufferBindingLayout
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = true,
                MinBindingSize = minBindingSize,
            },
        };
        var dynamicBglDesc = new BindGroupLayoutDescriptor
        {
            Entries = &dynamicBglEntry,
            EntryCount = 1,
        };
        var dynamicBgl = Wgpu.DeviceCreateBindGroupLayout(Device, in dynamicBglDesc);

        // Step 5: Build explicit pipeline layout
        var layoutBgls = stackalloc BindGroupLayout*[bindGroupCount];
        for (int i = 0; i < bindGroupCount; i++)
        {
            layoutBgls[i] = i == groupIndex ? dynamicBgl : bgls[i];
        }

        var plDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayouts = layoutBgls,
            BindGroupLayoutCount = (uint)bindGroupCount,
        };
        var pipelineLayout = Wgpu.DeviceCreatePipelineLayout(Device, in plDesc);

        // Step 6: Create final pipeline with explicit layout
        pipelineDesc.Layout = pipelineLayout;
        var pipeline = Wgpu.DeviceCreateRenderPipeline(Device, in pipelineDesc);

        // Cleanup
        SilkMarshal.Free((nint)vsEntryPoint);
        if (fsEntryPoint != null) SilkMarshal.Free((nint)fsEntryPoint);
        if (vertexLayoutPtr != null)
        {
            for (int i = 0; i < vertexBufferCount; i++)
                NativeMemory.Free((void*)vertexLayoutPtr[i].Attributes);
            NativeMemory.Free(vertexLayoutPtr);
        }

        Wgpu.RenderPipelineRelease(tempPipeline);
        Wgpu.PipelineLayoutRelease(pipelineLayout);
        Wgpu.BindGroupLayoutRelease(dynamicBgl);
        for (int i = 0; i < bindGroupCount; i++)
            Wgpu.BindGroupLayoutRelease(bgls[i]);

        return new WebGPUPipeline(Wgpu, pipeline);
    }

    public IBindingSet CreateDynamicUniformBindingSet(IRenderPipeline pipeline, int groupIndex, IBuffer uniformBuffer, nuint bindingSize)
    {
        var wgpuPipeline = ((WebGPUPipeline)pipeline).NativePtr;
        var layout = Wgpu.RenderPipelineGetBindGroupLayout(wgpuPipeline, (uint)groupIndex);

        var bgEntry = new BindGroupEntry
        {
            Binding = 0,
            Buffer = ((WebGPUBuffer)uniformBuffer).NativePtr,
            Offset = 0,
            Size = bindingSize,
        };

        var bgDesc = new BindGroupDescriptor
        {
            Layout = layout,
            Entries = &bgEntry,
            EntryCount = 1,
        };
        var bindGroup = Wgpu.DeviceCreateBindGroup(Device, in bgDesc);
        Wgpu.BindGroupLayoutRelease(layout);

        return new WebGPUBindingSet(Wgpu, bindGroup) { HasDynamicOffsets = true };
    }

    public IBindingSet CreateBindingSetForComputePipeline(IComputePipeline pipeline, int groupIndex, TextureBinding[] textures, StorageTextureBinding[] storageTextures)
    {
        var wgpuPipeline = ((WebGPUComputePipeline)pipeline).NativePtr;
        var layout = Wgpu.ComputePipelineGetBindGroupLayout(wgpuPipeline, (uint)groupIndex);

        int totalBindings = textures.Length + storageTextures.Length;
        var bgEntries = (BindGroupEntry*)NativeMemory.Alloc((nuint)(totalBindings * sizeof(BindGroupEntry)));

        int idx = 0;
        foreach (var t in textures)
        {
            bgEntries[idx++] = new BindGroupEntry
            {
                Binding = (uint)t.Binding,
                TextureView = ((WebGPUTextureView)t.TextureView).NativePtr,
            };
        }
        foreach (var st in storageTextures)
        {
            bgEntries[idx++] = new BindGroupEntry
            {
                Binding = (uint)st.Binding,
                TextureView = ((WebGPUTextureView)st.TextureView).NativePtr,
            };
        }

        var bgDesc = new BindGroupDescriptor
        {
            Layout = layout,
            Entries = bgEntries,
            EntryCount = (uint)totalBindings,
        };
        var bindGroup = Wgpu.DeviceCreateBindGroup(Device, in bgDesc);
        NativeMemory.Free(bgEntries);
        Wgpu.BindGroupLayoutRelease(layout);

        return new WebGPUBindingSet(Wgpu, bindGroup);
    }

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
            Usage = Silk.NET.WebGPU.TextureUsage.RenderAttachment,
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

    // --- Enum mappings ---

    private static TextureFormat MapPixelFormat(DriverPixelFormat format) => format switch
    {
        DriverPixelFormat.BGRA8Unorm => TextureFormat.Bgra8Unorm,
        DriverPixelFormat.BGRA8UnormSrgb => TextureFormat.Bgra8UnormSrgb,
        DriverPixelFormat.RGBA8Unorm => TextureFormat.Rgba8Unorm,
        DriverPixelFormat.Depth24Plus => TextureFormat.Depth24Plus,
        DriverPixelFormat.Depth24PlusStencil8 => TextureFormat.Depth24PlusStencil8,
        DriverPixelFormat.Depth32Float => TextureFormat.Depth24Plus,
        _ => TextureFormat.Bgra8Unorm,
    };

    private static Silk.NET.WebGPU.TextureViewDimension MapTextureViewDimension(Kilo.Rendering.Driver.TextureViewDimension dimension) => dimension switch
    {
        Kilo.Rendering.Driver.TextureViewDimension.View1D => Silk.NET.WebGPU.TextureViewDimension.Dimension1D,
        Kilo.Rendering.Driver.TextureViewDimension.View2D => Silk.NET.WebGPU.TextureViewDimension.Dimension2D,
        Kilo.Rendering.Driver.TextureViewDimension.View2DArray => Silk.NET.WebGPU.TextureViewDimension.Dimension2DArray,
        Kilo.Rendering.Driver.TextureViewDimension.View3D => Silk.NET.WebGPU.TextureViewDimension.Dimension3D,
        _ => Silk.NET.WebGPU.TextureViewDimension.Dimension2D,
    };

    private static Silk.NET.WebGPU.FilterMode MapFilterMode(Kilo.Rendering.Driver.FilterMode mode) => mode switch
    {
        Kilo.Rendering.Driver.FilterMode.Nearest => Silk.NET.WebGPU.FilterMode.Nearest,
        Kilo.Rendering.Driver.FilterMode.Linear => Silk.NET.WebGPU.FilterMode.Linear,
        _ => Silk.NET.WebGPU.FilterMode.Linear,
    };

    private static MipmapFilterMode MapMipmapFilter(Kilo.Rendering.Driver.FilterMode mode) => mode switch
    {
        Kilo.Rendering.Driver.FilterMode.Nearest => MipmapFilterMode.Nearest,
        Kilo.Rendering.Driver.FilterMode.Linear => MipmapFilterMode.Linear,
        _ => MipmapFilterMode.Linear,
    };

    private static Silk.NET.WebGPU.AddressMode MapWrapMode(WrapMode mode) => mode switch
    {
        WrapMode.ClampToEdge => Silk.NET.WebGPU.AddressMode.ClampToEdge,
        WrapMode.Repeat => Silk.NET.WebGPU.AddressMode.Repeat,
        WrapMode.MirrorRepeat => Silk.NET.WebGPU.AddressMode.MirrorRepeat,
        _ => Silk.NET.WebGPU.AddressMode.ClampToEdge,
    };

    private static Silk.NET.WebGPU.TextureUsage MapTextureUsage(RgTextureUsage usage)
    {
        Silk.NET.WebGPU.TextureUsage result = 0;
        if (usage.HasFlag(RgTextureUsage.RenderAttachment)) result |= Silk.NET.WebGPU.TextureUsage.RenderAttachment;
        if (usage.HasFlag(RgTextureUsage.ShaderBinding)) result |= Silk.NET.WebGPU.TextureUsage.TextureBinding;
        if (usage.HasFlag(RgTextureUsage.CopyDst)) result |= Silk.NET.WebGPU.TextureUsage.CopyDst;
        if (usage.HasFlag(RgTextureUsage.CopySrc)) result |= Silk.NET.WebGPU.TextureUsage.CopySrc;
        if (usage.HasFlag(RgTextureUsage.Storage)) result |= Silk.NET.WebGPU.TextureUsage.StorageBinding;
        return result;
    }

    private static Silk.NET.WebGPU.BufferUsage MapBufferUsage(RgBufferUsage usage)
    {
        Silk.NET.WebGPU.BufferUsage result = 0;
        if (usage.HasFlag(RgBufferUsage.Vertex)) result |= Silk.NET.WebGPU.BufferUsage.Vertex;
        if (usage.HasFlag(RgBufferUsage.Index)) result |= Silk.NET.WebGPU.BufferUsage.Index;
        if (usage.HasFlag(RgBufferUsage.Uniform)) result |= Silk.NET.WebGPU.BufferUsage.Uniform;
        if (usage.HasFlag(RgBufferUsage.CopyDst)) result |= Silk.NET.WebGPU.BufferUsage.CopyDst;
        if (usage.HasFlag(RgBufferUsage.Storage)) result |= Silk.NET.WebGPU.BufferUsage.Storage;
        if (usage.HasFlag(RgBufferUsage.MapRead)) result |= Silk.NET.WebGPU.BufferUsage.MapRead;
        return result;
    }

    private static BlendFactor MapBlendFactor(DriverBlendFactor factor) => factor switch
    {
        DriverBlendFactor.Zero => BlendFactor.Zero,
        DriverBlendFactor.One => BlendFactor.One,
        DriverBlendFactor.SrcColor => BlendFactor.Src,
        DriverBlendFactor.OneMinusSrcColor => BlendFactor.OneMinusSrc,
        DriverBlendFactor.SrcAlpha => BlendFactor.SrcAlpha,
        DriverBlendFactor.OneMinusSrcAlpha => BlendFactor.OneMinusSrcAlpha,
        DriverBlendFactor.DstColor => BlendFactor.Dst,
        DriverBlendFactor.DstAlpha => BlendFactor.DstAlpha,
        _ => BlendFactor.Zero,
    };

    private static Silk.NET.WebGPU.BlendOperation MapBlendOperation(BlendOperation op) => op switch
    {
        BlendOperation.Add => Silk.NET.WebGPU.BlendOperation.Add,
        BlendOperation.Subtract => Silk.NET.WebGPU.BlendOperation.Subtract,
        BlendOperation.ReverseSubtract => Silk.NET.WebGPU.BlendOperation.ReverseSubtract,
        BlendOperation.Min => Silk.NET.WebGPU.BlendOperation.Min,
        BlendOperation.Max => Silk.NET.WebGPU.BlendOperation.Max,
        _ => Silk.NET.WebGPU.BlendOperation.Add,
    };

    private static PrimitiveTopology MapTopology(DriverPrimitiveTopology topology) => topology switch
    {
        DriverPrimitiveTopology.TriangleList => PrimitiveTopology.TriangleList,
        DriverPrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
        DriverPrimitiveTopology.LineList => PrimitiveTopology.LineList,
        DriverPrimitiveTopology.LineStrip => PrimitiveTopology.LineStrip,
        DriverPrimitiveTopology.PointList => PrimitiveTopology.PointList,
        _ => PrimitiveTopology.TriangleList,
    };

    private static Silk.NET.WebGPU.VertexFormat MapVertexFormat(VertexFormat format) => format switch
    {
        VertexFormat.Float32x2 => Silk.NET.WebGPU.VertexFormat.Float32x2,
        VertexFormat.Float32x3 => Silk.NET.WebGPU.VertexFormat.Float32x3,
        VertexFormat.Float32x4 => Silk.NET.WebGPU.VertexFormat.Float32x4,
        VertexFormat.UInt32 => Silk.NET.WebGPU.VertexFormat.Uint32,
        VertexFormat.UInt32x4 => Silk.NET.WebGPU.VertexFormat.Uint32x4,
        _ => Silk.NET.WebGPU.VertexFormat.Float32x2,
    };

    private static Silk.NET.WebGPU.ShaderStage MapShaderVisibility(ShaderVisibility visibility)
    {
        Silk.NET.WebGPU.ShaderStage result = 0;
        if (visibility.HasFlag(ShaderVisibility.Vertex)) result |= Silk.NET.WebGPU.ShaderStage.Vertex;
        if (visibility.HasFlag(ShaderVisibility.Fragment)) result |= Silk.NET.WebGPU.ShaderStage.Fragment;
        if (visibility.HasFlag(ShaderVisibility.Compute)) result |= Silk.NET.WebGPU.ShaderStage.Compute;
        return result;
    }

    private static CompareFunction MapCompareFunction(DriverCompareFunction func) => func switch
    {
        DriverCompareFunction.Never => CompareFunction.Never,
        DriverCompareFunction.Less => CompareFunction.Less,
        DriverCompareFunction.Equal => CompareFunction.Equal,
        DriverCompareFunction.LessEqual => CompareFunction.LessEqual,
        DriverCompareFunction.Greater => CompareFunction.Greater,
        DriverCompareFunction.NotEqual => CompareFunction.NotEqual,
        DriverCompareFunction.GreaterEqual => CompareFunction.GreaterEqual,
        DriverCompareFunction.Always => CompareFunction.Always,
        _ => CompareFunction.Less,
    };

    // --- Readback ---

    private static readonly System.Threading.ManualResetEventSlim s_readbackDone = new(false);
    private static byte[]? s_readbackResult;
    private static string? s_readbackError;
    private static WgpuApi? s_cbWgpu;
    private static Silk.NET.WebGPU.Buffer* s_cbBuffer;
    private static nuint s_cbOffset;
    private static nuint s_cbSize;

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static void OnBufferMapped(BufferMapAsyncStatus status, void* message)
    {
        if (status == BufferMapAsyncStatus.Success)
        {
            var mapped = s_cbWgpu!.BufferGetMappedRange(s_cbBuffer, s_cbOffset, s_cbSize);
            s_readbackResult = new byte[(int)s_cbSize];
            Marshal.Copy((nint)mapped, s_readbackResult, 0, (int)s_cbSize);
            s_cbWgpu.BufferUnmap(s_cbBuffer);
        }
        else
        {
            s_readbackError = $"MapAsync failed with status {status}";
        }
        s_readbackDone.Set();
    }

    public byte[] ReadBufferSync(IBuffer buffer, nuint offset, nuint size)
    {
        var wgpuBuf = ((WebGPUBuffer)buffer).NativePtr;

        s_readbackDone.Reset();
        s_readbackResult = null;
        s_readbackError = null;
        s_cbWgpu = Wgpu;
        s_cbBuffer = wgpuBuf;
        s_cbOffset = offset;
        s_cbSize = size;

        delegate* unmanaged[Cdecl]<BufferMapAsyncStatus, void*, void> cb = &OnBufferMapped;
        Wgpu.BufferMapAsync(wgpuBuf, MapMode.Read, offset, size, new PfnBufferMapCallback(cb), null);

        // Pump event loop until callback fires
        while (!s_readbackDone.Wait(0))
        {
            Wgpu.InstanceProcessEvents(Instance);
        }

        if (s_readbackError != null)
            throw new InvalidOperationException($"Buffer readback failed: {s_readbackError}");

        return s_readbackResult!;
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
