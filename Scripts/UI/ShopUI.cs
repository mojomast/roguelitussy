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
    private const int VisibleEntryRows = 10;

    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private Panel? _panel;
    private ColorRect? _background;
    private Label? _header;
    private Label? _balance;
    private Label? _footer;
    private Control? _list;
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
            _eventBus.EmitLogMessage(message, LogCategory.Warning);
        }

        ClampSelection();
        RefreshVisualState();
    }

    private void EnsureVisuals()
    {
        if (_panel is not null)
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
        _background = new ColorRect { Name = "Background", Color = UiStyle.PanelBlack() };
        _header = new Label { Name = "Header", Modulate = UiStyle.BrightGold() };
        _balance = new Label { Name = "Balance", Modulate = UiStyle.Parchment() };
        _footer = new Label { Name = "Footer", Modulate = UiStyle.MutedText() };
        _list = new Control { Name = "Entries" };
        UiStyle.ConfigureSingleLineLabel(_header, 18);
        UiStyle.ConfigureSingleLineLabel(_balance);
        UiStyle.ConfigureSingleLineLabel(_footer);
        _panel.AddChild(_background);
        _panel.AddChild(_header);
        _panel.AddChild(_balance);
        _panel.AddChild(_list);
        _panel.AddChild(_footer);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _background is null || _header is null || _balance is null || _footer is null || _list is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        _panel.Visible = Visible;
        _background.Size = panelSize;
        var width = System.Math.Max(0f, panelSize.X - PanelPadding * 2f);
        _header.Position = new Vector2(PanelPadding, 12f);
        _header.Size = new Vector2(width, 28f);
        _header.Text = _gameManager?.World?.GetEntity(_merchantId)?.Name ?? "Trade unavailable";
        _balance.Position = new Vector2(PanelPadding, 42f);
        _balance.Size = new Vector2(width, 22f);
        _balance.Text = $"Gold: {_gameManager?.World?.Player?.GetComponent<WalletComponent>()?.Gold ?? 0}    Mode: {_mode}";
        _footer.Position = new Vector2(PanelPadding, panelSize.Y - 38f);
        _footer.Size = new Vector2(width, 22f);
        _footer.Text = "Up/Down choose  Enter trade  Tab buy/sell  Esc/F close";
        _list.Position = new Vector2(PanelPadding, 76f);
        _list.Size = new Vector2(width, System.Math.Max(0f, _footer.Position.Y - 12f - _list.Position.Y));
        foreach (var child in _list.GetChildren().ToArray())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        var window = ResolveVisibleWindow(ResolveEntryCount());
        for (var index = window.Start; index < window.End; index++)
        {
            var entry = ResolveEntryText(index);
            var row = new ColorRect
            {
                Name = $"Entry_{index}",
                Position = new Vector2(0f, (index - window.Start) * 28f),
                Size = new Vector2(width, 28f),
                Color = index == _selectedIndex ? UiStyle.SlotSelected() : UiStyle.PanelInner(),
            };
            var text = new Label
            {
                Name = "EntryText",
                Position = new Vector2(8f, 3f),
                Size = new Vector2(System.Math.Max(0f, width - 178f), 22f),
                Text = entry.Name,
                Modulate = index == _selectedIndex ? UiStyle.BrightGold() : UiStyle.Parchment(),
            };
            UiStyle.ConfigureSingleLineLabel(text);
            row.AddChild(text);
            var price = new Label
            {
                Name = "PriceText",
                Position = new Vector2(System.Math.Max(8f, width - 162f), 3f),
                Size = new Vector2(154f, 22f),
                Text = entry.Price,
                Modulate = entry.Color,
            };
            UiStyle.ConfigureSingleLineLabel(price);
            row.AddChild(price);
            _list.AddChild(row);
        }
    }

    private (string Name, string Price, Color Color) ResolveEntryText(int index)
    {
        var world = _gameManager!.World!;
        var merchant = world.GetEntity(_merchantId)!.GetComponent<MerchantComponent>()!;
        var item = _mode == ShopMode.Sell ? world.Player!.GetComponent<InventoryComponent>()!.Items[index] : null;
        var id = item?.TemplateId ?? merchant.Offers[index].ItemTemplateId;
        var template = _content is not null && _content.TryGetItemTemplate(id, out var resolved) ? resolved : null;
        var name = template is null ? id : ItemRarityPresentation.ResolveDecoratedName(template.DisplayName, template.Rarity);
        var price = item is null ? _gameManager!.ResolveMerchantBuyPrice(merchant.Offers[index].Price) : System.Math.Max(1, (template?.Value ?? 2) / 2);
        var soldOut = item is null && merchant.Offers[index].Quantity <= 0;
        var quantity = item is null ? (soldOut ? "sold out" : $"qty {merchant.Offers[index].Quantity}") : $"x{item.StackCount}";
        var color = soldOut ? UiStyle.FaintText()
            : item is null && (world.Player!.GetComponent<WalletComponent>()?.Gold ?? 0) < price ? UiStyle.DangerRed() : UiStyle.BrightGold();
        return ($"{(index == _selectedIndex ? ">" : " ")} {index + 1}. {name}", $"{price}g  {quantity}", color);
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
        builder.AppendLine($"[b][color={UiStyle.ToHex(UiStyle.BrightGold())}]{ItemRarityPresentation.EscapeBBCode(merchant.Name)}[/color][/b]");
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.ActiveGreen())}]{ItemRarityPresentation.EscapeBBCode($"Gold: {wallet.Gold}")}[/color]    [color={UiStyle.ToHex(UiStyle.MutedText())}]{ItemRarityPresentation.EscapeBBCode($"Mode: {_mode}")}[/color]");
        builder.AppendLine();

        if (_mode == ShopMode.Buy)
        {
            var window = ResolveVisibleWindow(merchantStock.Offers.Count);
            AppendWindowPrefix(builder, window.Start);
            for (var index = window.Start; index < window.End; index++)
            {
                var offer = merchantStock.Offers[index];
                var name = _content is not null && _content.TryGetItemTemplate(offer.ItemTemplateId, out var template)
                    ? ItemRarityPresentation.ResolveDecoratedName(template.DisplayName, template.Rarity)
                    : offer.ItemTemplateId;
                var price = _gameManager?.ResolveMerchantBuyPrice(offer.Price) ?? offer.Price;
                var marker = index == _selectedIndex ? ">" : " ";
                var suffix = offer.Quantity > 0 ? $"qty {offer.Quantity}" : "sold out";
                var color = offer.Quantity > 0 ? UiStyle.ToHex(index == _selectedIndex ? UiStyle.BrightGold() : UiStyle.Parchment()) : UiStyle.ToHex(UiStyle.FaintText());
                var priceColor = wallet.Gold >= price ? UiStyle.ToHex(UiStyle.BrightGold()) : UiStyle.ToHex(UiStyle.DangerRed());
                builder.AppendLine($"[color={color}]{ItemRarityPresentation.EscapeBBCode($"{marker} {name}  ")}[/color][color={priceColor}]{price}g[/color][color={color}]{ItemRarityPresentation.EscapeBBCode($"  {suffix}")}[/color]");
            }

            AppendWindowSuffix(builder, window.End, merchantStock.Offers.Count);
        }
        else
        {
            var window = ResolveVisibleWindow(inventory.Items.Count);
            AppendWindowPrefix(builder, window.Start);
            for (var index = window.Start; index < window.End; index++)
            {
                var item = inventory.Items[index];
                var name = _content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template)
                    ? ItemRarityPresentation.ResolveDecoratedName(template.DisplayName, template.Rarity)
                    : item.TemplateId;
                var sellPrice = _content is not null && _content.TryGetItemTemplate(item.TemplateId, out var pricedTemplate)
                    ? System.Math.Max(1, pricedTemplate.Value / 2)
                    : 1;
                var quantityText = item.StackCount > 1 ? $"x{item.StackCount}" : string.Empty;
                var marker = index == _selectedIndex ? ">" : " ";
                var color = index == _selectedIndex ? UiStyle.ToHex(UiStyle.BrightGold()) : UiStyle.ToHex(UiStyle.Parchment());
                builder.AppendLine($"[color={color}]{ItemRarityPresentation.EscapeBBCode($"{marker} {name} {quantityText}  ")}[/color][color={UiStyle.ToHex(UiStyle.BrightGold())}]{sellPrice}g[/color]");
            }

            AppendWindowSuffix(builder, window.End, inventory.Items.Count);
        }

        builder.AppendLine();
        builder.Append($"[i][color={UiStyle.ToHex(UiStyle.FaintText())}]{ItemRarityPresentation.EscapeBBCode("Up/Down: choose  Enter/B/S: trade  Tab: buy/sell  Esc/F: close")}[/color][/i]");
        return builder.ToString().TrimEnd();
    }

    private static Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
    }

    private (int Start, int End) ResolveVisibleWindow(int count)
    {
        var rows = System.Math.Max(1, System.Math.Min(VisibleEntryRows,
            (int)System.Math.Floor((ResolvePanelSize(ResolveViewportSize()).Y - 126f) / 28f)));
        if (count <= rows)
        {
            return (0, count);
        }

        var start = System.Math.Clamp(_selectedIndex - (rows / 2), 0, count - rows);
        return (start, start + rows);
    }

    private static void AppendWindowPrefix(StringBuilder builder, int start)
    {
        if (start > 0)
        {
            builder.AppendLine("...");
        }
    }

    private static void AppendWindowSuffix(StringBuilder builder, int end, int count)
    {
        if (end < count)
        {
            builder.AppendLine("...");
        }
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}
