using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ClientDashboardPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly Label _vehicle = Value();
    private readonly Label _dtc = Value();
    private readonly Label _service = Value();
    private readonly Label _orders = Value();
    private readonly Label _sync = Theme.MutedText("Синхронизация...");

    public ClientDashboardPage()
    {
        Title = "Главная";
        BackgroundColor = Theme.Page;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children = { Header(), Hero(), Kpis(), QuickActions(), ClientNotice() }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View Header()
    {
        var session = _api.Session;
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Theme.Eyebrow("AUTODIAG PRO • CLIENT"),
                Theme.H1($"Здравствуйте, {session?.DisplayName ?? "клиент"}"),
                new HorizontalStackLayout { Spacing = 8, Children = { Theme.Pill("КЛИЕНТ", Theme.Green), _sync } }
            }
        };
    }

    private View Hero()
    {
        var service = Theme.PrimaryButton("Записаться на СТО");
        service.Clicked += async (_, _) => await Shell.Current.GoToAsync("//clientservice");
        var history = Theme.SecondaryButton("История");
        history.Clicked += async (_, _) => await Shell.Current.GoToAsync("history");

        var grid = new Grid { HeightRequest = 265 };
        grid.Add(new Image { Source = "hero_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.62 });
        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(18),
            Spacing = 9,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("MY CAR"),
                new Label { Text = "Ваш автомобиль и сервис\nв одном приложении", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = "Диагностика • история • ТО • детали • запись", FontSize = 12, TextColor = Theme.TextSoft },
                new HorizontalStackLayout { Spacing = 8, Children = { service, history } }
            }
        });

        return new Border { Stroke = Theme.Line, StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 }, Content = grid };
    }

    private View Kpis()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 10, RowSpacing = 10
        };
        grid.Add(Kpi("МОЙ АВТОМОБИЛЬ", _vehicle), 0, 0);
        grid.Add(Kpi("ОШИБКИ DTC", _dtc), 1, 0);
        grid.Add(Kpi("СЛЕДУЮЩЕЕ ТО", _service), 0, 1);
        grid.Add(Kpi("АКТИВНЫЕ РАБОТЫ", _orders), 1, 1);
        return grid;
    }

    private View QuickActions()
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10, RowSpacing = 10 };
        grid.Add(Action("Диагностика", "Проверить авто", "tab_scan.svg", async () => await Shell.Current.GoToAsync("//diagnostics")), 0, 0);
        grid.Add(Action("AI помощник", "Разобрать проблему", "tab_ai.svg", async () => await Shell.Current.GoToAsync("ai")), 1, 0);
        grid.Add(Action("Сервис", "Запись и статус", "tab_service.svg", async () => await Shell.Current.GoToAsync("//clientservice")), 0, 1);
        grid.Add(Action("Мои авто", "VIN и пробег", "tab_car.svg", async () => await Shell.Current.GoToAsync("//vehicles")), 1, 1);
        return new VerticalStackLayout { Spacing = 9, Children = { Theme.H2("Для вас"), grid } };
    }

    private static View Action(string title, string subtitle, string icon, Func<Task> action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await action();
        var card = Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                new Image { Source = icon, HeightRequest = 28, WidthRequest = 28, HorizontalOptions = LayoutOptions.Start },
                new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText(subtitle)
            }
        }, new Thickness(14));
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private View ClientNotice() => Theme.CardView(new VerticalStackLayout
    {
        Spacing = 8,
        Children =
        {
            Theme.Eyebrow("ВАШ ДОСТУП"),
            Theme.H2("Личный кабинет клиента"),
            Theme.Body("Показываются только ваши автомобили, диагностика, история, сервис, отчёты, детали и AI. Служебные функции СТО и администрирование недоступны.")
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

            var selected = _state.SelectedVehicle;
            _vehicle.Text = selected?.DisplayName ?? "Не выбрано";
            var last = selected is null ? null : scans.Where(x => x.VehicleId == selected.Id || (!string.IsNullOrWhiteSpace(selected.Vin) && x.Vin == selected.Vin)).OrderByDescending(x => x.ScannedAt).FirstOrDefault();
            _dtc.Text = last is null ? "—" : last.DtcCount.ToString();
            _dtc.TextColor = last?.DtcCount > 0 ? Theme.Accent : Theme.Green;

            var db = await _store.LoadAsync();
            var plan = selected is null ? null : db.ServicePlans.FirstOrDefault(x => x.VehicleId == selected.Id);
            _service.Text = plan?.NextServiceMileageKm is long km ? $"{km:N0} км" : plan?.NextServiceDate is DateTimeOffset d ? d.LocalDateTime.ToString("dd.MM.yyyy") : "Не задано";
            _orders.Text = selected is null ? "0" : orders.Count(x => x.VehicleId == selected.Id && !string.Equals(x.Status, "Выдано", StringComparison.OrdinalIgnoreCase)).ToString();
            _sync.Text = $"ONLINE • {DateTime.Now:HH:mm}";
            _sync.TextColor = Theme.Green;
        }
        catch (Exception ex) { _sync.Text = ex.Message; _sync.TextColor = Theme.Red; }
    }

    private static View Kpi(string title, Label value) => Theme.CardView(new VerticalStackLayout { Spacing = 7, Children = { Theme.Eyebrow(title), value } }, new Thickness(14));
    private static Label Value() => new() { Text = "—", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, LineBreakMode = LineBreakMode.TailTruncation };
}
