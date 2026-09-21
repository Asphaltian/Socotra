namespace Socotra.Layout;

internal interface IInlineContent
{
    LayoutSize Measure(float width, bool minContent);
    InlineContentLayout Layout(float width);
}

internal readonly record struct InlineFragment(LayoutNode Owner, int TextStart, int TextLength,
    float X, float Y, float Width, float Height);

internal sealed record InlineContentLayout(LayoutSize Size, float Baseline, IReadOnlyList<InlineFragment> Fragments);
