using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace Kilo.Rendering.Driver.WebGPUImpl;

using WgpuApi = Silk.NET.WebGPU.WebGPU;
using RgTextureDescriptor = Kilo.Rendering.RenderGraph.TextureDescriptor;
using RgBufferDescriptor = Kilo.Rendering.RenderGraph.BufferDescriptor;

public sealed unsafe partial class WebGPURenderDriver
{
    // --- Resource creation ---

    public ITexture CreateTexture(RgTextureDescriptor descriptor)
    {
        bool isCube = descriptor.DepthOrArrayLayers == 6;
        var desc = new Silk.NET.WebGPU.TextureDescriptor
        {
            Size = new Extent3D { Width = (uint)descriptor.Width, Height = (uint)descriptor.Height, DepthOrArrayLayers = (uint)descriptor.DepthOrArrayLayers },
            MipLevelCount = (uint)descriptor.MipLevelCount,
            SampleCount = (uint)descriptor.SampleCount,
            Dimension = isCube ? TextureDimension.Dimension2D : TextureDimension.Dimension2D,
            Format = WebGPUMappings.MapPixelFormat(descriptor.Format),
            Usage = WebGPUMappings.MapTextureUsage(descriptor.Usage),
        };
        var tex = Wgpu.DeviceCreateTexture(Device, in desc);
        return new WebGPUTexture(Wgpu, Device, tex, descriptor.Width, descriptor.Height, descriptor.Format);
    }

    public ITextureView CreateTextureView(ITexture texture, TextureViewDescriptor descriptor)
    {
        var wgpuTexture = (WebGPUTexture)texture;
        var mappedFormat = WebGPUMappings.MapPixelFormat(descriptor.Format);
        var isDepthFormat = descriptor.Format == DriverPixelFormat.Depth24Plus
                         || descriptor.Format == DriverPixelFormat.Depth24PlusStencil8
                         || descriptor.Format == DriverPixelFormat.Depth32Float;
        var viewDesc = new Silk.NET.WebGPU.TextureViewDescriptor
        {
            Format = mappedFormat,
            Dimension = WebGPUMappings.MapTextureViewDimension(descriptor.Dimension),
            BaseMipLevel = (uint)descriptor.BaseMipLevel,
            MipLevelCount = (uint)descriptor.MipLevelCount,
            BaseArrayLayer = 0,
            ArrayLayerCount = descriptor.Dimension == TextureViewDimension.ViewCube ? 6u : 1u,
            Aspect = isDepthFormat ? TextureAspect.DepthOnly : TextureAspect.All,
        };
        var view = Wgpu.TextureCreateView(wgpuTexture.NativePtr, in viewDesc);
        return new WebGPUTextureView(Wgpu, view);
    }

    public ISampler CreateSampler(SamplerDescriptor descriptor)
    {
        var desc = new Silk.NET.WebGPU.SamplerDescriptor
        {
            MinFilter = (Silk.NET.WebGPU.FilterMode)WebGPUMappings.MapFilterMode(descriptor.MinFilter),
            MagFilter = (Silk.NET.WebGPU.FilterMode)WebGPUMappings.MapFilterMode(descriptor.MagFilter),
            MipmapFilter = (MipmapFilterMode)WebGPUMappings.MapMipmapFilter(descriptor.MipFilter),
            AddressModeU = WebGPUMappings.MapWrapMode(descriptor.AddressModeU),
            AddressModeV = WebGPUMappings.MapWrapMode(descriptor.AddressModeV),
            AddressModeW = WebGPUMappings.MapWrapMode(descriptor.AddressModeW),
            MaxAnisotropy = 1,
        };
        if (descriptor.Compare)
        {
            desc.Compare = WebGPUMappings.MapCompareFunction(descriptor.CompareFunction);
        }
        var sampler = Wgpu.DeviceCreateSampler(Device, in desc);
        return new WebGPUSampler(Wgpu, sampler);
    }

    public IBuffer CreateBuffer(RgBufferDescriptor descriptor)
    {
        var desc = new Silk.NET.WebGPU.BufferDescriptor
        {
            Size = descriptor.Size,
            Usage = WebGPUMappings.MapBufferUsage(descriptor.Usage),
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
                        SrcFactor = WebGPUMappings.MapBlendFactor(blend.Color.SrcFactor),
                        DstFactor = WebGPUMappings.MapBlendFactor(blend.Color.DstFactor),
                        Operation = WebGPUMappings.MapBlendOperation(blend.Color.Operation),
                    },
                    Alpha = new BlendComponent
                    {
                        SrcFactor = WebGPUMappings.MapBlendFactor(blend.Alpha.SrcFactor),
                        DstFactor = WebGPUMappings.MapBlendFactor(blend.Alpha.DstFactor),
                        Operation = WebGPUMappings.MapBlendOperation(blend.Alpha.Operation),
                    },
                };
                blendPtr = &blendState;
            }

            colorTarget = new ColorTargetState
            {
                Format = descriptor.ColorTargets.Length > 0 ? WebGPUMappings.MapPixelFormat(descriptor.ColorTargets[0].Format) : _swapchainFormat,
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
                        Format = WebGPUMappings.MapVertexFormat(vb.Attributes[j].Format),
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
                Format = WebGPUMappings.MapPixelFormat(descriptor.DepthStencil.Format),
                DepthWriteEnabled = descriptor.DepthStencil.DepthWriteEnabled ? 1u : 0u,
                DepthCompare = WebGPUMappings.MapCompareFunction(descriptor.DepthStencil.DepthCompare),
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
                Topology = WebGPUMappings.MapTopology(descriptor.Topology),
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
                Visibility = WebGPUMappings.MapShaderVisibility(entry.Visibility),
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
                        Format = WebGPUMappings.MapPixelFormat(st.Format),
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
                        SrcFactor = WebGPUMappings.MapBlendFactor(blend.Color.SrcFactor),
                        DstFactor = WebGPUMappings.MapBlendFactor(blend.Color.DstFactor),
                        Operation = WebGPUMappings.MapBlendOperation(blend.Color.Operation),
                    },
                    Alpha = new BlendComponent
                    {
                        SrcFactor = WebGPUMappings.MapBlendFactor(blend.Alpha.SrcFactor),
                        DstFactor = WebGPUMappings.MapBlendFactor(blend.Alpha.DstFactor),
                        Operation = WebGPUMappings.MapBlendOperation(blend.Alpha.Operation),
                    },
                };
                blendPtr = &blendState;
            }

            colorTarget = new ColorTargetState
            {
                Format = descriptor.ColorTargets.Length > 0 ? WebGPUMappings.MapPixelFormat(descriptor.ColorTargets[0].Format) : _swapchainFormat,
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
                        Format = WebGPUMappings.MapVertexFormat(vb.Attributes[j].Format),
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
                Format = WebGPUMappings.MapPixelFormat(descriptor.DepthStencil.Format),
                DepthWriteEnabled = descriptor.DepthStencil.DepthWriteEnabled ? 1u : 0u,
                DepthCompare = WebGPUMappings.MapCompareFunction(descriptor.DepthStencil.DepthCompare),
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
                Topology = WebGPUMappings.MapTopology(descriptor.Topology),
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
}
