using System.Collections.Generic;
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
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Title))
        {
            builder.AppendLine(Title);
            builder.AppendLine();
        }

        var body = BuildBodyText();
        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine(body);
            builder.AppendLine();
        }

        for (var index = 0; index < _options.Count; index++)
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

        Size = ResolveViewportSize();
        ZIndex = 100;

        _panel = new Panel
        {
            Name = "Panel",
            Size = new Vector2(PanelWidth, PanelHeight),
            Position = new Vector2(
                (Size.X - PanelWidth) * 0.5f,
                (Size.Y - PanelHeight) * 0.5f),
        };

        _label = new Label
        {
            Name = "Label",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(PanelWidth - (PanelPadding * 2f), PanelHeight - (PanelPadding * 2f)),
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

        Size = ResolveViewportSize();
        _panel.Position = new Vector2(
            (Size.X - _panel.Size.X) * 0.5f,
            (Size.Y - _panel.Size.Y) * 0.5f);
        _panel.Visible = Visible;
        _label.Visible = Visible;
        _label.Text = MenuText;
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}