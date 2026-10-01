using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Label _vehicle = ValueLabel();
    private readonly Label _dtc = ValueLabel();
    private readonly Label _lastScan = ValueLabel();
    private readonly Label _orders = ValueLabel();
    private readonly Label _shopActive = ValueLabel();
    private readonly Label _shopReady = ValueLabel();
    private readonly Label _shopUnpaid = ValueLabel();
    private readonly Label _shopRevenue = ValueLabel();
    private readonly Label _sync = Theme.MutedText("Синхронизация...");

    public DashboardPage()
    {
        Title = "Главная";
        BackgroundColor = Theme.Page;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    BuildTopBar(),
                    BuildHero(),
                    BuildKpis(),
                    BuildWorkshopKpis(),
                    BuildQuickActions(),
                    BuildWorkspace()
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View BuildTopBar()
    {
        var session = _api.Session;
        var settings = Theme.CompactButton("Настройки");
        settings.Clicked += async (_, _) => await Shell.Current.GoToAsync("settings");

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        grid.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                Theme.Eyebrow("AUTODIAG PRO • IOS"),
                Theme.H1($"Привет, {session?.DisplayName ?? "клиент"}"),
                _sync
            }
        }, 0, 0);
        grid.Add(settings, 1, 0);
        return grid;
    }

    private View BuildHero()
    {
        var scan = Theme.PrimaryButton("Полная диагностика");
        scan.Clicked += async (_, _) => await Shell.Current.GoToAsync("//diagnostics");

        var car = Theme.SecondaryButton("Автомобиль");
        car.Clicked += async (_, _) => await Shell.Current.GoToAsync("//vehicles");

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(18),
            Spacing = 10,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("VEHICLE SESSION"),
                new Label { Text = "Диагностика, ремонт и сервис\nв одном приложении", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = "VIN • DTC • Live Data • AI • детали • СТО", FontSize = 12, TextColor = Color.FromArgb("#D1D7DB") },
                new HorizontalStackLayout { Spacing = 8, Children = { scan, car } }
            }
        };

        var grid = new Grid { HeightRequest = 260 };
        grid.Add(new Image { Source = "hero_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Color.FromArgb("#070B10"), Opacity = 0.60 });
        grid.Add(content);

        return new Border
        {
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = grid
        };
    }

    private View BuildKpis()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        grid.Add(Kpi("ТЕКУЩЕЕ АВТО", _vehicle), 0, 0);
        grid.Add(Kpi("ОШИБКИ DTC", _dtc), 1, 0);
        grid.Add(Kpi("ПОСЛЕДНИЙ SCAN", _lastScan), 0, 1);
        grid.Add(Kpi("ЗАКАЗ-НАРЯДЫ", _orders), 1, 1);
        return grid;
    }

    private static View Kpi(string title, Label value) => Theme.CardView(
        new VerticalStackLayout { Spacing = 7, Children = { Theme.Eyebrow(title), value } },
        new Thickness(14));

    private View BuildWorkshopKpis()
    {
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
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        grid.Add(Kpi("В РАБОТЕ", _shopActive), 0, 0);
        grid.Add(Kpi("ГОТОВО К ВЫДАЧЕ", _shopReady), 1, 0);
        grid.Add(Kpi("НЕОПЛАЧЕНО", _shopUnpaid), 0, 1);
        grid.Add(Kpi("ВЫРУЧКА СЕГОДНЯ", _shopRevenue), 1, 1);

        return new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.H2("СТО сегодня"),
                grid
            }
        };
    }

    private View BuildQuickActions()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        grid.Add(ActionCard("Диагностика", "Full scan • DTC • Live", async () => await Shell.Current.GoToAsync("//diagnostics")), 0, 0);
        grid.Add(ActionCard("AI помощник", "Ремонт • детали • цены", async () => await Shell.Current.GoToAsync("ai")), 1, 0);
        grid.Add(ActionCard("Управление СТО", "Заказы • CRM • склад", async () => await Shell.Current.GoToAsync("//workshop")), 0, 1);
        grid.Add(ActionCard("Все функции", "Coding • Keys • Battery", async () => await Shell.Current.GoToAsync("//more")), 1, 1);

        return new VerticalStackLayout
        {
            Spacing = 9,
            Children = { Theme.H2("Быстрые действия"), grid }
        };
    }

    private static View ActionCard(string title, string subtitle, Func<Task> action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await action();

        var card = Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText(subtitle),
                new Label { Text = "Открыть  ›", FontSize = 12, TextColor = Theme.Accent, FontAttributes = FontAttributes.Bold }
            }
        }, new Thickness(14));
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private View BuildWorkspace()
    {
        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Theme.Eyebrow("WORKSPACE"),
                Theme.H2("Как на ПК — адаптировано под iPhone"),
                Theme.Body("На мобильной версии собраны автомобиль, диагностика, Repair Brain/AI, сервис, детали и клиентская часть СТО. Опасные операции записи ECU запускаются только там, где есть совместимый интерфейс и подтверждение пользователя."),
                Theme.Pill("SERVER SYNC", Theme.Green)
            }
        });
    }

    private async Task LoadAsync()
    {
        try
        {
            var vehiclesTask = _api.GetVehiclesAsync();
            var scansTask = _api.GetScansAsync();
            var ordersTask = _api.GetWorkOrdersAsync();
            var dashboardTask = _api.GetDashboardAsync();
            await Task.WhenAll(vehiclesTask, scansTask, ordersTask, dashboardTask);

            var vehicles = await vehiclesTask;
            var scans = await scansTask;
            var orders = await ordersTask;
            var dashboard = await dashboardTask;
            _state.Vehicles = vehicles;
            _state.SelectedVehicle ??= vehicles.FirstOrDefault();
            _state.LastSyncUtc = DateTimeOffset.UtcNow;

            var selected = _state.SelectedVehicle;
            _vehicle.Text = selected?.DisplayName ?? "Не выбрано";
            _vehicle.FontSize = selected is null ? 17 : 14;

            var relevantScans = selected is null
                ? scans
                : scans.Where(x => x.VehicleId == selected.Id || (!string.IsNullOrWhiteSpace(selected.Vin) && x.Vin == selected.Vin)).ToList();
            var last = relevantScans.OrderByDescending(x => x.ScannedAt).FirstOrDefault();

            _dtc.Text = last is null ? "—" : last.DtcCount == 0 ? "0" : last.DtcCount.ToString();
            _dtc.TextColor = last?.DtcCount > 0 ? Theme.Accent : Theme.Green;
            _lastScan.Text = last is null ? "Нет данных" : last.ScannedAt.LocalDateTime.ToString("dd.MM • HH:mm");

            var activeOrders = selected is null
                ? orders.Count(x => !string.Equals(x.Status, "Выдано", StringComparison.OrdinalIgnoreCase))
                : orders.Count(x => x.VehicleId == selected.Id && !string.Equals(x.Status, "Выдано", StringComparison.OrdinalIgnoreCase));
            _orders.Text = activeOrders.ToString();

            _shopActive.Text = dashboard.ActiveOrders.ToString();
            _shopReady.Text = dashboard.ReadyOrders.ToString();
            _shopUnpaid.Text = dashboard.UnpaidInvoices.ToString();
            _shopRevenue.Text = dashboard.PaidToday > 0 ? $"{dashboard.PaidToday:N2} €" : "0 €";
            _shopReady.TextColor = dashboard.ReadyOrders > 0 ? Theme.Green : Theme.Text;
            _shopUnpaid.TextColor = dashboard.UnpaidInvoices > 0 ? Theme.Accent : Theme.Green;

            _sync.Text = $"ONLINE • синхронизировано {DateTime.Now:HH:mm}";
            _sync.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _sync.Text = "Синхронизация: " + ex.Message;
            _sync.TextColor = Theme.Red;
        }
    }

    private static Label ValueLabel() => new()
    {
        Text = "—",
        FontSize = 19,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text,
        LineBreakMode = LineBreakMode.TailTruncation
    };
}
