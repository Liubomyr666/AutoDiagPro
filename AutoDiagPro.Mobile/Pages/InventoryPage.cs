using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class InventoryPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly VerticalStackLayout _list = new() { Spacing = 9 };
    private readonly VerticalStackLayout _summary = new() { Spacing = 8 };
    private readonly Label _status = Theme.MutedText("Склад загружается...");
    private readonly Entry _search = new()
    {
        Placeholder = "Поиск: название / код / автомобиль",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        ReturnType = ReturnType.Search,
        FontAutoScalingEnabled = false
    };

    private List<ServerInventoryRecord> _items = new();

    public InventoryPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Склад";

        var scan = Theme.PrimaryButton("QR / штрихкод");
        scan.Clicked += async (_, _) => await Shell.Current.GoToAsync("qrparts");

        var manual = Theme.SecondaryButton("Добавить вручную");
        manual.Clicked += async (_, _) => await Shell.Current.GoToAsync("qrparts");

        var refresh = Theme.SecondaryButton("Обновить");
        refresh.Clicked += async (_, _) => await LoadAsync();

        _search.TextChanged += (_, _) => Render();

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        actions.Add(scan, 0, 0);
        actions.Add(manual, 1, 0);
        actions.Add(refresh, 0, 1);
        Grid.SetColumnSpan(refresh, 2);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("INVENTORY • AUTODIAG CLOUD"),
                    Theme.H1("Склад запчастей"),
                    Theme.MutedText("Один склад для Windows и iPhone. Изменения остатков синхронизируются через AutoDiag Cloud."),
                    Theme.CardView(actions),
                    Theme.CardView(_summary),
                    _search,
                    _status,
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _status.Text = "Синхронизация склада...";
        _status.TextColor = Theme.Accent;

        try
        {
            _items = (await _api.GetInventoryAsync())
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Code)
                .ToList();

            RenderSummary();
            Render();
            _status.Text = $"AutoDiag Cloud • обновлено {DateTime.Now:HH:mm:ss}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _items.Clear();
            _summary.Clear();
            _list.Clear();
            _status.Text = "Не удалось загрузить склад: " + ex.Message;
            _status.TextColor = Theme.Red;
            _list.Add(Theme.CardView(Theme.MutedText("Проверьте подключение к AutoDiag Cloud.")));
        }
    }

    private void RenderSummary()
    {
        _summary.Clear();

        var total = _items.Sum(x => x.Quantity);
        var empty = _items.Count(x => x.Quantity <= 0);
        var low = _items.Count(x => x.Quantity > 0 && x.Quantity <= 2);

        _summary.Add(Theme.Eyebrow("СОСТОЯНИЕ СКЛАДА"));

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        grid.Add(Kpi("ПОЗИЦИЙ", _items.Count.ToString(), Theme.Text), 0, 0);
        grid.Add(Kpi("ЕДИНИЦ", total.ToString(), Theme.Text), 1, 0);
        grid.Add(Kpi("МАЛО", low.ToString(), low > 0 ? Theme.Accent : Theme.Green), 0, 1);
        grid.Add(Kpi("НЕТ В НАЛИЧИИ", empty.ToString(), empty > 0 ? Theme.Red : Theme.Green), 1, 1);
        _summary.Add(grid);
    }

    private void Render()
    {
        _list.Clear();

        var q = (_search.Text ?? "").Trim();
        var visible = string.IsNullOrWhiteSpace(q)
            ? _items
            : _items.Where(x =>
                    x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    VehicleName(x.VehicleId).Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (visible.Count == 0)
        {
            _list.Add(Theme.CardView(Theme.MutedText(
                _items.Count == 0
                    ? "Склад пуст. Примите первую деталь по QR или вручную."
                    : "По этому запросу ничего не найдено.")));
            return;
        }

        foreach (var item in visible)
            _list.Add(Card(item));
    }

    private View Card(ServerInventoryRecord item)
    {
        var minus = Theme.CompactButton("−1");
        var plus = Theme.CompactButton("+1");
        minus.IsEnabled = item.Quantity > 0;

        minus.Clicked += async (_, _) => await AdjustAsync(item, -1);
        plus.Clicked += async (_, _) => await AdjustAsync(item, 1);

        var low = item.Quantity <= 2;
        var stateText = item.Quantity <= 0 ? "НЕТ" : low ? "МАЛО" : "В НАЛИЧИИ";
        var stateColor = item.Quantity <= 0 ? Theme.Red : low ? Theme.Accent : Theme.Green;

        var title = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        title.Add(new Label
        {
            Text = item.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text,
            LineBreakMode = LineBreakMode.TailTruncation,
            FontAutoScalingEnabled = false
        }, 0, 0);
        title.Add(new Label
        {
            Text = item.Quantity.ToString(),
            FontSize = 21,
            FontAttributes = FontAttributes.Bold,
            TextColor = stateColor,
            FontAutoScalingEnabled = false
        }, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                title,
                Theme.Pill(stateText, stateColor),
                Theme.MutedText("Код • " + (string.IsNullOrWhiteSpace(item.Code) ? "—" : item.Code)),
                Theme.MutedText(VehicleName(item.VehicleId)),
                Theme.MutedText(item.LastReceivedAt is null
                    ? "Последняя приёмка • —"
                    : $"Последняя приёмка • {item.LastReceivedAt.Value.LocalDateTime:dd.MM.yyyy HH:mm}"),
                new HorizontalStackLayout { Spacing = 8, Children = { minus, plus } }
            }
        }, new Thickness(13));
    }

    private string VehicleName(Guid? vehicleId)
    {
        if (vehicleId is not Guid id) return "Общий склад";
        return _state.Vehicles.FirstOrDefault(x => x.Id == id)?.DisplayName ?? "Привязано к автомобилю";
    }

    private async Task AdjustAsync(ServerInventoryRecord item, int delta)
    {
        try
        {
            _status.Text = "Сохраняю остаток...";
            _status.TextColor = Theme.Accent;
            var quantity = Math.Max(0, item.Quantity + delta);
            await _api.UpdateInventoryQuantityAsync(item.Id, quantity);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Склад: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View Kpi(string title, string value, Color color) =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Theme.Eyebrow(title),
                new Label
                {
                    Text = value,
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = color,
                    FontAutoScalingEnabled = false
                }
            }
        }, new Thickness(11));
}

internal static class InventoryGridExtensions
{
    public static Grid WithChildren(this Grid grid, View left, View right)
    {
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        return grid;
    }
}
