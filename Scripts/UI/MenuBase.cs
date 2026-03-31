using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Godotussy;

public abstract partial class MenuBase : Control
{
    private readonly List<string> _options = new();
    private Panel? _panel;
    private Label? _label;
    private const float PanelWidth = 420f;
    private const float PanelHeight = 300f;
    private const float PanelPadding = 20f;
    private const float OuterMargin = 24f;
    private const float ApproxLineHeight = 18f;
    private const int MinimumVisibleOptions = 4;

    protected int SelectedIndex { get; private set; }

    public string Title { get; protected set; } = string.Empty;

    public string MenuText { get; private set; } = string.Empty;

    public IReadOnlyList<string> Options => _options;

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    protected void ConfigureOptions(params string[] options)
    {
        _options.Clear();
        _options.AddRange(options);
        if (SelectedIndex >= _options.Count)
        {
            SelectedIndex = 0;
        }

        RebuildMenuText();
    }

    protected void SetSelection(int index)
    {
        if (_options.Count == 0)
        {
            SelectedIndex = 0;
            MenuText = string.Empty;
            return;
        }

        SelectedIndex = (index % _options.Count + _options.Count) % _options.Count;
        RebuildMenuText();
    }

    public virtual bool HandleKey(Key key)
    {
        if (!Visible || _options.Count == 0)
        {
            return false;
        }

        switch (key)
        {
            case Key.Up:
            case Key.W:
                SetSelection(SelectedIndex - 1);
                return true;
            case Key.Down:
            case Key.S:
                SetSelection(SelectedIndex + 1);
                return true;
            case Key.Enter:
                ActivateSelected();
                return true;
            case Key.Escape:
                Cancel();
                return true;
            default:
                return HandleCustomKey(key);
        }
    }

    public virtual void Open()
    {
        Visible = true;
        SetSelection(SelectedIndex);
        RefreshVisualState();
    }

    public virtual void Close()
    {
        Visible = false;
        RefreshVisualState();
    }

    protected virtual bool HandleCustomKey(Key key)
    {
        return false;
    }

    protected virtual void Cancel()
    {
        Close();
    }

    protected abstract void ActivateSelected();

    protected virtual string BuildBodyText()
    {
        return string.Empty;
    }

    protected void RebuildMenuText()
    {
        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        var contentLineCapacity = Math.Max(1, (int)Math.Floor(Math.Max(0f, panelSize.Y - (PanelPadding * 2f)) / ApproxLineHeight));
        var bodyLines = SplitLines(BuildBodyText());
        var titleLines = SplitLines(Title);
        var minimumOptionLines = Math.Min(_options.Count, MinimumVisibleOptions);

        var titleSectionLines = titleLines.Count == 0 ? 0 : titleLines.Count + 1;
        var availableBodyLines = Math.Max(0, contentLineCapacity - titleSectionLines - minimumOptionLines);
        var visibleBodyLines = bodyLines;
        if (visibleBodyLines.Count > availableBodyLines)
        {
            visibleBodyLines = bodyLines.Take(availableBodyLines).ToList();
            if (visibleBodyLines.Count > 0 && bodyLines.Count > availableBodyLines)
            {
                visibleBodyLines[^1] = "...";
            }
        }

        var bodySectionLines = visibleBodyLines.Count == 0 ? 0 : visibleBodyLines.Count + 1;
        var visibleOptionCount = Math.Max(1, contentLineCapacity - titleSectionLines - bodySectionLines);
        var firstVisibleOption = ResolveFirstVisibleOption(visibleOptionCount);

        var builder = new StringBuilder();
        if (titleLines.Count > 0)
        {
            foreach (var line in titleLines)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine();
        }

        if (visibleBodyLines.Count > 0)
        {
            foreach (var line in visibleBodyLines)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine();
        }

        var lastVisibleOption = Math.Min(_options.Count, firstVisibleOption + visibleOptionCount);
        for (var index = firstVisibleOption; index < lastVisibleOption; index++)
        {
            builder.Append(index == SelectedIndex ? "> " : "  ");
            builder.AppendLine(_options[index]);
        }

        MenuText = builder.ToString().TrimEnd();
        RefreshVisualState();
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _label is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        ZIndex = 100;

        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
            Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize),
        };

        _label = new Label
        {
            Name = "Label",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(
                Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
                Math.Max(0f, panelSize.Y - (PanelPadding * 2f))),
        };

        _panel.AddChild(_label);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _label is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        _label.Position = new Vector2(PanelPadding, PanelPadding);
        _label.Size = new Vector2(
            Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
            Math.Max(0f, panelSize.Y - (PanelPadding * 2f)));
        _panel.Visible = Visible;
        _label.Visible = Visible;
        _label.Text = MenuText;
        OnVisualStateRefreshed(_panel, _label, viewportSize, panelSize);
    }

    protected virtual void OnVisualStateRefreshed(Panel panel, Label label, Vector2 viewportSize, Vector2 panelSize)
    {
    }

    protected virtual Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var maxWidth = Math.Max(0f, viewportSize.X - (OuterMargin * 2f));
        var maxHeight = Math.Max(0f, viewportSize.Y - (OuterMargin * 2f));
        return new Vector2(
            Math.Min(maxWidth, Math.Max(PanelWidth, viewportSize.X * 0.72f)),
            Math.Min(maxHeight, Math.Max(PanelHeight, viewportSize.Y * 0.88f)));
    }

    private static List<string> SplitLines(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split('\n').Select(line => line.TrimEnd()).ToList();
    }

    private int ResolveFirstVisibleOption(int visibleOptionCount)
    {
        if (_options.Count <= visibleOptionCount)
        {
            return 0;
        }

        var preferred = SelectedIndex - (visibleOptionCount / 2);
        return Math.Clamp(preferred, 0, _options.Count - visibleOptionCount);
    }

    private Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, ResolveDesiredPanelSize(viewportSize), OuterMargin);
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}