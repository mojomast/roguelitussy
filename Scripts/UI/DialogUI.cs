using System.Collections.Generic;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class DialogUI : Control
{
    private const float PanelWidth = 780f;
    private const float PanelHeight = 300f;
    private const float PanelPadding = 18f;
    private const float OuterMargin = 24f;

    private Panel? _panel;
    private RichTextLabel? _bodyLabel;
    private GameManager.InteractionContext? _context;
    private string _currentNodeId = string.Empty;
    private int _selectedOptionIndex;

    public DialogUI()
    {
        Name = "DialogUI";
        Visible = false;
    }

    public event System.Action<EntityId>? ShopRequested;

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void Open(GameManager.InteractionContext context)
    {
        _context = context;
        _currentNodeId = context.DialogueTemplate.StartNodeId;
        _selectedOptionIndex = 0;
        Visible = true;
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        _context = null;
        _currentNodeId = string.Empty;
        _selectedOptionIndex = 0;
        RefreshVisualState();
    }

    public bool HandleKey(Key key)
    {
        if (!Visible || _context is null || !TryGetCurrentNode(out var node))
        {
            return false;
        }

        switch (key)
        {
            case Key.Up:
                MoveSelection(-1, node.Options.Count);
                return true;
            case Key.Down:
                MoveSelection(1, node.Options.Count);
                return true;
            case Key.Enter:
            case Key.KpEnter:
                ActivateSelectedOption();
                return true;
            case Key.Escape:
            case Key.F:
                Close();
                return true;
            case Key.Key1:
            case Key.Key2:
            case Key.Key3:
            case Key.Key4:
            case Key.Key5:
            case Key.Key6:
            case Key.Key7:
            case Key.Key8:
            case Key.Key9:
                return ActivateByNumber(key, node.Options.Count);
            default:
                return false;
        }
    }

    private bool ActivateByNumber(Key key, int optionCount)
    {
        var numericIndex = (int)key - (int)Key.Key1;
        if (numericIndex < 0 || numericIndex >= optionCount)
        {
            return false;
        }

        _selectedOptionIndex = numericIndex;
        ActivateSelectedOption();
        return true;
    }

    private void MoveSelection(int delta, int optionCount)
    {
        if (optionCount <= 0)
        {
            return;
        }

        _selectedOptionIndex = (_selectedOptionIndex + delta + optionCount) % optionCount;
        RefreshVisualState();
    }

    private void ActivateSelectedOption()
    {
        if (_context is null || !TryGetCurrentNode(out var node) || node.Options.Count == 0)
        {
            Close();
            return;
        }

        var option = node.Options[_selectedOptionIndex];
        if (string.Equals(option.ActionId, "shop", System.StringComparison.OrdinalIgnoreCase))
        {
            ShopRequested?.Invoke(_context.NpcId);
            Close();
            return;
        }

        if (string.Equals(option.ActionId, "close", System.StringComparison.OrdinalIgnoreCase))
        {
            Close();
            return;
        }

        if (!string.IsNullOrWhiteSpace(option.NextNodeId))
        {
            if (!_context.DialogueTemplate.Nodes.ContainsKey(option.NextNodeId))
            {
                Close();
                return;
            }

            _currentNodeId = option.NextNodeId;
            _selectedOptionIndex = 0;
            RefreshVisualState();
            return;
        }

        Close();
    }

    private bool TryGetCurrentNode(out DialogueNode node)
    {
        node = null!;
        return _context is not null && _context.DialogueTemplate.Nodes.TryGetValue(_currentNodeId, out node!);
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _bodyLabel is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        ZIndex = 96;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _bodyLabel = new RichTextLabel
        {
            Name = "BodyLabel",
            Position = new Vector2(PanelPadding, PanelPadding),
            BbcodeEnabled = true,
        };
        _panel.AddChild(_bodyLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _bodyLabel is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        _panel.Visible = Visible;
        _bodyLabel.Visible = Visible;
        _bodyLabel.Position = new Vector2(PanelPadding, PanelPadding);
        _bodyLabel.Size = new Vector2(
            System.Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
            System.Math.Max(0f, panelSize.Y - (PanelPadding * 2f)));
        _bodyLabel.Clear();
        _bodyLabel.AppendText(BuildBodyMarkup());
    }

    private string BuildBodyMarkup()
    {
        if (_context is null || !TryGetCurrentNode(out var node))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"[b]{ItemRarityPresentation.EscapeBBCode(_context.DisplayName)}[/b]");
        builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"Role: {_context.Role}"));
        builder.AppendLine();
        builder.AppendLine(ItemRarityPresentation.EscapeBBCode(node.Text));
        builder.AppendLine();
        for (var index = 0; index < node.Options.Count; index++)
        {
            var marker = index == _selectedOptionIndex ? ">" : " ";
            builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"{marker} {index + 1}. {node.Options[index].Text}"));
        }

        builder.AppendLine();
        builder.Append(ItemRarityPresentation.EscapeBBCode("Up/Down or 1-9: choose  Enter: confirm  Esc/F: close"));
        return builder.ToString().TrimEnd();
    }

    private static Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}