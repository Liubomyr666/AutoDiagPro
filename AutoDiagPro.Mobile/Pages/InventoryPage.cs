using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class InventoryPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly VerticalStackLayout _list = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Склад загружается...");

    public InventoryPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Склад";

        var scan = Theme.PrimaryButton("Принять по QR / штрихкоду");
        scan.Clicked += async (_, _) => await Shell.Current.GoToAsync("qrparts");

        var refresh = Theme.SecondaryButton("Обновить склад");
        refresh.Clicked += async (_, _) => await LoadAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("INVENTORY • CLOUD"),
                    Theme.H1("Склад запчастей"),
                    Theme.MutedText("Общий облачный склад AutoDiag Pro. QR-приёмка и остатки больше не хранятся только на этом iPhone."),
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Star)
                        },
                        ColumnSpacing = 8
                    }.WithChildren(scan, refresh),
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
        _list.Clear();
        try
        {
            var items = (await _api.GetInventoryAsync())
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Code)
                .ToList();

            _status.Text = $"Облако • позиций {items.Count} • единиц {items.Sum(x => x.Quantity)}";
            _status.TextColor = Theme.Green;

            if (items.Count == 0)
            {
                _list.Add(Theme.CardView(Theme.MutedText("Склад пуст. Примите первую деталь через QR.")));
                return;
            }

            foreach (var item in items)
                _list.Add(Card(item));
        }
        catch (Exception ex)
        {
            _status.Text = "Не удалось загрузить склад: " + ex.Message;
            _status.TextColor = Theme.Red;
            _list.Add(Theme.CardView(Theme.MutedText("Проверьте подключение к AutoDiag Cloud.")));
        }
    }

    private View Card(ServerInventoryRecord item)
    {
        var minus = Theme.CompactButton("−1");
        var plus = Theme.CompactButton("+1");
        minus.IsEnabled = item.Quantity > 0;

        minus.Clicked += async (_, _) => await AdjustAsync(item, -1);
        plus.Clicked += async (_, _) => await AdjustAsync(item, 1);

        var vehicle = item.VehicleId is Guid id
            ? _state.Vehicles.FirstOrDefault(x => x.Id == id)?.DisplayName ?? "Привязано к авто"
            : "Общий склад";

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        grid.Add(new Label
        {
            Text = item.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text,
            LineBreakMode = LineBreakMode.TailTruncation
        }, 0, 0);
        grid.Add(new Label
        {
            Text = item.Quantity.ToString(),
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = item.Quantity > 0 ? Theme.Accent : Theme.Red
        }, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                grid,
                Theme.MutedText("Код • " + item.Code),
                Theme.MutedText(vehicle +
                    (item.LastReceivedAt is null ? "" : $" • приём {item.LastReceivedAt.Value.LocalDateTime:dd.MM HH:mm}")),
                new HorizontalStackLayout { Spacing = 8, Children = { minus, plus } }
            }
        }, new Thickness(13));
    }

    private async Task AdjustAsync(ServerInventoryRecord item, int delta)
    {
        try
        {
            var quantity = Math.Max(0, item.Quantity + delta);
            await _api.UpdateInventoryQuantityAsync(item.Id, quantity);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Склад", ex.Message, "OK");
        }
    }
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
