namespace Socotra;

/// <summary>The top of a panel tree. Give it the area to fill and call <see cref="Update"/> every frame.</summary>
public partial class RootPanel : Panel
{
    private readonly HashSet<Panel> _styleRuleQueue = [];
    private readonly List<Panel> _deletionQueue = [];
    private Rect _layoutBounds;
    private float _layoutScale;
    private int _fonts;

    /// <summary>Makes an empty root panel.</summary>
    public RootPanel()
    {
        Input = new PanelInput(this);
    }

    /// <summary>The area the UI fills, in pixels.</summary>
    public Rect Bounds { get; private set; }

    /// <summary>How many screen pixels one CSS pixel is. By default you design for a 1080 pixel tall screen and the UI scales to fit.</summary>
    public float Scale { get; private set; } = 1;

    /// <summary>Seconds of UI time so far, added up from what you pass to <see cref="Update"/>.</summary>
    public double Time { get; private set; }

    internal float DeltaTime { get; private set; }

    /// <summary>
    /// Call this once a frame, before <see cref="Paint"/>, with the area the UI fills and the seconds since the last frame.
    /// Feed in the frame's input first. Update and paint all your root panels from the same thread.
    /// </summary>
    public void Update(Rect bounds, float deltaTime)
    {
        Time += deltaTime;
        DeltaTime = deltaTime;
        Bounds = bounds;
        Scale = Math.Clamp(GetScale(bounds), 0.1f, 10);

        RefreshTextForNewFonts();
        TickInternal();
        Input.Tick();
        Layout();
        FinishDeletions();
    }

    /// <summary>Deletes the root panel and everything in it, straight away unless you pass false.</summary>
    public override void Delete(bool immediate = true) => base.Delete(immediate);

    /// <summary>Override it to scale the UI differently, for example to design for a 720 pixel tall screen: <c>bounds.Height / 720</c>.</summary>
    protected virtual float GetScale(Rect bounds) => bounds.Height / 1080.0f;

    internal void QueueStyleRules(Panel panel) => _styleRuleQueue.Add(panel);

    internal void AddDeferredDeletion(Panel panel) => _deletionQueue.Add(panel);

    private void RefreshTextForNewFonts()
    {
        var fonts = FontManager.Instance.Generation;
        if (fonts == _fonts)
        {
            return;
        }

        _fonts = fonts;
        foreach (var label in Descendants.OfType<Label>())
        {
            label.SetNeedsPreLayout();
        }
    }

    private void Layout()
    {
        var cascade = new LayoutCascade { Scale = Scale };
        if (Bounds != _layoutBounds || Scale != _layoutScale)
        {
            _layoutBounds = Bounds;
            _layoutScale = Scale;
            StyleSelectorsChanged(true, true);
            SkipTransitions();
            cascade.SelectorChanged = true;
        }

        BuildStyleRules();
        Style.Left = 0;
        Style.Top = 0;
        Style.Width = Bounds.Width / Scale;
        Style.Height = Bounds.Height / Scale;
        LayoutPass(cascade);
        if (NeedsPreLayout)
        {
            LayoutPass(new LayoutCascade { Scale = Scale });
        }
    }

    internal void PushRootValues()
    {
        Length.RootSize = Bounds.Size;
        Length.RootFontSize = ComputedStyle?.FontSize?.Value ?? Length.InitialFontSize;
        Length.RootScale = Scale;
    }

    private void LayoutPass(LayoutCascade cascade)
    {
        PushRootValues();
        PreLayout(cascade);
        FinishDeletions();
        if (LayoutTree.IsDirty)
        {
            PushRootValues();
            LayoutTree.CalculateLayout(Bounds.Width, Bounds.Height);
        }

        PushRootValues();
        FinalLayout(Bounds.Position);
        foreach (var panel in FixedOverlays)
        {
            if (panel.IsVisible || panel.HasIntro)
            {
                panel.FinalLayout(Bounds.Position);
            }
        }
    }

    private void BuildStyleRules()
    {
        foreach (var panel in _styleRuleQueue)
        {
            if (!panel.IsDeleted && panel.Style.BuildRules())
            {
                panel.SetNeedsPreLayout();
            }

            panel.MarkStylesRebuilt();
        }

        _styleRuleQueue.Clear();
    }

    private void FinishDeletions()
    {
        for (int i = _deletionQueue.Count - 1; i >= 0; i--)
        {
            var panel = _deletionQueue[i];
            if (panel.IsDeleted || !panel.HasActiveTransitions)
            {
                _deletionQueue.RemoveAt(i);
                try
                {
                    panel.Delete(immediate: true);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }
    }
}
