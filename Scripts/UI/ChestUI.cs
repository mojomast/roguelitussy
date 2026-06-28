using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ChestUI : MenuBase
{
    private enum ChestAction
    {
        ToggleItem,
        TakeSelected,
        TakeAll,
        Close,
    }

    private readonly record struct ChestOption(ChestAction Action, EntityId ItemId);

    private readonly System.Collections.Generic.List<ChestOption> _actions = new();
    private readonly System.Collections.Generic.HashSet<EntityId> _selectedItemIds = new();
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private EntityId _chestId = EntityId.Invalid;
    private string _statusText = string.Empty;
    private UiMouseColorRect? _titleClickTarget;

    public ChestUI()
    {
        Name = "ChestUI";
        Title = "CHEST";
        RebuildChestOptions();
        Visible = false;
    }

    public EntityId ChestId => _chestId;

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.TurnCompleted -= OnTurnCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;

        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted += OnLoadCompleted;
            _eventBus.TurnCompleted += OnTurnCompleted;
        }

        RebuildMenuText();
    }

    public void Open(EntityId chestId)
    {
        _chestId = chestId;
        _statusText = string.Empty;
        _selectedItemIds.Clear();
        Title = ResolveChestTitle();
        RebuildChestOptions();
        SetSelection(0);
        RebuildMenuText();
        base.Open();
    }

    public override void Close()
    {
        base.Close();
        _chestId = EntityId.Invalid;
        _statusText = string.Empty;
        _selectedItemIds.Clear();
        Title = "CHEST";
        RebuildChestOptions();
        RebuildMenuText();
    }

    public override bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        switch (key)
        {
            case Key.T:
            case Key.A:
                TakeAll();
                return true;
            case Key.Space:
                ToggleSelectedItem();
                return true;
            case Key.F:
                Close();
                return true;
            default:
                return base.HandleKey(key);
        }
    }

    public string SnapshotBodyMarkup() => BuildBodyText();

    protected override string BuildBodyText()
    {
        var chest = _gameManager?.World?.GetEntity(_chestId);
        var chestComponent = chest?.GetComponent<ChestComponent>();
        if (chest is null || chestComponent is null)
        {
            return "Chest unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Container   {chest.Name}");
        builder.AppendLine($"Loot source {chestComponent.LootTableId}");
        builder.AppendLine();
        if (!chestComponent.HasRolled)
        {
            builder.AppendLine("This chest has not been opened yet.");
        }
        else if (chestComponent.Contents.Count == 0)
        {
            builder.AppendLine("The chest is empty.");
        }
        else
        {
            builder.AppendLine("Choose individual treasures, or take everything that fits in your pack.");
            builder.AppendLine();
            for (var index = 0; index < chestComponent.Contents.Count; index++)
            {
                var item = chestComponent.Contents[index];
                var marker = _selectedItemIds.Contains(item.InstanceId) ? "[x]" : "[ ]";
                builder.AppendLine($"{index + 1,2}. {marker} {DescribeItem(item)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Enter toggles loot rows. TAKE SELECTED only moves checked items.");
        if (!string.IsNullOrWhiteSpace(_statusText))
        {
            builder.AppendLine();
            builder.Append(_statusText);
        }

        return builder.ToString().TrimEnd();
    }

    protected override string BuildFooterText()
    {
        return "Up/Down choose  Enter/Space toggle or confirm  T/A take all  F/Esc close";
    }

    protected override void ActivateSelected()
    {
        var option = SelectedIndex >= 0 && SelectedIndex < _actions.Count ? _actions[SelectedIndex] : new ChestOption(ChestAction.Close, EntityId.Invalid);
        if (option.Action == ChestAction.ToggleItem)
        {
            ToggleSelectedItem(option.ItemId);
        }
        else if (option.Action == ChestAction.TakeSelected)
        {
            TakeSelected();
        }
        else if (option.Action == ChestAction.TakeAll)
        {
            TakeAll();
        }
        else
        {
            Close();
        }
    }

    protected override void Cancel()
    {
        Close();
    }

    protected override Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var desired = new Vector2(520f, System.Math.Min(300f, viewportSize.Y * 0.70f));
        return OverlayLayoutHelper.FitPanelSize(viewportSize, desired, 24f);
    }

    protected override void OnVisualStateRefreshed(Panel panel, RichTextLabel label, Vector2 viewportSize, Vector2 panelSize)
    {
        EnsureTitleClickTarget(panel);
    }

    private void RebuildChestOptions()
    {
        _actions.Clear();
        ConfigureOptions();
        var chestComponent = _gameManager?.World?.GetEntity(_chestId)?.GetComponent<ChestComponent>();
        if (chestComponent is { HasRolled: true, Contents.Count: > 0 })
        {
            foreach (var item in chestComponent.Contents)
            {
                var marker = _selectedItemIds.Contains(item.InstanceId) ? "[x]" : "[ ]";
                AddOption($"{marker} {DescribeItem(item)}", new ChestOption(ChestAction.ToggleItem, item.InstanceId));
            }

            AddOption("Take Selected", new ChestOption(ChestAction.TakeSelected, EntityId.Invalid));
            AddOption("Take All", new ChestOption(ChestAction.TakeAll, EntityId.Invalid));
        }
        else if (chestComponent is { HasRolled: false })
        {
            AddOption("Roll Chest", new ChestOption(ChestAction.TakeAll, EntityId.Invalid));
        }

        AddOption("Close", new ChestOption(ChestAction.Close, EntityId.Invalid));
    }

    private void AddOption(string label, ChestOption action)
    {
        _actions.Add(action);
        ConfigureOption(label);
    }

    private void EnsureTitleClickTarget(Panel panel)
    {
        var titleLabel = TitleLabel;
        if (titleLabel is null)
        {
            return;
        }

        if (_titleClickTarget is null)
        {
            _titleClickTarget = new UiMouseColorRect
            {
                Name = "TitleClickTarget",
                Color = UiStyle.DeepBlack(0f),
                InputSubmitted = OnTitleInput,
            };
        }

        if (_titleClickTarget.GetParent() != panel)
        {
            _titleClickTarget.GetParent()?.RemoveChild(_titleClickTarget);
            panel.AddChild(_titleClickTarget);
        }

        _titleClickTarget.Position = titleLabel.Position;
        _titleClickTarget.Size = titleLabel.Size;
        _titleClickTarget.Visible = titleLabel.Visible;
    }

    private void OnTitleInput(InputEvent input)
    {
        if (!Visible)
        {
            return;
        }

        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            TakeAll();
        }
    }

    private void TakeAll()
    {
        SubmitTakeAction(takeAll: true);
    }

    private void TakeSelected()
    {
        if (_selectedItemIds.Count == 0)
        {
            _statusText = "Select at least one treasure row first.";
            RebuildMenuText();
            return;
        }

        SubmitTakeAction(takeAll: false);
    }

    private void SubmitTakeAction(bool takeAll)
    {
        var world = _gameManager?.World;
        var player = world?.Player;
        var chest = world?.GetEntity(_chestId);
        var chestComponent = chest?.GetComponent<ChestComponent>();
        if (world is null || player is null || chestComponent is null || _eventBus is null)
        {
            _statusText = "Chest unavailable.";
            RebuildMenuText();
            return;
        }

        if (!chestComponent.HasRolled)
        {
            var openAction = new OpenChestAction(player.Id, _chestId);
            if (openAction.Validate(world) != ActionResult.Success)
            {
                _statusText = "Move closer to open this chest.";
                RebuildMenuText();
                return;
            }

            _eventBus.EmitPlayerActionSubmitted(openAction);
            RebuildChestOptions();
            RebuildMenuText();
            return;
        }

        var action = new TakeChestLootAction(player.Id, _chestId, takeAll ? null : _selectedItemIds, takeAll);
        if (action.Validate(world) != ActionResult.Success)
        {
            _statusText = chestComponent.Contents.Count == 0 ? "The chest is empty." : "Move closer to loot this chest.";
            RebuildMenuText();
            return;
        }

        _eventBus.EmitPlayerActionSubmitted(action);
        var remainingChest = _gameManager?.World?.GetEntity(_chestId)?.GetComponent<ChestComponent>();
        if (remainingChest is null)
        {
            Close();
            return;
        }

        _selectedItemIds.RemoveWhere(id => !remainingChest.Contents.Exists(item => item.InstanceId == id));
        RebuildChestOptions();
        RebuildMenuText();
    }

    private void ToggleSelectedItem()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _actions.Count || _actions[SelectedIndex].Action != ChestAction.ToggleItem)
        {
            return;
        }

        ToggleSelectedItem(_actions[SelectedIndex].ItemId);
    }

    private void ToggleSelectedItem(EntityId itemId)
    {
        if (!itemId.IsValid)
        {
            return;
        }

        if (!_selectedItemIds.Add(itemId))
        {
            _selectedItemIds.Remove(itemId);
        }

        RebuildChestOptions();
        RebuildMenuText();
    }

    private string DescribeItem(ItemInstance item)
    {
        var stackCount = System.Math.Max(1, item.StackCount);
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return stackCount > 1 ? $"{stackCount}x {template.DisplayName}" : template.DisplayName;
        }

        return stackCount > 1 ? $"{stackCount}x {item.TemplateId}" : item.TemplateId;
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            Close();
        }
    }

    private void OnTurnCompleted()
    {
        if (!Visible)
        {
            return;
        }

        if (_gameManager?.World?.GetEntity(_chestId)?.GetComponent<ChestComponent>() is null)
        {
            Close();
            return;
        }

        RebuildChestOptions();
        RebuildMenuText();
    }

    private string ResolveChestTitle()
    {
        var chest = _gameManager?.World?.GetEntity(_chestId);
        return string.IsNullOrWhiteSpace(chest?.Name) ? "CHEST" : chest!.Name.ToUpperInvariant();
    }
}
