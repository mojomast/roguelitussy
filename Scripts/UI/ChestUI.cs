using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ChestUI : Control
{
    private const float PanelWidth = 520f;
    private const float PanelHeight = 260f;
    private const float PanelPadding = 18f;
    private const float OuterMargin = 24f;

    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private Panel? _panel;
    private ColorRect? _background;
    private ColorRect? _borderTop;
    private ColorRect? _borderBottom;
    private ColorRect? _borderLeft;
    private ColorRect? _borderRight;
    private UiMouseLabel? _titleLabel;
    private RichTextLabel? _bodyLabel;
    private UiMouseLabel? _takeAllLabel;
    private UiMouseLabel? _closeLabel;
    private EntityId _chestId = EntityId.Invalid;
    private string _statusText = string.Empty;

    public ChestUI()
    {
        Name = "ChestUI";
        Visible = false;
    }

    public EntityId ChestId => _chestId;

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

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

        RefreshVisualState();
    }

    public void Open(EntityId chestId)
    {
        _chestId = chestId;
        _statusText = string.Empty;
        Visible = true;
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        _chestId = EntityId.Invalid;
        _statusText = string.Empty;
        RefreshVisualState();
    }

    public bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
            case Key.KpEnter:
            case Key.T:
            case Key.A:
                TakeAll();
                return true;
            case Key.Escape:
            case Key.F:
                Close();
                return true;
            default:
                return false;
        }
    }

    public string SnapshotBodyMarkup() => BuildBodyMarkup();

    private void TakeAll()
    {
        var world = _gameManager?.World;
        var player = world?.Player;
        var chest = world?.GetEntity(_chestId);
        if (world is null || player is null || chest?.GetComponent<ChestComponent>() is null || _eventBus is null)
        {
            _statusText = "Chest unavailable.";
            RefreshVisualState();
            return;
        }

        var action = new OpenChestAction(player.Id, _chestId);
        if (action.Validate(world) != ActionResult.Success)
        {
            _statusText = "Move closer to open this chest.";
            RefreshVisualState();
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

    private void EnsureVisuals()
    {
        if (_panel is not null && _bodyLabel is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        ZIndex = 98;
        _panel = new Panel { Name = "Panel", Size = panelSize };
        _background = new ColorRect { Name = "Background", Color = UiStyle.PanelBlack(0.98f) };
        _borderTop = new ColorRect { Name = "BorderTop", Color = UiStyle.BorderActive() };
        _borderBottom = new ColorRect { Name = "BorderBottom", Color = UiStyle.BorderActive() };
        _borderLeft = new ColorRect { Name = "BorderLeft", Color = UiStyle.BorderActive() };
        _borderRight = new ColorRect { Name = "BorderRight", Color = UiStyle.BorderActive() };
        _titleLabel = new UiMouseLabel
        {
            Name = "TitleLabel",
            Text = "CHEST",
            Modulate = UiStyle.BrightGold(),
            InputSubmitted = input =>
            {
                if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    TakeAll();
                }
            },
        };
        _bodyLabel = new RichTextLabel
        {
            Name = "BodyLabel",
            BbcodeEnabled = true,
            Modulate = UiStyle.Parchment(),
        };
        _takeAllLabel = new UiMouseLabel
        {
            Name = "TakeAllButton",
            Text = "[TAKE ALL]",
            Modulate = UiStyle.BrightGold(),
            InputSubmitted = input =>
            {
                if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    TakeAll();
                }
            },
        };
        _closeLabel = new UiMouseLabel
        {
            Name = "CloseButton",
            Text = "[CLOSE]",
            Modulate = UiStyle.MutedText(),
            InputSubmitted = input =>
            {
                if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    Close();
                }
            },
        };

        _panel.AddChild(_background);
        _panel.AddChild(_borderTop);
        _panel.AddChild(_borderBottom);
        _panel.AddChild(_borderLeft);
        _panel.AddChild(_borderRight);
        _panel.AddChild(_titleLabel);
        _panel.AddChild(_bodyLabel);
        _panel.AddChild(_takeAllLabel);
        _panel.AddChild(_closeLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();
        if (_panel is null || _bodyLabel is null || _background is null || _borderTop is null || _borderBottom is null || _borderLeft is null || _borderRight is null || _titleLabel is null || _takeAllLabel is null || _closeLabel is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        _panel.Visible = Visible;
        _panel.Modulate = UiStyle.GoldTrim();
        _background.Position = Vector2.Zero;
        _background.Size = panelSize;
        _borderTop.Position = Vector2.Zero;
        _borderTop.Size = new Vector2(panelSize.X, 1f);
        _borderBottom.Position = new Vector2(0f, panelSize.Y - 1f);
        _borderBottom.Size = new Vector2(panelSize.X, 1f);
        _borderLeft.Position = Vector2.Zero;
        _borderLeft.Size = new Vector2(1f, panelSize.Y);
        _borderRight.Position = new Vector2(panelSize.X - 1f, 0f);
        _borderRight.Size = new Vector2(1f, panelSize.Y);

        _titleLabel.Visible = Visible;
        _titleLabel.Position = new Vector2(PanelPadding, 12f);
        _titleLabel.Size = new Vector2(panelSize.X - (PanelPadding * 2f), 24f);
        _titleLabel.Text = ResolveChestTitle();
        _bodyLabel.Visible = Visible;
        _bodyLabel.Position = new Vector2(PanelPadding, 48f);
        _bodyLabel.Size = new Vector2(panelSize.X - (PanelPadding * 2f), panelSize.Y - 96f);
        _bodyLabel.Clear();
        _bodyLabel.AppendText(BuildBodyMarkup());
        _takeAllLabel.Visible = Visible;
        _takeAllLabel.Position = new Vector2(PanelPadding, panelSize.Y - 38f);
        _takeAllLabel.Size = new Vector2(130f, 22f);
        _closeLabel.Visible = Visible;
        _closeLabel.Position = new Vector2(panelSize.X - PanelPadding - 110f, panelSize.Y - 38f);
        _closeLabel.Size = new Vector2(110f, 22f);
    }

    private string BuildBodyMarkup()
    {
        var chest = _gameManager?.World?.GetEntity(_chestId);
        var chestComponent = chest?.GetComponent<ChestComponent>();
        if (chest is null || chestComponent is null)
        {
            return ItemRarityPresentation.EscapeBBCode("Chest unavailable.");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.MutedText())}]Container[/color] [color={UiStyle.ToHex(UiStyle.Parchment())}]{ItemRarityPresentation.EscapeBBCode(chest.Name)}[/color]");
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.MutedText())}]Loot source[/color] [color={UiStyle.ToHex(UiStyle.Parchment())}]{ItemRarityPresentation.EscapeBBCode(chestComponent.LootTableId)}[/color]");
        builder.AppendLine();
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.Parchment())}]This chest rolls its contents when opened. Loot that fits is stowed; overflow spills onto the floor.[/color]");
        builder.AppendLine();
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.BrightGold())}]TAKE ALL[/color] [color={UiStyle.ToHex(UiStyle.MutedText())}]Open chest and collect loot.[/color]");
        if (!string.IsNullOrWhiteSpace(_statusText))
        {
            builder.AppendLine();
            builder.Append($"[color={UiStyle.ToHex(UiStyle.DangerRed())}]{ItemRarityPresentation.EscapeBBCode(_statusText)}[/color]");
        }

        return builder.ToString().TrimEnd();
    }

    private string ResolveChestTitle()
    {
        var chest = _gameManager?.World?.GetEntity(_chestId);
        return string.IsNullOrWhiteSpace(chest?.Name) ? "CHEST" : chest!.Name.ToUpperInvariant();
    }

    private static Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        var desired = new Vector2(PanelWidth, System.Math.Min(PanelHeight, viewportSize.Y * 0.70f));
        return OverlayLayoutHelper.FitPanelSize(viewportSize, desired, OuterMargin);
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}
