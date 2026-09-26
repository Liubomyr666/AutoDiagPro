using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Label _vehicle = new() { Text = "—", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
    private readonly Label _dtc = new() { Text = "—", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
    private readonly Label _lastScan = new() { Text = "—", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
    private readonly Label _orders = new() { Text = "—", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
    private readonly Label _sync = Theme.MutedText("Синхронизация...");

    public DashboardPage()
    {
        Title = "Главная";
        BackgroundColor = Theme.Page;
        Content = new RefreshView
        {
            Command = new Command(async () => await LoadAsync()),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 24, 18, 36),
                    Spacing = 14,
                    Children = { BuildHeader(), BuildStatusGrid(), BuildQuickActions(), BuildInfo() }
                }
            }
        };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View BuildHeader()
    {
        var session = _api.Session;
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Theme.H1($"Привет, {session?.DisplayName ?? "клиент"}"),
                Theme.MutedText("AutoDiag Pro • мобильная панель автомобиля"),
                _sync
            }
        };
    }

    private View BuildStatusGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        grid.Add(Kpi("МОЁ АВТО", _vehicle), 0, 0);
        grid.Add(Kpi("ОШИБКИ DTC", _dtc), 1, 0);
        grid.Add(Kpi("ПОСЛЕДНИЙ SCAN", _lastScan), 0, 1);
        grid.Add(Kpi("ЗАКАЗЫ", _orders), 1, 1);
        return grid;
    }
    private static View Kpi(string title, Label value) =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = title, FontSize = 10, TextColor = Theme.Muted },
                value
            }
        }, new Thickness(14));

    private View BuildQuickActions()
    {
        var diag = ActionButton("Запустить диагностику", Theme.Accent);
        diag.Clicked += async (_, _) => await Shell.Current.GoToAsync("//diagnostics");

        var cars = ActionButton("Мои автомобили", Color.FromArgb("#1B242A"));
        cars.TextColor = Theme.Text;
        cars.Clicked += async (_, _) => await Shell.Current.GoToAsync("//vehicles");

        var ai = ActionButton("AI помощник", Color.FromArgb("#1B242A"));
        ai.TextColor = Theme.Text;
        ai.Clicked += async (_, _) => await Shell.Current.GoToAsync("//ai");

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "БЫСТРЫЙ СТАРТ", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                diag, cars, ai
            }
        });
    }
    private static Button ActionButton(string text, Color background) =>
        new()
        {
            Text = text,
            BackgroundColor = background,
            TextColor = Color.FromArgb("#111315"),
            CornerRadius = 12,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };

    private static View BuildInfo() =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Клиентский режим", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText("На iPhone доступны безопасная диагностика, Live Data, DTC, VIN, история, сервисные данные и AI. Запись в ECU и программирование не выполняются из клиентского режима.")
            }
        });

    private async Task LoadAsync()
    {
        try
        {
            var vehicles = await _api.GetVehiclesAsync();
            var scans = await _api.GetScansAsync();
            var orders = await _api.GetWorkOrdersAsync();

            _state.Vehicles = vehicles;
            _state.SelectedVehicle ??= vehicles.FirstOrDefault();
            _state.LastSyncUtc = DateTimeOffset.UtcNow;
            var selected = _state.SelectedVehicle;
            _vehicle.Text = selected?.DisplayName ?? "Не привязано";

            var relevantScans = selected is null
                ? scans
                : scans.Where(x => x.VehicleId == selected.Id || (!string.IsNullOrWhiteSpace(selected.Vin) && x.Vin == selected.Vin)).ToList();

            var last = relevantScans.OrderByDescending(x => x.ScannedAt).FirstOrDefault();
            _dtc.Text = last is null ? "Нет scan" : last.DtcCount == 0 ? "Ошибок нет" : $"{last.DtcCount} ошибок";
            _dtc.TextColor = last?.DtcCount > 0 ? Theme.Accent : Theme.Green;
            _lastScan.Text = last is null ? "Ещё не было" : last.ScannedAt.LocalDateTime.ToString("dd.MM • HH:mm");

            var activeOrders = selected is null
                ? orders.Count
                : orders.Count(x => x.VehicleId == selected.Id && !string.Equals(x.Status, "Выдано", StringComparison.OrdinalIgnoreCase));
            _orders.Text = activeOrders.ToString();
            _sync.Text = $"Синхронизировано • {DateTime.Now:HH:mm}";
            _sync.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _sync.Text = "Синхронизация: " + ex.Message;
            _sync.TextColor = Theme.Red;
        }
    }
}