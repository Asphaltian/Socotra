namespace Socotra;

internal static class ImageRect
{
    public record struct Input
    {
        public Texture? Image;
        public Rect PanelRect;
        public Length? ImageSizeX;
        public Length? ImageSizeY;
        public Length? ImagePositionX;
        public Length? ImagePositionY;
        public float ScaleToScreen;
        public Length DefaultSize;
    }

    public static Vector4 Calculate(in Input input)
    {
        var w = (float)(input.Image?.Width ?? input.PanelRect.Width);
        var h = (float)(input.Image?.Height ?? input.PanelRect.Height);
        var aspect = h / w;
        var sizeX = input.ImageSizeX is { Unit: not LengthUnit.Undefined } size ? size : input.DefaultSize;

        switch (sizeX.Unit)
        {
            case LengthUnit.Cover:
                w = input.PanelRect.Width;
                h = w * aspect;
                if (h < input.PanelRect.Height)
                {
                    h = input.PanelRect.Height;
                    w = h / aspect;
                }

                break;

            case LengthUnit.Contain:
                w = input.PanelRect.Width;
                h = w * aspect;
                if (h > input.PanelRect.Height)
                {
                    h = input.PanelRect.Height;
                    w = h / aspect;
                }

                break;

            case LengthUnit.Pixels or LengthUnit.Percentage:
                w = input.ImageSizeX?.GetPixels(input.PanelRect.Width) ?? w;
                h = input.ImageSizeY?.GetPixels(input.PanelRect.Height) ?? h;
                if (input.ImageSizeX?.Unit == LengthUnit.Pixels)
                {
                    w *= input.ScaleToScreen;
                }

                if (input.ImageSizeY?.Unit == LengthUnit.Pixels)
                {
                    h *= input.ScaleToScreen;
                }

                break;
        }

        var x = input.ImagePositionX?.GetPixels(input.PanelRect.Width, w) ?? 0;
        var y = input.ImagePositionY?.GetPixels(input.PanelRect.Height, h) ?? 0;
        return new Vector4(x, y, w, h);
    }
}
