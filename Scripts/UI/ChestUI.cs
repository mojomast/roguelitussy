using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ChestUI : MenuBase
{
    private enum ChestAction
    {
        TakeAll,
        Close,
    }

    private readonly System.Collections.Generic.List<ChestAction> _actions = new();
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
        ConfigureChestOptions();
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
        Title = ResolveChestTitle();
        SetSelection(0);
        RebuildMenuText();
        base.Open();
    }

    public override void Close()
    {
        base.Close();
        _chestId = EntityId.Invalid;
        _statusText = string.Empty;
        Title = "CHEST";
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
        builder.AppendLine("This chest rolls its contents when opened. Loot that fits is stowed; overflow spills onto the floor.");
        builder.AppendLine();
        builder.AppendLine("TAKE ALL    Open chest and collect loot.");
        if (!string.IsNullOrWhiteSpace(_statusText))
        {
            builder.AppendLine();
            builder.Append(_statusText);
        }

        return builder.ToString().TrimEnd();
    }

    protected override string BuildFooterText()
    {
        return "Up/Down choose  Enter confirm  T/A take all  F/Esc close";
    }

    protected override void ActivateSelected()
    {
        var action = SelectedIndex >= 0 && SelectedIndex < _actions.Count ? _actions[SelectedIndex] : ChestAction.Close;
        if (action == ChestAction.TakeAll)
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

    protected override void OnVisualStateRefreshed(Panel panel, Label label, Vector2 viewportSize, Vector2 panelSize)
    {
        EnsureTitleClickTarget(panel);
    }

    private void ConfigureChestOptions()
    {
        _actions.Clear();
        ConfigureOptions();
        AddOption("Take All", ChestAction.TakeAll);
        AddOption("Close", ChestAction.Close);
    }

    private void AddOption(string label, ChestAction action)
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
        var world = _gameManager?.World;
        var player = world?.Player;
        var chest = world?.GetEntity(_chestId);
        if (world is null || player is null || chest?.GetComponent<ChestComponent>() is null || _eventBus is null)
        {
            _statusText = "Chest unavailable.";
            RebuildMenuText();
            return;
        }

        var action = new OpenChestAction(player.Id, _chestId);
        if (action.Validate(world) != ActionResult.Success)
        {
            _statusText = "Move closer to open this chest.";
            RebuildMenuText();
            return;
        }

        _eventBus.EmitPlayerActionSubmitted(action);
        Close();
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
        if (Visible && _gameManager?.World?.GetEntity(_chestId)?.GetComponent<ChestComponent>() is null)
        {
            Close();
        }
    }

    private string ResolveChestTitle()
    {
        var chest = _gameManager?.World?.GetEntity(_chestId);
        return string.IsNullOrWhiteSpace(chest?.Name) ? "CHEST" : chest!.Name.ToUpperInvariant();
    }
}
