using Topten.RichTextKit;

namespace Socotra;

public static partial class TextRendering
{
    internal sealed class TextBlock
    {
        public TextBlock(in Scope scope, TextFlag flags, Vector2 clip)
        {
            IsEmpty = string.IsNullOrEmpty(scope.Text);

            var block = new Topten.RichTextKit.TextBlock
            {
                FontMapper = FontManager.Instance,
                MaxWidth = clip.X,
                MaxHeight = clip.Y,
                Alignment = GetAlignment(flags),
            };

            if (flags.HasFlag(TextFlag.SingleLine))
            {
                block.MaxLines = 1;
            }

            if (flags.HasFlag(TextFlag.DontClip))
            {
                block.MaxWidth = null;
                block.MaxHeight = null;
            }

            var style = new Style();
            scope.ToStyle(style);
            block.AddText(IsEmpty ? "." : scope.Text, style);

            var padding = block.MeasuredPadding;
            int width = Socotra.TextBlock.ClampSize(block.MeasuredWidth);
            int height = Socotra.TextBlock.ClampSize(block.MeasuredHeight);

            if (style.LetterSpacing < 0)
            {
                width += Math.Abs((int)MathF.Floor(style.LetterSpacing));
            }

            var overhang = block.MeasuredOverhang;
            var margin = EffectMargin(scope) + new Margin(MathF.Ceiling(overhang.Left), MathF.Ceiling(overhang.Top), MathF.Ceiling(overhang.Right), MathF.Ceiling(overhang.Bottom));
            var edge = margin.EdgeSize;
            width += (int)MathF.Ceiling(edge.X);
            height += (int)MathF.Ceiling(edge.Y);

            if (IsEmpty)
            {
                block.Clear();
            }

            Layout = block;
            Size = new Vector2(width, height);
            BlockOrigin = new Vector2(margin.Left - padding.Left, margin.Top - padding.Top);
        }

        public bool IsEmpty { get; }

        public Topten.RichTextKit.TextBlock Layout { get; }

        public Vector2 Size { get; }

        public Vector2 BlockOrigin { get; }

        public long LastUsed { get; set; }

        private static Margin EffectMargin(in Scope scope)
        {
            float left = 0, top = 0, right = 0, bottom = 0;

            if (scope.Outline.Enabled && scope.Outline.Size > 0)
            {
                left = top = right = bottom = MathF.Ceiling(scope.Outline.Size);
            }

            if (scope.Shadow.Enabled)
            {
                var blur = Math.Clamp(scope.Shadow.Size, 0, 512) * 3;
                var offset = scope.Shadow.Offset;
                left = MathF.Ceiling(MathF.Max(left, blur - offset.X));
                right = MathF.Ceiling(MathF.Max(right, blur + offset.X));
                top = MathF.Ceiling(MathF.Max(top, blur - offset.Y));
                bottom = MathF.Ceiling(MathF.Max(bottom, blur + offset.Y));
            }

            return new Margin(MathF.Min(left, 512), MathF.Min(top, 512), MathF.Min(right, 512), MathF.Min(bottom, 512));
        }

        private static TextAlignment GetAlignment(TextFlag flags)
        {
            if (flags.HasFlag(TextFlag.Left))
            {
                return TextAlignment.Left;
            }

            if (flags.HasFlag(TextFlag.CenterHorizontally))
            {
                return TextAlignment.Center;
            }

            return flags.HasFlag(TextFlag.Right) ? TextAlignment.Right : TextAlignment.Left;
        }
    }
}
