using System.Diagnostics.CodeAnalysis;

namespace Socotra;

internal sealed partial class PanelLayout
{
    private InlineFormattingContext? _inlineContext;

    public InlineFormattingContext? InlineContext => _inlineContext;

    [MemberNotNullWhen(true, nameof(_inlineContext))]
    public bool HasInlineContent => _inlineContext?.Root == this;

    public bool IsInlineParticipant => _inlineContext is not null && _inlineContext.Root != this;

    public string? SelectedInlineText => _inlineContext?.SelectedText;

    public void PrepareInlineContent()
    {
        if (!InlineFormattingContext.CanFormat(_panel))
        {
            if (HasInlineContent)
            {
                ReleaseInlineContext();
            }

            return;
        }

        if (!HasInlineContent)
        {
            ReleaseInlineContext();
            _inlineContext = new InlineFormattingContext(_panel);
        }

        _inlineContext.Update();
        _node.InlineContent = _inlineContext;
    }

    public void JoinInlineContext(InlineFormattingContext context)
    {
        if (_inlineContext == context)
        {
            return;
        }

        ReleaseInlineContext();
        _inlineContext = context;
    }

    public void LeaveInlineContext(InlineFormattingContext context)
    {
        if (_inlineContext != context)
        {
            return;
        }

        _inlineContext = null;
        _node.InlineFragments = [];
        _node.MarkDirty();
    }

    public void FinalizeInlineContent()
    {
        if (HasInlineContent)
        {
            _inlineContext.FinalizeLayout();
        }
    }

    public void DrawInlineContent(Painter painter)
    {
        if (HasInlineContent)
        {
            _inlineContext.Draw(painter);
        }
    }

    public bool ContainsInlineContent(Vector2 position) => _inlineContext?.Contains(_panel, position) == true;

    public bool SelectInlineText(Vector2 start, Vector2 end)
    {
        if (!HasInlineContent)
        {
            return false;
        }

        _inlineContext.Select(start, end);
        return true;
    }

    public bool SetInlineSelection(int start, int end)
    {
        if (!HasInlineContent)
        {
            return false;
        }

        _inlineContext.SetSelection(start, end);
        return true;
    }

    private void ReleaseInlineContext()
    {
        var context = _inlineContext;
        if (context is null)
        {
            return;
        }

        _inlineContext = null;
        if (context.Root == this)
        {
            _node.InlineContent = null;
            context.Dispose();
        }
        else
        {
            context.Invalidate();
        }
    }
}
