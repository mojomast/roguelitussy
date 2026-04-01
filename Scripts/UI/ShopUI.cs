using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ShopUI : Control
{
    private enum ShopMode
    {
        Buy,
        Sell,
    }

    private const float PanelWidth = 920f;
    private const float PanelHeight = 420f;
    private const float PanelPadding = 18f;
    private const float OuterMargin = 24f;

    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private Panel? _panel;
    private RichTextLabel? _label;
    private EntityId _merchantId = EntityId.Invalid;
    private int _selectedIndex;
    private ShopMode _mode;

    public ShopUI()
    {
        Name = "ShopUI";
        Visible = false;
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.CurrencyChanged -= OnCurrencyChanged;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;

        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.CurrencyChanged += OnCurrencyChanged;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        RefreshVisualState();
    }

    public void Open(EntityId merchantId)
    {
        _merchantId = merchantId;
        _selectedIndex = 0;
        _mode = ShopMode.Buy;
        Visible = true;
        ClampSelection();
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        _merchantId = EntityId.Invalid;
        _selectedIndex = 0;
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
            case Key.Up:
                MoveSelection(-1);
                return true;
            case Key.Down:
                MoveSelection(1);
                return true;
            case Key.Tab:
                _mode = _mode == ShopMode.Buy ? ShopMode.Sell : ShopMode.Buy;
                _selectedIndex = 0;
                RefreshVisualState();
                return true;
            case Key.Enter:
            case Key.KpEnter:
            case Key.B:
            case Key.S:
                SubmitTrade();
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

    private void OnInventoryChanged(EntityId entityId)
    {
        if (Visible && _gameManager?.World?.Player?.Id == entityId)
        {
            ClampSelection();
            RefreshVisualState();
        }
    }

    private void OnCurrencyChanged(EntityId entityId, int gold)
    {
        if (Visible && _gameManager?.World?.Player?.Id == entityId)
        {
            RefreshVisualState();
        }
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            Close();
        }
    }

    private void MoveSelection(int delta)
    {
        var count = ResolveEntryCount();
        if (count <= 0)
        {
            _selectedIndex = 0;
            RefreshVisualState();
            return;
        }

        _selectedIndex = (_selectedIndex + delta + count) % count;
        RefreshVisualState();
    }

    private void ClampSelection()
    {
        var count = ResolveEntryCount();
        if (count <= 0)
        {
            _selectedIndex = 0;
        }
        else if (_selectedIndex >= count)
        {
            _selectedIndex = count - 1;
        }
    }

    private int ResolveEntryCount()
    {
        if (_gameManager?.World?.GetEntity(_merchantId)?.GetComponent<MerchantComponent>() is not { } merchant)
        {
            return 0;
        }

        return _mode == ShopMode.Buy
            ? merchant.Offers.Count
            : (_gameManager.World.Player.GetComponent<InventoryComponent>()?.Items.Count ?? 0);
    }

    private void SubmitTrade()
    {
        if (_gameManager is null || _eventBus is null)
        {
            return;
        }

        string message;
        var success = false;
        if (_mode == ShopMode.Buy)
        {
            success = _gameManager.TryBuyMerchantOffer(_merchantId, _selectedIndex, out message);
        }
        else
        {
            var inventory = _gameManager.World?.Player?.GetComponent<InventoryComponent>();
            if (inventory is null || _selectedIndex < 0 || _selectedIndex >= inventory.Items.Count)
            {
                message = "There is nothing to sell.";
            }
            else
            {
                success = _gameManager.TrySellItemToMerchant(_merchantId, inventory.Items[_selectedIndex].InstanceId, out message);
            }
        }

        if (!success)
        {
            _eventBus.EmitLogMessage(message);
        }

        ClampSelection();
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
        ZIndex = 97;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _label = new RichTextLabel
        {
            Name = "Label",
            Position = new Vector2(PanelPadding, PanelPadding),
            BbcodeEnabled = true,
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
        _panel.Visible = Visible;
        _label.Visible = Visible;
        _label.Position = new Vector2(PanelPadding, PanelPadding);
        _label.Size = new Vector2(
            System.Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
            System.Math.Max(0f, panelSize.Y - (PanelPadding * 2f)));
        _label.Clear();
        _label.AppendText(BuildBodyMarkup());
    }

    private string BuildBodyMarkup()
    {
        if (_gameManager?.World is null)
        {
            return string.Empty;
        }

        var player = _gameManager.World.Player;
        if (player is null)
        {
            return "Trade unavailable.";
        }

        var merchant = _gameManager.World.GetEntity(_merchantId);
        var merchantStock = merchant?.GetComponent<MerchantComponent>();
        var inventory = player.GetComponent<InventoryComponent>();
        var wallet = player.GetComponent<WalletComponent>();
        if (merchant is null || merchantStock is null || inventory is null || wallet is null)
        {
            return "Trade unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"[b]{ItemRarityPresentation.EscapeBBCode(merchant.Name)}[/b]");
        builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"Gold: {wallet.Gold}    Mode: {_mode}"));
        builder.AppendLine();

        if (_mode == ShopMode.Buy)
        {
            for (var index = 0; index < merchantStock.Offers.Count; index++)
            {
                var offer = merchantStock.Offers[index];
                var name = _content is not null && _content.TryGetItemTemplate(offer.ItemTemplateId, out var template)
                    ? template.DisplayName
                    : offer.ItemTemplateId;
                var price = _gameManager?.ResolveMerchantBuyPrice(offer.Price) ?? offer.Price;
                var marker = index == _selectedIndex ? ">" : " ";
                var suffix = offer.Quantity > 0 ? $"qty {offer.Quantity}" : "sold out";
                builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"{marker} {name}  {price}g  {suffix}"));
            }
        }
        else
        {
            for (var index = 0; index < inventory.Items.Count; index++)
            {
                var item = inventory.Items[index];
                var name = _content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template)
                    ? template.DisplayName
                    : item.TemplateId;
                var sellPrice = _content is not null && _content.TryGetItemTemplate(item.TemplateId, out var pricedTemplate)
                    ? System.Math.Max(1, pricedTemplate.Value / 2)
                    : 1;
                var quantityText = item.StackCount > 1 ? $"x{item.StackCount}" : string.Empty;
                var marker = index == _selectedIndex ? ">" : " ";
                builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"{marker} {name} {quantityText}  {sellPrice}g"));
            }
        }

        builder.AppendLine();
        builder.Append(ItemRarityPresentation.EscapeBBCode("Up/Down: choose  Enter/B/S: trade  Tab: buy/sell  Esc/F: close"));
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