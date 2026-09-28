using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ClientDashboardPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Label _vehicle = Value();
    private readonly Label _dtc = Value();
    private readonly Label _service = Value();
    private readonly Label _orders = Value();
    private readonly Label _sync = Theme.MutedText("Обновление...");
    private readonly Border _statusDot = new()
    {
        WidthRequest = 8,
        HeightRequest = 8,
        BackgroundColor = Theme.Muted,
        StrokeThickness = 0,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
        VerticalOptions = LayoutOptions.Center
    };
    private readonly Label _heroTitle = new()
    {
        Text = "Добавьте автомобиль",
        FontSize = 20,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.White,
        MaxLines = 2
    };
    private readonly Label _heroSubtitle = new()
    {
        Text = "VIN, диагностика, история и сервис в одном месте",
        FontSize = 12,
        TextColor = Color.FromArgb("#D3D9DD"),
        MaxLines = 2
    };

    public ClientDashboardPage()
    {
        Title = "Главная";
        BackgroundColor = Theme.Page;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 92),
                Spacing = 13,
                Children =
                {
                    Header(),
                    Hero(),
                    StatusGrid(),
                    QuickActions()
                }
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
        var name = ShortName(session?.DisplayName);

        var brand = new HorizontalStackLayout
        {
            Spacing = 9,
            Children =
            {
                new Image { Source = "brandmark.png", WidthRequest = 30, HeightRequest = 30 },
                new VerticalStackLayout
                {
                    Spacing = 0,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "AutoDiag Pro",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Text
                        },
                        new Label
                        {
                            Text = "Личный кабинет",
                            FontSize = 10,
                            TextColor = Theme.Muted
                        }
                    }
                }
            }
        };

        var profile = Theme.CompactButton("Профиль");
        profile.HeightRequest = 36;
        profile.Clicked += async (_, _) => await Shell.Current.GoToAsync("//more");

        var top = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        top.Add(brand, 0, 0);
        top.Add(profile, 1, 0);

        var greeting = new Label
        {
            Text = string.IsNullOrWhiteSpace(name) ? "Здравствуйте" : $"Здравствуйте, {name}",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text,
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                top,
                greeting,
                new HorizontalStackLayout
                {
                    Spacing = 7,
                    Children =
                    {
                        _statusDot,
                        _sync
                    }
                }
            }
        };
    }

    private View Hero()
    {
        var primary = Theme.PrimaryButton("Открыть автомобиль");
        primary.FontSize = 14;
        primary.HeightRequest = 42;
        primary.Clicked += async (_, _) =>
        {
            await Shell.Current.GoToAsync("//vehicles");
        };

        var diag = Theme.SecondaryButton("Диагностика");
        diag.FontSize = 14;
        diag.HeightRequest = 42;
        diag.Clicked += async (_, _) => await Shell.Current.GoToAsync("//diagnostics");

        var grid = new Grid { HeightRequest = 184 };
        grid.Add(new Image { Source = "hero_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.56 });

        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 7,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                _heroTitle,
                _heroSubtitle,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { primary, diag }
                }
            }
        });

        return new Border
        {
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = grid
        };
    }

    private View StatusGrid()
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

        grid.Add(Kpi("Автомобиль", _vehicle), 0, 0);
        grid.Add(Kpi("Ошибки", _dtc), 1, 0);
        grid.Add(Kpi("Следующее ТО", _service), 0, 1);
        grid.Add(Kpi("Работы", _orders), 1, 1);

        return grid;
    }

    private View QuickActions()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        grid.Add(Action("Диагностика", "Проверить авто", "tab_scan.png",
            async () => await Shell.Current.GoToAsync("//diagnostics")), 0, 0);

        grid.Add(Action("AI помощник", "Разобрать проблему", "tab_ai.png",
            async () => await Shell.Current.GoToAsync("ai")), 1, 0);

        grid.Add(Action("Сервис", "Запись и статус", "tab_service.png",
            async () => await Shell.Current.GoToAsync("//clientservice")), 0, 1);

        grid.Add(Action("История", "Проверки и работы", "tab_history.png",
            async () => await Shell.Current.GoToAsync("history")), 1, 1);

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Быстрые действия",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text
                },
                grid
            }
        };
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
                new Image
                {
                    Source = icon,
                    HeightRequest = 20,
                    WidthRequest = 20,
                    HorizontalOptions = LayoutOptions.Start
                },
                new Label
                {
                    Text = title,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    MaxLines = 1
                },
                new Label
                {
                    Text = subtitle,
                    FontSize = 12,
                    TextColor = Theme.Muted,
                    MaxLines = 1
                }
            }
        }, new Thickness(12), 14);

        card.GestureRecognizers.Add(tap);
        return card;
    }

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
            _heroTitle.Text = selected?.DisplayName ?? "Добавьте автомобиль";
            _heroSubtitle.Text = selected is null
                ? "VIN, диагностика, история и сервис в одном месте"
                : $"{(string.IsNullOrWhiteSpace(selected.Vin) ? "VIN не указан" : selected.Vin)} • {(selected.MileageKm is null ? "пробег не указан" : $"{selected.MileageKm:N0} км")}";

            var last = selected is null
                ? null
                : scans
                    .Where(x => x.VehicleId == selected.Id ||
                                (!string.IsNullOrWhiteSpace(selected.Vin) && x.Vin == selected.Vin))
                    .OrderByDescending(x => x.ScannedAt)
                    .FirstOrDefault();

            _dtc.Text = last is null ? "—" : last.DtcCount.ToString();
            _dtc.TextColor = last?.DtcCount > 0 ? Theme.Accent : Theme.Green;

            if (selected is null)
            {
                _service.Text = "Не задано";
            }
            else
            {
                var maintenance = await _api.GetMaintenanceAsync(selected.Id);
                var currentKm = selected.MileageKm;
                var today = DateOnly.FromDateTime(DateTime.Today);

                var next = maintenance
                    .OrderBy(x =>
                    {
                        if (x.DueMileage is long dueKm && currentKm is long nowKm)
                            return dueKm - nowKm;
                        return long.MaxValue;
                    })
                    .ThenBy(x => x.DueDate ?? DateOnly.MaxValue)
                    .FirstOrDefault();

                if (next is null)
                {
                    _service.Text = "Не задано";
                }
                else if (next.DueMileage is long dueKm && currentKm is long nowKm)
                {
                    var left = dueKm - nowKm;
                    _service.Text = left <= 0
                        ? $"Просрочено {Math.Abs(left):N0} км"
                        : $"Через {left:N0} км";
                    _service.TextColor = left <= 0 ? Theme.Red : left <= 1000 ? Theme.Accent : Theme.Green;
                }
                else if (next.DueDate is DateOnly dueDate)
                {
                    var days = dueDate.DayNumber - today.DayNumber;
                    _service.Text = days < 0
                        ? $"Просрочено {Math.Abs(days)} дн."
                        : days == 0
                            ? "Сегодня"
                            : $"Через {days} дн.";
                    _service.TextColor = days < 0 ? Theme.Red : days <= 14 ? Theme.Accent : Theme.Green;
                }
                else
                {
                    _service.Text = next.Name;
                }
            }

            _orders.Text = selected is null
                ? "0"
                : orders.Count(x => x.VehicleId == selected.Id && !IsClosed(x.Status)).ToString();

            _sync.Text = $"Синхронизировано • {DateTime.Now:HH:mm}";
            _sync.TextColor = Theme.Green;
            _statusDot.BackgroundColor = Theme.Green;
        }
        catch
        {
            _sync.Text = "Нет связи с сервером";
            _sync.TextColor = Theme.Red;
            _statusDot.BackgroundColor = Theme.Red;
        }
    }

    private static bool IsClosed(string? status)
    {
        var value = (status ?? "").Trim();
        return value.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Завершено", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Закрыт", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Выдано", StringComparison.OrdinalIgnoreCase);
    }

    private static View Kpi(string title, Label value) =>
        Theme.CardView(
            new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 11,
                        TextColor = Theme.Muted
                    },
                    value
                }
            },
            new Thickness(14),
            16);

    private static Label Value() => new()
    {
        Text = "—",
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text,
        MaxLines = 1,
        LineBreakMode = LineBreakMode.TailTruncation
    };

    private static string ShortName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "";

        return displayName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";
    }
}
