using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class DialogUI : Control
{
    private const float PanelPadding = 18f;
    private const float RowHeight = 32f;
    private const int MaximumVisibleRows = 6;
    private readonly Dictionary<EntityId, int> _dialogOpenCounts = new();
    private readonly List<ColorRect> _rows = new();
    private readonly List<Label> _optionLabels = new();
    private GameManager? _manager;
    private EventBus? _bus;
    private WorldState? _greetingWorld;
    private GameManager.InteractionContext? _context;
    private Panel? _panel;
    private ColorRect? _surface;
    private ColorRect? _header;
    private Label? _titleLabel;
    private Label? _roleLabel;
    private RichTextLabel? _bodyLabel;
    private Label? _footerLabel;
    private Label? _statusLabel;
    private string _currentNodeId = string.Empty;
    private string _notice = string.Empty;
    private bool _showReport;
    private int _selectedOptionIndex;
    private int _visibleRows = MaximumVisibleRows;

    public DialogUI()
    {
        Name = "DialogUI";
        Visible = false;
    }

    public event Action<EntityId>? ShopRequested;

    public void Bind(GameManager? manager, EventBus? bus)
    {
        _manager = manager;
        _bus = bus;
        _dialogOpenCounts.Clear();
        _greetingWorld = manager?.World;
        Close();
    }

    public override void _Ready() => RefreshVisualState();

    public void Open(GameManager.InteractionContext context)
    {
        if (!ReferenceEquals(_greetingWorld, _manager?.World))
        {
            _dialogOpenCounts.Clear();
            _greetingWorld = _manager?.World;
        }

        _context = context;
        var starts = context.DialogueTemplate.StartNodeIds;
        _dialogOpenCounts.TryGetValue(context.NpcId, out var count);
        _currentNodeId = starts.Count == 0 ? context.DialogueTemplate.StartNodeId : starts[count % starts.Count];
        _dialogOpenCounts[context.NpcId] = starts.Count == 0 ? 0 : (count + 1) % starts.Count;
        _selectedOptionIndex = 0;
        _showReport = false;
        _notice = string.Empty;
        Visible = true;
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        _context = null;
        _currentNodeId = string.Empty;
        _selectedOptionIndex = 0;
        _showReport = false;
        _notice = string.Empty;
        RefreshVisualState();
    }

    public bool HandleKey(Key key)
    {
        if (!Visible || _context is null)
        {
            return false;
        }

        var options = GetOptions();
        switch (key)
        {
            case Key.Up:
            case Key.Down:
                _selectedOptionIndex = (_selectedOptionIndex + (key == Key.Up ? -1 : 1) + options.Count) % options.Count;
                RefreshVisualState();
                return true;
            case Key.Enter:
            case Key.KpEnter:
                Activate(options[Math.Clamp(_selectedOptionIndex, 0, options.Count - 1)]);
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
                var index = (int)key - (int)Key.Key1;
                if (index >= options.Count)
                {
                    return false;
                }

                Activate(options[index]);
                return true;
            default:
                return false;
        }
    }

    private IReadOnlyList<DialogueOption> GetOptions()
    {
        if (_showReport)
        {
            return new[] { new DialogueOption("Back to our conversation.", _currentNodeId, null), new DialogueOption("Leave.", null, "close") };
        }

        return _context is not null && _context.DialogueTemplate.Nodes.TryGetValue(_currentNodeId, out var node)
            ? DialogueResolver.GetOptions(node, _manager?.World?.Player)
            : new[] { new DialogueOption("Leave.", null, "close") };
    }

    private void Activate(DialogueOption option)
    {
        if (_context is null)
        {
            return;
        }

        _notice = string.Empty;
        if (string.Equals(option.ActionId, "shop", StringComparison.OrdinalIgnoreCase))
        {
            if (!_context.IsMerchant)
            {
                _notice = "There is nothing to trade here.";
                RefreshVisualState();
                return;
            }

            var npcId = _context.NpcId;
            Close();
            ShopRequested?.Invoke(npcId);
            return;
        }

        if (string.Equals(option.ActionId, "report", StringComparison.OrdinalIgnoreCase))
        {
            _showReport = true;
            _selectedOptionIndex = 0;
            RefreshVisualState();
            return;
        }

        if (option.ActionId?.StartsWith("service:", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (_manager?.World?.Player is not { } player)
            {
                _notice = "No active expedition is available.";
                RefreshVisualState();
                return;
            }

            var action = new NpcServiceAction(player.Id, _context.NpcId, option.ActionId[8..]);
            if (action.Validate(_manager.World) != ActionResult.Success)
            {
                _notice = "Treatment requires wounds, enough gold, and a living provider beside you.";
                _bus?.EmitLogMessage(_notice, LogCategory.Warning);
                RefreshVisualState();
                return;
            }

            // Close before the turn: enemy responses can end the run or replace modal UI.
            Close();
            _manager.ProcessPlayerAction(action);
            return;
        }

        if (!string.IsNullOrWhiteSpace(option.NextNodeId) && _context.DialogueTemplate.Nodes.ContainsKey(option.NextNodeId))
        {
            _currentNodeId = option.NextNodeId;
            _showReport = false;
            _selectedOptionIndex = 0;
            RefreshVisualState();
            return;
        }

        Close();
    }

    private string BodyText()
    {
        if (_showReport)
        {
            return _manager?.World is { } world ? DialogueResolver.BuildExpeditionReport(world) : "No active expedition is available.";
        }

        return _context is not null && _context.DialogueTemplate.Nodes.TryGetValue(_currentNodeId, out var node) ? node.Text : string.Empty;
    }

    private string OptionText(DialogueOption option)
    {
        var service = option.ActionId?.StartsWith("service:", StringComparison.OrdinalIgnoreCase) == true
            ? _context?.NpcTemplate.Services?.FirstOrDefault(value => value.Id == option.ActionId[8..])
            : null;
        return service is null ? option.Text : $"{service.Cost}g, up to {service.HealAmount} HP, 1 turn: {option.Text}";
    }

    public string SnapshotBodyMarkup()
    {
        if (_context is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine(ItemRarityPresentation.EscapeBBCode(_context.DisplayName));
        builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"Role: {_context.Role}"));
        builder.AppendLine(ItemRarityPresentation.EscapeBBCode(BodyText()));
        var options = GetOptions();
        var (start, end) = VisibleWindow(options.Count);
        if (start > 0)
        {
            builder.AppendLine("...");
        }

        for (var index = start; index < end; index++)
        {
            builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"{(index == _selectedOptionIndex ? ">" : " ")} {index + 1}. {OptionText(options[index])}"));
        }

        if (end < options.Count)
        {
            builder.AppendLine("...");
        }

        builder.AppendLine(ItemRarityPresentation.EscapeBBCode(_notice));
        return builder.ToString();
    }

    private (int Start, int End) VisibleWindow(int count)
    {
        var start = Math.Clamp(_selectedOptionIndex - (_visibleRows / 2), 0, Math.Max(0, count - _visibleRows));
        return (start, Math.Min(count, start + _visibleRows));
    }

    private void EnsureVisuals()
    {
        if (_panel is not null)
        {
            return;
        }

        ZIndex = 96;
        _panel = new Panel { Name = "Panel" };
        AddChild(_panel);
        _surface = new ColorRect { Name = "Surface", Color = UiStyle.PanelBlack() };
        _panel.AddChild(_surface);
        _header = new ColorRect { Name = "Header", Color = UiStyle.CathedralBlack() };
        _panel.AddChild(_header);
        _titleLabel = new Label { Name = "TitleLabel", Modulate = UiStyle.BrightGold() };
        _roleLabel = new Label { Name = "RoleLabel", Modulate = UiStyle.MutedText() };
        _footerLabel = new Label { Name = "FooterLabel", Modulate = UiStyle.MutedText() };
        _statusLabel = new Label { Name = "StatusLabel", Modulate = UiStyle.WarningAmber() };
        UiStyle.ConfigureSingleLineLabel(_titleLabel, 22);
        UiStyle.ConfigureSingleLineLabel(_roleLabel, 14);
        UiStyle.ConfigureSingleLineLabel(_footerLabel, 13);
        UiStyle.ConfigureSingleLineLabel(_statusLabel, 13);
        _panel.AddChild(_titleLabel);
        _panel.AddChild(_roleLabel);
        _bodyLabel = new RichTextLabel { Name = "BodyLabel", BbcodeEnabled = true, FitContent = false, Modulate = UiStyle.Parchment() };
        _bodyLabel.AddThemeFontSizeOverride("normal_font_size", 17);
        _panel.AddChild(_bodyLabel);
        for (var index = 0; index < MaximumVisibleRows; index++)
        {
            var row = new ColorRect { Name = $"OptionRow{index}" };
            var label = new Label { Name = "OptionLabel", Modulate = UiStyle.Parchment() };
            UiStyle.ConfigureSingleLineLabel(label, 15);
            row.AddChild(label);
            _panel.AddChild(row);
            _rows.Add(row);
            _optionLabels.Add(label);
        }

        _panel.AddChild(_statusLabel);
        _panel.AddChild(_footerLabel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();
        var viewport = GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
        var size = OverlayLayoutHelper.FitPanelSize(viewport, new Vector2(820f, 480f), 24f);
        Size = viewport;
        _panel!.Visible = Visible;
        _panel.Size = size;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewport, size);
        _surface!.Size = size;
        _header!.Size = new Vector2(size.X, 72f);
        var width = Math.Max(0f, size.X - PanelPadding * 2);
        _titleLabel!.Position = new Vector2(PanelPadding, 12f);
        _titleLabel.Size = new Vector2(width, 28f);
        _titleLabel.Text = _context?.DisplayName ?? string.Empty;
        _roleLabel!.Position = new Vector2(PanelPadding, 43f);
        _roleLabel.Size = new Vector2(width, 22f);
        _roleLabel.Text = _context is null ? string.Empty : $"{_context.Role}  /  {_context.NpcTemplate.FactionId.Replace('_', ' ')}";
        var bodyHeight = Math.Clamp(size.Y * 0.24f, 40f, 112f);
        _bodyLabel!.Position = new Vector2(PanelPadding, 84f);
        _bodyLabel.Size = new Vector2(width, bodyHeight);
        _bodyLabel.Clear();
        _bodyLabel.AppendText(ItemRarityPresentation.EscapeBBCode(BodyText()));
        var optionsY = 96f + bodyHeight;
        _visibleRows = Math.Clamp((int)((size.Y - optionsY - 60f) / RowHeight), 1, MaximumVisibleRows);
        var options = GetOptions();
        _selectedOptionIndex = Math.Clamp(_selectedOptionIndex, 0, options.Count - 1);
        var (start, end) = VisibleWindow(options.Count);
        for (var index = 0; index < MaximumVisibleRows; index++)
        {
            var optionIndex = start + index;
            _rows[index].Visible = Visible && optionIndex < end;
            _rows[index].Position = new Vector2(PanelPadding, optionsY + index * RowHeight);
            _rows[index].Size = new Vector2(width, RowHeight - 2f);
            _rows[index].Color = optionIndex == _selectedOptionIndex ? UiStyle.SlotSelected() : UiStyle.PanelInner();
            _optionLabels[index].Position = new Vector2(8f, 3f);
            _optionLabels[index].Size = new Vector2(Math.Max(0f, width - 16f), RowHeight - 6f);
            _optionLabels[index].Text = optionIndex < end ? $"{(optionIndex == _selectedOptionIndex ? ">" : " ")} {optionIndex + 1}. {OptionText(options[optionIndex])}" : string.Empty;
        }

        _statusLabel!.Position = new Vector2(PanelPadding, size.Y - 54f);
        _statusLabel.Size = new Vector2(width, 22f);
        _statusLabel.Text = _notice.Length > 0 ? _notice : $"Choices {start + 1}-{end} of {options.Count}";
        _footerLabel!.Position = new Vector2(PanelPadding, size.Y - 29f);
        _footerLabel.Size = new Vector2(width, 20f);
        _footerLabel.Text = "Up/Down or 1-9: choose   Enter: confirm   Esc/F: leave";
    }
}
