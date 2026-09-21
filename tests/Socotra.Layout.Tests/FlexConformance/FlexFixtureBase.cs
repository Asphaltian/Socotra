namespace Socotra.Layout.Tests.FlexConformance;

public abstract class FlexFixtureBase
{
    private protected static LayoutNode CreateNode()
    {
        var node = new LayoutNode();
        node.Style.FlexDirection = FlexDirection.Column;
        node.Style.AlignContent = Align.FlexStart;
        node.Style.JustifyContent = Justify.FlexStart;
        node.Style.FlexShrink = 0;
        return node;
    }

    protected static void AssertEq(float expected, float actual, string what)
    {
        if (float.IsNaN(expected) && float.IsNaN(actual))
        {
            return;
        }

        if (float.IsNaN(expected) || float.IsNaN(actual) || MathF.Abs(expected - actual) > 0.01f)
        {
            Assert.Fail($"{what}: expected {expected}, got {actual}");
        }
    }

    private protected static LayoutSize IntrinsicSizeMeasure(LayoutNode node, float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        var innerText = (string?)node.Context ?? "";
        const float heightPerChar = 10;
        const float widthPerChar = 10;
        float measuredWidth;
        float measuredHeight;

        if (widthMode == MeasureMode.Exactly)
        {
            measuredWidth = width;
        }
        else if (widthMode == MeasureMode.AtMost)
        {
            measuredWidth = MathF.Min(innerText.Length * widthPerChar, width);
        }
        else if (widthMode == MeasureMode.MinContent)
        {
            measuredWidth = LongestWordWidth(innerText, widthPerChar);
        }
        else
        {
            measuredWidth = innerText.Length * widthPerChar;
        }

        if (heightMode == MeasureMode.Exactly)
        {
            measuredHeight = height;
        }
        else if (heightMode == MeasureMode.AtMost)
        {
            measuredHeight = MathF.Min(CalculateHeight(innerText, MathF.Max(LongestWordWidth(innerText, widthPerChar), measuredWidth), widthPerChar, heightPerChar), height);
        }
        else
        {
            measuredHeight = CalculateHeight(innerText, MathF.Max(LongestWordWidth(innerText, widthPerChar), measuredWidth), widthPerChar, heightPerChar);
        }

        return new LayoutSize(measuredWidth, measuredHeight);
    }

    private static float LongestWordWidth(string text, float widthPerChar)
    {
        int maxLength = 0;
        int currentLength = 0;
        foreach (var c in text)
        {
            if (c == ' ')
            {
                maxLength = Math.Max(currentLength, maxLength);
                currentLength = 0;
            }
            else
            {
                currentLength++;
            }
        }
        return Math.Max(currentLength, maxLength) * widthPerChar;
    }

    private static float CalculateHeight(string text, float measuredWidth, float widthPerChar, float heightPerChar)
    {
        if (text.Length * widthPerChar <= measuredWidth)
        {
            return heightPerChar;
        }

        var words = text.Split(' ');

        float lines = 1;
        float currentLineLength = 0;
        foreach (var word in words)
        {
            var wordWidth = word.Length * widthPerChar;
            if (wordWidth > measuredWidth)
            {
                if (currentLineLength > 0)
                {
                    lines++;
                }

                lines++;
                currentLineLength = 0;
            }
            else if (currentLineLength + wordWidth <= measuredWidth)
            {
                currentLineLength += wordWidth + widthPerChar;
            }
            else
            {
                lines++;
                currentLineLength = wordWidth + widthPerChar;
            }
        }

        return (currentLineLength == 0 ? lines - 1 : lines) * heightPerChar;
    }
}
