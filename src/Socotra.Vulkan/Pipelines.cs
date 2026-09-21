using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

internal enum Shader
{
    Box,
    Filter,
    Backdrop,
    DropShadow,
    BorderWrap,
    Downsample,
}

internal sealed unsafe class Pipelines : IDisposable
{
    private readonly VulkanDevice _vulkan;
    private readonly PipelineLayout _layout;
    private readonly ShaderModule[] _modules;
    private readonly ShaderModule _textRasterModule;
    private readonly Dictionary<(Shader Shader, BlendMode Blend, Format Format), Pipeline> _pipelines = [];

    public Pipelines(VulkanDevice vulkan, PipelineLayout layout, PipelineLayout textRasterLayout)
    {
        _vulkan = vulkan;
        _layout = layout;
        _modules = [.. Enum.GetValues<Shader>().Select(shader => LoadModule($"UI.{shader}.spv"))];
        _textRasterModule = LoadModule("UI.TextRaster.spv");
        TextRaster = CreateCompute(_textRasterModule, textRasterLayout);
    }

    public Pipeline TextRaster { get; }

    public Pipeline Get(Shader shader, BlendMode blend, Format format)
    {
        if (!_pipelines.TryGetValue((shader, blend, format), out var pipeline))
        {
            pipeline = Create(shader, blend, format);
            _pipelines.Add((shader, blend, format), pipeline);
        }

        return pipeline;
    }

    public void Dispose()
    {
        foreach (var pipeline in _pipelines.Values)
        {
            _vulkan.Api.DestroyPipeline(_vulkan.Device, pipeline, null);
        }

        foreach (var module in _modules)
        {
            _vulkan.Api.DestroyShaderModule(_vulkan.Device, module, null);
        }

        _vulkan.Api.DestroyPipeline(_vulkan.Device, TextRaster, null);
        _vulkan.Api.DestroyShaderModule(_vulkan.Device, _textRasterModule, null);
    }

    private static PipelineColorBlendAttachmentState BlendState(Shader shader, BlendMode blend)
    {
        var state = new PipelineColorBlendAttachmentState
        {
            BlendEnable = shader != Shader.Downsample,
            ColorBlendOp = BlendOp.Add,
            AlphaBlendOp = BlendOp.Add,
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
        };
        (state.SrcColorBlendFactor, state.DstColorBlendFactor, state.SrcAlphaBlendFactor, state.DstAlphaBlendFactor) = blend switch
        {
            BlendMode.Multiply => (BlendFactor.DstColor, BlendFactor.Zero, BlendFactor.One, BlendFactor.Zero),
            BlendMode.Lighten => (BlendFactor.SrcAlpha, BlendFactor.One, BlendFactor.One, BlendFactor.One),
            BlendMode.PremultipliedAlpha => (BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
            _ => (BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
        };
        return state;
    }

    private ShaderModule LoadModule(string name)
    {
        using var stream = typeof(Pipelines).Assembly.GetManifestResourceStream(name)!;
        var code = new byte[stream.Length];
        stream.ReadExactly(code);
        fixed (byte* pointer = code)
        {
            var info = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length, PCode = (uint*)pointer };
            VulkanDevice.Check(_vulkan.Api.CreateShaderModule(_vulkan.Device, in info, null, out var module), "vkCreateShaderModule");
            return module;
        }
    }

    private Pipeline CreateCompute(ShaderModule module, PipelineLayout layout)
    {
        fixed (byte* entry = "MainCs\0"u8)
        {
            var info = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = module, PName = entry },
                Layout = layout,
            };
            VulkanDevice.Check(_vulkan.Api.CreateComputePipelines(_vulkan.Device, default, 1, in info, null, out var pipeline), "vkCreateComputePipelines");
            return pipeline;
        }
    }

    private Pipeline Create(Shader shader, BlendMode blend, Format format)
    {
        var blendMode = (int)blend;
        var specialization = new SpecializationMapEntry(0, 0, sizeof(int));
        var specializationInfo = new SpecializationInfo(1, &specialization, sizeof(int), &blendMode);
        fixed (byte* vertexEntry = "VertexMain\0"u8)
        fixed (byte* fragmentEntry = "FragmentMain\0"u8)
        {
            var stages = stackalloc PipelineShaderStageCreateInfo[]
            {
                new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = _modules[(int)shader], PName = vertexEntry },
                new()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = _modules[(int)shader],
                    PName = fragmentEntry,
                    PSpecializationInfo = &specializationInfo,
                },
            };
            var vertexInput = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo };
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = shader == Shader.Downsample ? PrimitiveTopology.TriangleList : PrimitiveTopology.TriangleStrip,
            };
            var viewportState = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, ScissorCount = 1 };
            var rasterization = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1,
            };
            var multisample = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
            var attachment = BlendState(shader, blend);
            var colorBlend = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &attachment };
            var dynamicStates = stackalloc DynamicState[] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamicState = new PipelineDynamicStateCreateInfo { SType = StructureType.PipelineDynamicStateCreateInfo, DynamicStateCount = 2, PDynamicStates = dynamicStates };
            var rendering = new PipelineRenderingCreateInfo { SType = StructureType.PipelineRenderingCreateInfo, ColorAttachmentCount = 1, PColorAttachmentFormats = &format };
            var info = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &rendering,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterization,
                PMultisampleState = &multisample,
                PColorBlendState = &colorBlend,
                PDynamicState = &dynamicState,
                Layout = _layout,
            };
            VulkanDevice.Check(_vulkan.Api.CreateGraphicsPipelines(_vulkan.Device, default, 1, in info, null, out var pipeline), "vkCreateGraphicsPipelines");
            return pipeline;
        }
    }
}
