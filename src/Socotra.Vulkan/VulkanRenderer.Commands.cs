using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

public sealed unsafe partial class VulkanRenderer
{
    private CommandBuffer _commandBuffer;
    private FramePass? _frame;
    private Pipeline _bound;
    private Grab _lastGrab;
    private ulong _constantOffset;

    private Surface Current => _surfaces.Peek();

    private void Execute(CommandBuffer commandBuffer, FramePass frame, DrawList list, Surface target, AttachmentLoadOp load)
    {
        _commandBuffer = commandBuffer;
        _frame = frame;
        _bound = default;
        _lastGrab = default;
        _constantOffset = 0;
        _surfaces.Clear();
        _surfaces.Push(target);
        var sets = stackalloc DescriptorSet[] { frame.TableSet, frame.TextureSet };
        _vulkan.Api.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, _pipelineLayout, 0, 2, sets, 0, null);
        Begin(target, load);
        foreach (var command in list.Commands)
        {
            switch (command)
            {
                case ClearCommand clear:
                    Clear(clear.Color);
                    break;
                case BoxesCommand boxes:
                    DrawBoxes(boxes);
                    break;
                case BeginLayerCommand begin:
                    BeginLayer(_layers[begin.Layer].Image);
                    break;
                case EndLayerCommand:
                    EndLayer();
                    break;
                case FilterCommand filter:
                    DrawQuad(Shader.Filter, filter.Placement.Blend, FilterConstants(filter));
                    break;
                case DropShadowCommand shadow:
                    DrawQuad(Shader.DropShadow, shadow.Placement.Blend, DropShadowConstants(shadow));
                    break;
                case BorderWrapCommand border:
                    DrawQuad(Shader.BorderWrap, border.Placement.Blend, BorderWrapConstants(border));
                    break;
                case BackdropCommand backdrop:
                    if (!backdrop.ReuseGrab)
                    {
                        GrabCurrent();
                    }

                    DrawQuad(Shader.Backdrop, backdrop.Placement.Blend, BackdropConstants(backdrop));
                    break;
            }
        }

        _vulkan.EndRendering(commandBuffer);
        _frame = null;
    }

    private void Begin(Surface surface, AttachmentLoadOp load)
    {
        _vulkan.BeginRendering(_commandBuffer, surface.View, surface.Size, load);
        SetViewport(surface.Size, flipped: true);
    }

    private void SetViewport(Extent2D size, bool flipped)
    {
        var viewport = flipped ? new Viewport(0, size.Height, size.Width, -(float)size.Height, 0, 1) : new Viewport(0, 0, size.Width, size.Height, 0, 1);
        var scissor = new Rect2D(default, size);
        _vulkan.Api.CmdSetViewport(_commandBuffer, 0, 1, &viewport);
        _vulkan.Api.CmdSetScissor(_commandBuffer, 0, 1, &scissor);
    }

    private void Bind(Shader shader, BlendMode blend, Format format)
    {
        var pipeline = _pipelines.Get(shader, blend, format);
        if (pipeline.Handle != _bound.Handle)
        {
            _vulkan.Api.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, pipeline);
            _bound = pipeline;
        }
    }

    private void Clear(Color color)
    {
        var attachment = new ClearAttachment
        {
            AspectMask = ImageAspectFlags.ColorBit,
            ColorAttachment = 0,
            ClearValue = new ClearValue(new ClearColorValue(color.R * color.A, color.G * color.A, color.B * color.A, color.A)),
        };
        var rect = new ClearRect(new Rect2D(default, Current.Size), 0, 1);
        _vulkan.Api.CmdClearAttachments(_commandBuffer, 1, &attachment, 1, &rect);
    }

    private void DrawBoxes(BoxesCommand boxes)
    {
        var surface = Current;
        Bind(Shader.Box, boxes.Blend, surface.Format);
        var constants = new BoxConstants { Viewport = Viewport(surface), LayerMat = boxes.LayerMatrix, InstanceOffset = boxes.Offset };
        _vulkan.Api.CmdPushConstants(_commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BoxConstants), &constants);
        _vulkan.Api.CmdDraw(_commandBuffer, 4, (uint)boxes.Count, 0, 0);
    }

    private void BeginLayer(GpuImage layer)
    {
        _vulkan.EndRendering(_commandBuffer);
        _vulkan.Barrier(_commandBuffer, layer.Image, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.TransferBit | PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit);
        var surface = new Surface(layer.Image, layer.View, layer.Size, layer.Format);
        _surfaces.Push(surface);
        Begin(surface, AttachmentLoadOp.Clear);
    }

    private void EndLayer()
    {
        _vulkan.EndRendering(_commandBuffer);
        var layer = _surfaces.Pop();
        _vulkan.Barrier(_commandBuffer, layer.Image, ImageLayout.ColorAttachmentOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit, PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);
        Begin(Current, AttachmentLoadOp.Load);
    }

    private void GrabCurrent()
    {
        var surface = Current;
        var grab = _grabs[(surface.Size.Width, surface.Size.Height, surface.Format)];
        var image = grab.Image;
        _vulkan.EndRendering(_commandBuffer);
        _vulkan.Barrier(_commandBuffer, surface.Image, ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit, PipelineStageFlags.TransferBit, AccessFlags.TransferReadBit);
        _vulkan.Barrier(_commandBuffer, image.Image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
            PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit);
        if (image.MipLevels > 1)
        {
            _vulkan.Barrier(_commandBuffer, image.Image, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit,
                PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit, 1, image.MipLevels - 1);
        }

        var copy = new ImageCopy
        {
            SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            Extent = new Extent3D(image.Width, image.Height, 1),
        };
        _vulkan.Api.CmdCopyImage(_commandBuffer, surface.Image, ImageLayout.TransferSrcOptimal, image.Image, ImageLayout.TransferDstOptimal, 1, &copy);
        _vulkan.Barrier(_commandBuffer, surface.Image, ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags.TransferBit, 0, PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit);
        _vulkan.Barrier(_commandBuffer, image.Image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit, PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit);

        for (int level = 1; level < image.MipLevels; level++)
        {
            var size = image.MipSize(level);
            _vulkan.BeginRendering(_commandBuffer, image.MipView(level), size, AttachmentLoadOp.DontCare);
            SetViewport(size, flipped: false);
            Bind(Shader.Downsample, BlendMode.Normal, image.Format);
            var constants = new DownsampleConstants { SourceTextureIndex = grab.Slot + level };
            _vulkan.Api.CmdPushConstants(_commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, (uint)sizeof(DownsampleConstants), &constants);
            _vulkan.Api.CmdDraw(_commandBuffer, 3, 1, 0, 0);
            _vulkan.EndRendering(_commandBuffer);
            _vulkan.Barrier(_commandBuffer, image.Image, ImageLayout.ColorAttachmentOptimal, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit, PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit, (uint)level);
        }

        _lastGrab = grab;
        Begin(surface, AttachmentLoadOp.Load);
    }

    private void DrawQuad<T>(Shader shader, BlendMode blend, in T constants)
        where T : unmanaged
    {
        var offset = _constantOffset;
        _constantOffset += _constantStride;
        _frame!.Constants.Write(constants, offset);
        Bind(shader, blend, Current.Format);
        var set = _frame.ConstantSet;
        var dynamicOffset = (uint)offset;
        _vulkan.Api.CmdBindDescriptorSets(_commandBuffer, PipelineBindPoint.Graphics, _pipelineLayout, 2, 1, &set, 1, &dynamicOffset);
        _vulkan.Api.CmdDraw(_commandBuffer, 4, 1, 0, 0);
    }

    private static Vector4 Viewport(Surface surface) => new(0, 0, surface.Size.Width, surface.Size.Height);

    private QuadDraw Quad(in QuadPlacement placement, Rect box, Color color, int softwareScissor) => new()
    {
        TransformMat = placement.Transform,
        LayerMat = placement.LayerMatrix,
        Viewport = Viewport(Current),
        QuadRect = new Vector4(placement.Quad.Left, placement.Quad.Top, placement.Quad.Width, placement.Quad.Height),
        QuadColor = color,
        BoxSize = box.Size,
        SoftwareScissorIndex = softwareScissor,
    };

    private FilterConstants FilterConstants(FilterCommand command)
    {
        var filter = command.Filter;
        var constants = new FilterConstants
        {
            Quad = Quad(command.Placement, command.Box, Color.White, -1),
            PainterScissorIndex = command.Placement.Scissor,
            TextureIndex = _layers[command.Layer].Slot,
            FilterBrightness = filter.Brightness,
            FilterHueRotate = filter.HueRotation,
            FilterBlur = filter.Blur,
            FilterSaturate = filter.Saturation,
            FilterSepia = filter.Sepia,
            FilterInvert = filter.Invert,
            FilterContrast = filter.Contrast,
            FilterTint = filter.Tint,
        };

        if (command.Mask is { } mask)
        {
            constants.MaskTextureIndex = _textureSlots[mask.Texture];
            constants.MaskMode = (int)mask.Mode;
            constants.MaskScope = (int)command.MaskScope;
            constants.MaskAngle = float.DegreesToRadians(mask.Rotation);
            constants.SamplerIndex = BoxInstance.BackgroundSampler(mask.Repeat, mask.Sampling);
            constants.BorderSamplerIndex = Samplers.Index(mask.Sampling, TextureAddress.Border, TextureAddress.Border);
            constants.MaskPos = new Vector4(mask.Rect.Left - command.Box.Left, mask.Rect.Top - command.Box.Top, mask.Rect.Width, mask.Rect.Height);
        }

        return constants;
    }

    private DropShadowConstants DropShadowConstants(DropShadowCommand command) => new()
    {
        Quad = Quad(command.Placement, command.Box, Color.White, command.Placement.Scissor),
        TextureIndex = _layers[command.Layer].Slot,
        FilterDropShadowOffset = command.Offset,
        FilterDropShadowBlur = command.Blur,
        FilterDropShadowColor = command.Color,
        FilterDropShadowScale = command.Box.Size / command.Placement.Quad.Size,
    };

    private BorderWrapConstants BorderWrapConstants(BorderWrapCommand command) => new()
    {
        Quad = Quad(command.Placement, command.Box, Color.White, command.Placement.Scissor),
        TextureIndex = _layers[command.Layer].Slot,
        FilterBorderWrapColor = command.Color,
        FilterBorderWrapColorScale = command.Box.Size / command.Placement.Quad.Size,
        FilterBorderWrapWidth = command.Width,
    };

    private BackdropConstants BackdropConstants(BackdropCommand command) => new()
    {
        Quad = Quad(command.Placement, command.Box, command.Tint, -1),
        PainterScissorIndex = command.Placement.Scissor,
        CornerRadius = command.Radii.Horizontal,
        CornerRadiusV = command.Radii.Vertical,
        Brightness = command.Filter.Brightness,
        Contrast = command.Filter.Contrast,
        Saturate = command.Filter.Saturation,
        Invert = command.Filter.Invert,
        HueRotate = command.Filter.HueRotation,
        Sepia = command.Filter.Sepia,
        BlurScale = command.Filter.Blur,
        FrameBufferCopyTextureIndex = _lastGrab.Slot,
    };
}
