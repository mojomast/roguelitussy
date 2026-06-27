using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Godotussy;

public abstract partial class MenuBase : Control
{
    private readonly List<string> _options = new();
    private readonly HashSet<int> _sectionHeaderIndices = new();
    private Panel? _panel;
    private ColorRect? _backdrop;
    private ColorRect? _headerBand;
    private ColorRect? _bodyCard;
    private ColorRect? _optionsCard;
    private Label? _titleLabel;
    private Label? _label;
    private Label? _optionsLabel;
    private Label? _footerLabel;
    private readonly List<Panel> _optionRows = new();
    private const float PanelWidth = 420f;
    private const float PanelHeight = 300f;
    private const float PanelPadding = 20f;
    private const float OuterMargin = 24f;
    private const float HeaderHeight = 52f;
    private const float FooterHeight = 22f;
    private const float CardPadding = 14f;
    private const float SectionSpacing = 12f;
    private const int MinimumVisibleOptions = 4;
    private string _visibleTitleText = string.Empty;
    private string _visibleBodyText = string.Empty;
    private string _visibleOptionsText = string.Empty;
    private string _visibleFooterText = string.Empty;
    private int _firstVisibleOption;
    private int _visibleOptionCount;

    protected int SelectedIndex { get; private set; }

    protected Panel? RootPanel => _panel;

    protected ColorRect? Backdrop => _backdrop;

    protected ColorRect? HeaderBand => _headerBand;

    protected ColorRect? BodyCard => _bodyCard;

    protected ColorRect? OptionsCard => _optionsCard;

    protected Label? TitleLabel => _titleLabel;

    protected Label? BodyLabel => _label;

    protected Label? OptionsLabel => _optionsLabel;

    protected Label? FooterLabel => _footerLabel;

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
        _sectionHeaderIndices.Clear();
        _options.AddRange(options);
        if (SelectedIndex >= _options.Count || IsSectionHeader(SelectedIndex))
        {
            SelectedIndex = ResolveNextSelectableIndex(0, 1);
        }

        RebuildMenuText();
    }

    protected void ConfigureSectionHeader(string title)
    {
        _sectionHeaderIndices.Add(_options.Count);
        _options.Add($"── {title.ToUpperInvariant()} ─────────────");
        if (_options.Count == 1)
        {
            SelectedIndex = ResolveNextSelectableIndex(0, 1);
        }

        RebuildMenuText();
    }

    protected void ConfigureOption(string option)
    {
        _options.Add(option);
        if (_options.Count == 1 || IsSectionHeader(SelectedIndex))
        {
            SelectedIndex = ResolveNextSelectableIndex(0, 1);
        }

        RebuildMenuText();
    }

    protected void SetSelection(int index)
    {
        if (_options.Count == 0)
        {
            SelectedIndex = 0;
            RebuildMenuText();
            return;
        }

        SelectedIndex = ResolveNextSelectableIndex(index, index >= SelectedIndex ? 1 : -1);
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
            case Key.KpEnter:
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

    protected virtual string BuildFooterText()
    {
        return _options.Count == 0
            ? "Esc close"
            : "Up/Down move  Enter confirm  Esc back";
    }

    protected void RebuildMenuText()
    {
        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        var contentLineCapacity = Math.Max(1, (int)Math.Floor(Math.Max(0f, panelSize.Y - (PanelPadding * 2f)) / ResolveApproxLineHeight()));
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
        var visibleOptionCount = _options.Count == 0
            ? 0
            : Math.Max(1, contentLineCapacity - titleSectionLines - bodySectionLines);
        var firstVisibleOption = visibleOptionCount == 0 ? 0 : ResolveFirstVisibleOption(visibleOptionCount);
        var visibleOptionsText = BuildVisibleOptionsText(firstVisibleOption, visibleOptionCount);
        _firstVisibleOption = firstVisibleOption;
        _visibleOptionCount = visibleOptionCount;

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

        if (!string.IsNullOrWhiteSpace(visibleOptionsText))
        {
            builder.Append(visibleOptionsText);
        }

        _visibleTitleText = string.Join("\n", titleLines);
        _visibleBodyText = string.Join("\n", visibleBodyLines);
        _visibleOptionsText = visibleOptionsText;
        _visibleFooterText = BuildFooterText();
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

        _backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = UiStyle.PanelBlack(0.98f),
        };
        _headerBand = new ColorRect
        {
            Name = "HeaderBand",
            Color = UiStyle.PanelHighlight(0.98f),
        };
        _bodyCard = new ColorRect
        {
            Name = "BodyCard",
            Color = UiStyle.PanelInner(0.96f),
        };
        _optionsCard = new ColorRect
        {
            Name = "OptionsCard",
            Color = UiStyle.CathedralBlack(0.98f),
        };
        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Modulate = UiStyle.BrightGold(),
        };

        _label = new Label
        {
            Name = "Label",
            Modulate = UiStyle.Parchment(),
        };
        _optionsLabel = new Label
        {
            Name = "OptionsLabel",
            Modulate = UiStyle.Parchment(),
        };
        _footerLabel = new Label
        {
            Name = "FooterLabel",
            Modulate = UiStyle.MutedText(),
        };

        _bodyCard.AddChild(_label);
        _optionsCard.AddChild(_optionsLabel);
        _panel.AddChild(_backdrop);
        _panel.AddChild(_headerBand);
        _panel.AddChild(_bodyCard);
        _panel.AddChild(_optionsCard);
        _panel.AddChild(_titleLabel);
        _panel.AddChild(_footerLabel);
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
        if (_backdrop is null
            || _headerBand is null
            || _bodyCard is null
            || _optionsCard is null
            || _titleLabel is null
            || _optionsLabel is null
            || _footerLabel is null)
        {
            return;
        }

        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        _panel.Modulate = UiStyle.GoldTrim();
        _backdrop.Position = Vector2.Zero;
        _backdrop.Size = panelSize;
        _headerBand.Position = Vector2.Zero;
        _headerBand.Size = new Vector2(panelSize.X, HeaderHeight);

        var footerTop = panelSize.Y - PanelPadding - FooterHeight;
        var contentTop = HeaderHeight + 10f;
        var contentBottom = footerTop - 10f;
        var availableHeight = Math.Max(0f, contentBottom - contentTop);
        var bodyLineCount = CountLines(_visibleBodyText);
        var optionLineCount = CountLines(_visibleOptionsText);
        var gap = bodyLineCount > 0 && optionLineCount > 0 ? SectionSpacing : 0f;

        var desiredBodyHeight = bodyLineCount == 0
            ? 0f
            : Math.Clamp((bodyLineCount * ResolveApproxLineHeight()) + (CardPadding * 2f), 84f, availableHeight);
        var desiredOptionsHeight = optionLineCount == 0
            ? 0f
            : Math.Clamp((optionLineCount * ResolveApproxLineHeight()) + (CardPadding * 2f), 84f, availableHeight);

        if (desiredBodyHeight == 0f)
        {
            desiredOptionsHeight = availableHeight;
        }
        else if (desiredOptionsHeight == 0f)
        {
            desiredBodyHeight = availableHeight;
        }
        else
        {
            var desiredTotal = desiredBodyHeight + desiredOptionsHeight + gap;
            if (desiredTotal > availableHeight)
            {
                var scale = Math.Clamp((availableHeight - gap) / Math.Max(1f, desiredBodyHeight + desiredOptionsHeight), 0f, 1f);
                desiredBodyHeight *= scale;
                desiredOptionsHeight *= scale;
            }
        }

        var contentWidth = Math.Max(0f, panelSize.X - (PanelPadding * 2f));
        _titleLabel.Position = new Vector2(PanelPadding, 12f);
        _titleLabel.Size = new Vector2(contentWidth, 30f);
        _titleLabel.Text = _visibleTitleText;

        _bodyCard.Position = new Vector2(PanelPadding, contentTop);
        _bodyCard.Size = new Vector2(contentWidth, Math.Max(0f, desiredBodyHeight));
        _bodyCard.Visible = Visible && !string.IsNullOrWhiteSpace(_visibleBodyText);

        _label.Position = new Vector2(CardPadding, CardPadding - 2f);
        _label.Size = new Vector2(
            Math.Max(0f, _bodyCard.Size.X - (CardPadding * 2f)),
            Math.Max(0f, _bodyCard.Size.Y - (CardPadding * 2f)));
        _label.Text = _visibleBodyText;
        _label.Visible = _bodyCard.Visible;

        var optionsTop = _bodyCard.Visible
            ? _bodyCard.Position.Y + _bodyCard.Size.Y + gap
            : contentTop;
        _optionsCard.Position = new Vector2(PanelPadding, optionsTop);
        _optionsCard.Size = new Vector2(contentWidth, Math.Max(0f, desiredOptionsHeight));
        _optionsCard.Visible = Visible && !string.IsNullOrWhiteSpace(_visibleOptionsText);

        _optionsLabel.Position = new Vector2(CardPadding, CardPadding - 2f);
        _optionsLabel.Size = new Vector2(
            Math.Max(0f, _optionsCard.Size.X - (CardPadding * 2f)),
            Math.Max(0f, _optionsCard.Size.Y - (CardPadding * 2f)));
        _optionsLabel.Text = _visibleOptionsText;
        _optionsLabel.Modulate = UiStyle.Parchment();
        _optionsLabel.Visible = _optionsCard.Visible;

        _footerLabel.Position = new Vector2(PanelPadding, footerTop);
        _footerLabel.Size = new Vector2(contentWidth, FooterHeight);
        _footerLabel.Text = _visibleFooterText;
        _panel.Visible = Visible;
        _backdrop.Visible = Visible;
        _headerBand.Visible = Visible;
        _titleLabel.Visible = Visible && !string.IsNullOrWhiteSpace(_visibleTitleText);
        _footerLabel.Visible = Visible && !string.IsNullOrWhiteSpace(_visibleFooterText);
        OnVisualStateRefreshed(_panel, _label, viewportSize, panelSize);
        RebuildOptionRows(_optionsCard, _optionsLabel.Position, _optionsLabel.Size);
        _optionsLabel.Visible = false;
    }

    protected virtual void OnVisualStateRefreshed(Panel panel, Label label, Vector2 viewportSize, Vector2 panelSize)
    {
    }

    private void RebuildOptionRows(ColorRect optionsCard, Vector2 position, Vector2 size)
    {
        foreach (var row in _optionRows)
        {
            optionsCard.RemoveChild(row);
            row.QueueFree();
        }

        _optionRows.Clear();
        if (!optionsCard.Visible || _visibleOptionCount <= 0 || _options.Count == 0)
        {
            return;
        }

        var y = position.Y;
        var lastVisible = Math.Min(_options.Count, _firstVisibleOption + _visibleOptionCount);
        for (var index = _firstVisibleOption; index < lastVisible; index++)
        {
            var section = IsSectionHeader(index);
            var rowHeight = section ? 18f : 22f;
            var rowIndex = index;
            var row = section
                ? new Panel()
                : new UiMousePanel { InputSubmitted = input => OnOptionRowInput(rowIndex, input) };
            row.Name = $"OptionRow_{index}";
            row.Position = new Vector2(position.X, y);
            row.Size = new Vector2(size.X, rowHeight);
            var background = section
                ? new ColorRect()
                : new UiMouseColorRect { InputSubmitted = input => OnOptionRowInput(rowIndex, input) };
            background.Name = "RowBackground";
            background.Position = Vector2.Zero;
            background.Size = row.Size;
            background.Color = index == SelectedIndex ? UiStyle.SlotSelected() : UiStyle.DeepBlack(0f);
            var accent = new ColorRect { Name = "RowAccent", Position = Vector2.Zero, Size = new Vector2(index == SelectedIndex && !section ? 2f : 0f, rowHeight), Color = UiStyle.DimGold() };
            var textLabel = section
                ? new Label()
                : new UiMouseLabel { InputSubmitted = input => OnOptionRowInput(rowIndex, input) };
            textLabel.Name = "RowLabel";
            textLabel.Position = new Vector2(section ? 0f : 8f, 2f);
            textLabel.Size = new Vector2(Math.Max(0f, size.X - 8f), rowHeight);
            textLabel.Text = section ? _options[index] : (index == SelectedIndex ? $"▶ {_options[index]}" : $"  {_options[index]}");
            textLabel.Modulate = section ? UiStyle.FaintText() : index == SelectedIndex ? UiStyle.BrightGold() : UiStyle.Parchment();
            row.AddChild(background);
            row.AddChild(accent);
            row.AddChild(textLabel);
            optionsCard.AddChild(row);
            _optionRows.Add(row);
            y += rowHeight;
        }
    }

    private void OnOptionRowInput(int index, InputEvent input)
    {
        if (!Visible || IsSectionHeader(index))
        {
            return;
        }

        if (input is InputEventMouseMotion)
        {
            SetSelection(index);
            return;
        }

        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            SetSelection(index);
            ActivateSelected();
        }
    }

    protected virtual Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var maxWidth = Math.Max(0f, viewportSize.X - (OuterMargin * 2f));
        var maxHeight = Math.Max(0f, viewportSize.Y - (OuterMargin * 2f));
        return new Vector2(
            Math.Min(maxWidth, Math.Max(PanelWidth, viewportSize.X * 0.72f)),
            Math.Min(maxHeight, Math.Max(PanelHeight, viewportSize.Y * 0.88f)));
    }

    protected virtual float ResolveApproxLineHeight()
    {
        return 22f;
    }

    private static List<string> SplitLines(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split('\n').Select(line => line.TrimEnd()).ToList();
    }

    private string BuildVisibleOptionsText(int firstVisibleOption, int visibleOptionCount)
    {
        if (_options.Count == 0 || visibleOptionCount <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var lastVisibleOption = Math.Min(_options.Count, firstVisibleOption + visibleOptionCount);
        for (var index = firstVisibleOption; index < lastVisibleOption; index++)
        {
            if (IsSectionHeader(index))
            {
                builder.AppendLine(_options[index]);
                continue;
            }

            builder.Append(index == SelectedIndex ? "> " : "  ");
            builder.AppendLine(_options[index]);
        }

        return builder.ToString().TrimEnd();
    }

    private static int CountLines(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split('\n').Length;
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

    private bool IsSectionHeader(int index) => _sectionHeaderIndices.Contains(index);

    private int ResolveNextSelectableIndex(int requestedIndex, int direction)
    {
        if (_options.Count == 0)
        {
            return 0;
        }

        var index = (requestedIndex % _options.Count + _options.Count) % _options.Count;
        var step = direction < 0 ? -1 : 1;
        for (var attempts = 0; attempts < _options.Count; attempts++)
        {
            if (!IsSectionHeader(index))
            {
                return index;
            }

            index = (index + step + _options.Count) % _options.Count;
        }

        return 0;
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
