using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class InventoryPage : ContentPage
{
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
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

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("INVENTORY"),
                    Theme.H1("Склад запчастей"),
                    Theme.MutedText("Поступления с QR-приёмки группируются по коду. Можно скорректировать остаток прямо на iPhone."),
                    scan,
                    _status,
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _list.Clear();
        var db = await _store.LoadAsync();

        var groups = db.ReceivedParts
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Code) ? x.Name : x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Key = g.Key,
                Name = g.OrderByDescending(x => x.ReceivedAt).First().Name,
                Quantity = g.Sum(x => x.Quantity),
                VehicleId = g.OrderByDescending(x => x.ReceivedAt).First().VehicleId,
                Last = g.Max(x => x.ReceivedAt)
            })
            .OrderBy(x => x.Name)
            .ToList();

        _status.Text = $"Позиций: {groups.Count} • единиц: {groups.Sum(x => x.Quantity)}";
        _status.TextColor = Theme.Green;

        if (groups.Count == 0)
        {
            _list.Add(Theme.CardView(Theme.MutedText("Склад пуст. Используйте QR-приёмку или добавьте деталь там вручную.")));
            return;
        }

        foreach (var item in groups)
        {
            var minus = Theme.CompactButton("−1");
            minus.Clicked += async (_, _) => await AdjustAsync(item.Key, -1);

            var plus = Theme.CompactButton("+1");
            plus.Clicked += async (_, _) => await AdjustAsync(item.Key, 1);

            var vehicle = _state.Vehicles.FirstOrDefault(x => x.Id == item.VehicleId)?.DisplayName ?? "Общий склад";

            _list.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        ColumnSpacing = 10
                    }.WithChildren(
                        new Label { Text = item.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                        new Label { Text = item.Quantity.ToString(), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Theme.Accent }
                    ),
                    Theme.MutedText(item.Key),
                    Theme.MutedText($"{vehicle} • последнее поступление {item.Last.LocalDateTime:dd.MM HH:mm}"),
                    new HorizontalStackLayout { Spacing = 8, Children = { minus, plus } }
                }
            }, new Thickness(13)));
        }
    }

    private async Task AdjustAsync(string key, int delta)
    {
        var db = await _store.LoadAsync();
        var matches = db.ReceivedParts
            .Where(x => string.Equals(string.IsNullOrWhiteSpace(x.Code) ? x.Name : x.Code, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReceivedAt)
            .ToList();

        if (delta > 0)
        {
            var source = matches.FirstOrDefault();
            if (source is null) return;
            db.ReceivedParts.Add(new ReceivedPartMobile
            {
                VehicleId = source.VehicleId,
                Code = source.Code,
                Name = source.Name,
                Quantity = 1
            });
        }
        else
        {
            var source = matches.FirstOrDefault(x => x.Quantity > 0);
            if (source is null) return;
            source.Quantity -= 1;
            if (source.Quantity <= 0)
                db.ReceivedParts.Remove(source);
        }

        await _store.SaveAsync(db);
        await LoadAsync();
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