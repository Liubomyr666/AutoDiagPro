using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class VehiclesPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly VehicleVisualService _visual;

    private readonly VerticalStackLayout _vehicleCards = new() { Spacing = 10 };
    private readonly Image _selectedPhoto = new() { Aspect = Aspect.AspectFit, BackgroundColor = Theme.Surface };
    private Grid? _photoPlaceholder;
    private readonly Image _photoPlaceholderIcon = new()
    {
        Source = "vehicle_placeholder.svg",
        WidthRequest = 76,
        HeightRequest = 52,
        Aspect = Aspect.AspectFit,
        HorizontalOptions = LayoutOptions.Center
    };
    private readonly Label _photoPlaceholderTitle = new()
    {
        Text = "Укажите цвет кузова",
        TextColor = Theme.Text,
        FontSize = 15,
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center,
        FontAutoScalingEnabled = false
    };
    private readonly Label _photoPlaceholderText = new()
    {
        Text = "После выбора цвета загрузится реальное фото автомобиля.",
        TextColor = Theme.Muted,
        FontSize = 11,
        HorizontalTextAlignment = TextAlignment.Center,
        FontAutoScalingEnabled = false
    };
    private readonly Label _selectedColor = Theme.MutedText("Цвет • не определён");
    private readonly BoxView _selectedColorSwatch = new() { WidthRequest = 14, HeightRequest = 14, Color = Theme.Line };
    private readonly Label _status = Theme.MutedText("Загрузка...");
    private readonly Label _selectedTitle = Value("Автомобиль не выбран", 20);
    private readonly Label _selectedVin = Theme.MutedText("VIN • —");
    private readonly Label _selectedMileage = Theme.MutedText("Пробег • —");
    private readonly Label _condition = Value("Нет данных", 15);
    private readonly Label _dtc = Value("—", 15);
    private readonly Label _lastScan = Value("—", 13);
    private readonly Label _nextService = Value("Не задано", 13);
    private readonly Label _activeWorks = Value("0", 15);

    public VehiclesPage()
    {
        _visual = new VehicleVisualService(_api);
        Title = "Авто";
        BackgroundColor = Theme.Page;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 14, 16, 118),
                Spacing = 14,
                Children =
                {
                    BuildHeader(),
                    BuildVehicleHero(),
                    BuildStatusGrid(),
                    BuildQuickActions(),
                    Theme.H2("Мои автомобили"),
                    _vehicleCards
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
        var add = Theme.CompactButton("+ Добавить");
        add.IsVisible = AccessPolicy.IsStaff;
        add.Clicked += async (_, _) => await Shell.Current.GoToAsync("addvehicle");

        var refresh = Theme.CompactButton("Обновить");
        refresh.Clicked += async (_, _) => await LoadAsync();

        var top = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        top.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                Theme.Eyebrow("МОИ АВТОМОБИЛИ"),
                Theme.H1("Автомобили"),
                _status
            }
        }, 0, 0);

        top.Add(new HorizontalStackLayout
        {
            Spacing = 7,
            Children = { add, refresh }
        }, 1, 0);

        return top;
    }

    private View BuildVehicleHero()
    {
        var scanner = Theme.PrimaryButton("Открыть сканер");
        scanner.FontSize = 13;
        scanner.HeightRequest = 42;
        scanner.Clicked += async (_, _) => await Shell.Current.GoToAsync("//diagnostics");

        var history = Theme.SecondaryButton("История");
        history.FontSize = 13;
        history.HeightRequest = 42;
        history.Clicked += async (_, _) => await Shell.Current.GoToAsync("history");

        var color = Theme.SecondaryButton("Цвет");
        color.FontSize = 13;
        color.HeightRequest = 42;
        color.Clicked += async (_, _) => await EditColorAsync();

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        buttons.Add(scanner, 0, 0);
        buttons.Add(history, 1, 0);
        buttons.Add(color, 2, 0);

        var colorRow = new HorizontalStackLayout { Spacing = 7 };
        colorRow.Add(new Border
        {
            WidthRequest = 20,
            HeightRequest = 20,
            Padding = 3,
            BackgroundColor = Theme.Surface,
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = _selectedColorSwatch
        });
        _selectedColor.VerticalTextAlignment = TextAlignment.Center;
        colorRow.Add(_selectedColor);

        _photoPlaceholder = new Grid
        {
            Padding = new Thickness(24),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = true
        };
        _photoPlaceholder.Add(new VerticalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 310,
            Children =
            {
                _photoPlaceholderIcon,
                _photoPlaceholderTitle,
                _photoPlaceholderText
            }
        });

        var photoLayer = new Grid();
        photoLayer.Add(_selectedPhoto);
        photoLayer.Add(_photoPlaceholder);

        var photoFrame = new Border
        {
            HeightRequest = 230,
            BackgroundColor = Theme.Surface,
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(8),
            Content = photoLayer
        };

        var details = new VerticalStackLayout
        {
            Padding = new Thickness(2, 4, 2, 0),
            Spacing = 7,
            Children =
            {
                Theme.Pill("ПОДКЛЮЧЕННЫЙ АВТОМОБИЛЬ"),
                _selectedTitle,
                _selectedVin,
                _selectedMileage,
                colorRow,
                buttons
            }
        };

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { photoFrame, details }
        }, new Thickness(10), 18);
    }

    private View BuildStatusGrid()
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

        grid.Add(Kpi("Состояние", _condition), 0, 0);
        grid.Add(Kpi("Ошибки DTC", _dtc), 1, 0);
        grid.Add(Kpi("Последняя проверка", _lastScan), 0, 1);
        grid.Add(Kpi("Активные работы", _activeWorks), 1, 1);

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.H2("Состояние автомобиля"),
                grid,
                BuildServiceStatusCard()
            }
        };
    }

    private View BuildServiceStatusCard()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        grid.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                Theme.Eyebrow("СЛЕДУЮЩЕЕ ТО"),
                Theme.MutedText("План обслуживания для выбранного автомобиля")
            }
        }, 0, 0);

        _nextService.VerticalTextAlignment = TextAlignment.Center;
        grid.Add(_nextService, 1, 0);
        return Theme.CardView(grid, new Thickness(14), 16);
    }

    private View BuildQuickActions()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 9,
            RowSpacing = 9
        };

        grid.Add(ActionButton("AI помощник", "ai"), 0, 0);
        grid.Add(ActionButton("Детали по VIN", "parts"), 1, 0);
        grid.Add(ActionButton("Пробег / OBD", "mileage"), 0, 1);
        grid.Add(ActionButton("Шины / колодки", "wear"), 1, 1);
        grid.Add(ActionButton("VIN / комплектация", "vehicleidentity"), 0, 2);
        grid.Add(ActionButton("Отчёт / сравнение", "reports"), 1, 2);
        grid.Add(ActionButton("Вся история", "timeline"), 0, 3);
        grid.Add(ActionButton("Уведомления", "notifications"), 1, 3);

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.H2("По автомобилю"),
                grid
            }
        };
    }

    private Button ActionButton(string text, string route)
    {
        var button = Theme.SecondaryButton(text);
        button.FontSize = 12;
        button.HeightRequest = 46;
        button.Clicked += async (_, _) =>
        {
            if (_state.SelectedVehicle is null)
            {
                await DisplayAlert("AutoDiag Pro", "Сначала выберите автомобиль.", "OK");
                return;
            }
            if (route == "vininfo")
            {
                await ShowVinInfoAsync();
                return;
            }

            await Shell.Current.GoToAsync(route);
        };
        return button;
    }

    private async Task ShowVinInfoAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null || string.IsNullOrWhiteSpace(vehicle.Vin))
        {
            await DisplayAlert("VIN", "Для автомобиля не указан VIN.", "OK");
            return;
        }

        try
        {
            var data = await _api.DecodeVinRawAsync(vehicle.Vin);
            string V(string name) =>
                data.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? value.GetString() ?? "—"
                    : "—";

            var text =
                $"VIN: {V("vin")}\n" +
                $"Марка: {V("make")}\n" +
                $"Модель: {V("model")}\n" +
                $"Год: {V("modelYear")}\n" +
                $"Тип: {V("vehicleType")}\n" +
                $"Кузов: {V("bodyClass")}\n" +
                $"Двигатель: {V("engine")}\n" +
                $"Объём: {V("displacementL")} л\n" +
                $"Топливо: {V("fuelType")}\n" +
                $"Коробка: {V("transmission")}\n" +
                $"Привод: {V("driveType")}";

            await DisplayAlert("Данные по VIN", text, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VIN", ex.Message, "OK");
        }
    }

    private async Task LoadAsync()
    {
        _status.Text = "Синхронизация...";
        _status.TextColor = Theme.Muted;

        try
        {
            var vehiclesTask = _api.GetVehiclesAsync();
            var scansTask = _api.GetScansAsync();
            var ordersTask = _api.GetWorkOrdersAsync();

            await Task.WhenAll(vehiclesTask, scansTask, ordersTask);

            var vehicles = await vehiclesTask;
            var scans = await scansTask;
            var orders = await ordersTask;

            _state.Vehicles = vehicles;
            if (_state.SelectedVehicle is null || vehicles.All(x => x.Id != _state.SelectedVehicle.Id))
                _state.SelectedVehicle = vehicles.FirstOrDefault();

            RenderVehicleCards(vehicles);
            await RefreshSelectedAsync(scans, orders);

            _status.Text = vehicles.Count == 0
                ? "Добавьте первый автомобиль"
                : $"{vehicles.Count} авто • синхронизировано";
            _status.TextColor = vehicles.Count == 0 ? Theme.Accent : Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = "Не удалось обновить: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private void RenderVehicleCards(IReadOnlyList<ServerVehicleRecord> vehicles)
    {
        _vehicleCards.Clear();

        if (vehicles.Count == 0)
        {
            _vehicleCards.Add(Theme.CardView(Theme.MutedText("Автомобилей пока нет. Нажмите «+ Добавить».")));
            return;
        }

        foreach (var vehicle in vehicles)
        {
            var active = _state.SelectedVehicle?.Id == vehicle.Id;
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            row.Add(new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = vehicle.DisplayName,
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Theme.Text,
                        FontAutoScalingEnabled = false,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation
                    },
                    Theme.MutedText("VIN • " + (string.IsNullOrWhiteSpace(vehicle.Vin) ? "—" : vehicle.Vin)),
                    Theme.MutedText(vehicle.MileageKm is null ? "Пробег • —" : $"Пробег • {vehicle.MileageKm:N0} км")
                }
            }, 0, 0);

            var badge = Theme.Pill(active ? "АКТИВНЫЙ" : "ВЫБРАТЬ", active ? Theme.Green : Theme.Muted);
            badge.VerticalOptions = LayoutOptions.Center;
            row.Add(badge, 1, 0);

            var card = Theme.CardView(row, new Thickness(14), 16);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                _state.SelectedVehicle = vehicle;
                await LoadAsync();
            };
            card.GestureRecognizers.Add(tap);
            _vehicleCards.Add(card);
        }
    }

    private async Task RefreshSelectedAsync(IReadOnlyList<ServerScanRecord> scans, IReadOnlyList<ServerWorkOrderRecord> orders)
    {
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _selectedTitle.Text = "Автомобиль не выбран";
        }
        else
        {
            var displayModel = string.IsNullOrWhiteSpace(v.Model)
                ? VehicleVisualService.VehicleSeriesHint(v)
                : v.Model;
            _selectedTitle.Text = string.Join(" ", new[] { v.Make, displayModel }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            if (string.IsNullOrWhiteSpace(_selectedTitle.Text))
                _selectedTitle.Text = "Автомобиль";
        }
        _selectedVin.Text = "VIN • " + (string.IsNullOrWhiteSpace(v?.Vin) ? "—" : v!.Vin);
        _selectedMileage.Text = v?.MileageKm is null ? "Пробег • —" : $"Пробег • {v.MileageKm:N0} км";

        if (v is null)
        {
            _selectedPhoto.Source = null;
            SetPhotoPlaceholder(
                "Автомобиль не выбран",
                "Подключите OBD или выберите автомобиль, чтобы загрузить его фото.");
            _selectedColor.Text = "Цвет • не определён";
            _selectedColorSwatch.Color = Theme.Line;
            _condition.Text = "Нет данных";
            _condition.TextColor = Theme.Muted;
            _dtc.Text = "—";
            _lastScan.Text = "—";
            _nextService.Text = "Не задано";
            _activeWorks.Text = "0";
            return;
        }

        await RefreshVehicleVisualAsync(v);

        var last = scans
            .Where(x => x.VehicleId == v.Id ||
                        (!string.IsNullOrWhiteSpace(v.Vin) &&
                         string.Equals(x.Vin, v.Vin, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.ScannedAt)
            .FirstOrDefault();

        if (last is null)
        {
            _condition.Text = "Нет проверки";
            _condition.TextColor = Theme.Accent;
            _dtc.Text = "—";
            _dtc.TextColor = Theme.Muted;
            _lastScan.Text = "—";
        }
        else
        {
            _dtc.Text = last.DtcCount.ToString();
            _dtc.TextColor = last.DtcCount == 0 ? Theme.Green : Theme.Accent;
            _condition.Text = last.DtcCount == 0 ? "Без ошибок" : "Нужна проверка";
            _condition.TextColor = last.DtcCount == 0 ? Theme.Green : Theme.Accent;
            _lastScan.Text = last.ScannedAt.LocalDateTime.ToString("dd.MM.yy HH:mm");
        }

        _activeWorks.Text = orders.Count(x =>
            x.VehicleId == v.Id &&
            !IsClosed(x.Status)).ToString();

        try
        {
            var maintenance = await _api.GetMaintenanceAsync(v.Id);
            var currentKm = v.MileageKm;
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
                _nextService.Text = "Не задано";
                _nextService.TextColor = Theme.Muted;
            }
            else if (next.DueMileage is long dueKm && currentKm is long nowKm)
            {
                var left = dueKm - nowKm;
                _nextService.Text = left <= 0 ? $"Просрочено {Math.Abs(left):N0} км" : $"Через {left:N0} км";
                _nextService.TextColor = left <= 0 ? Theme.Red : left <= 1000 ? Theme.Accent : Theme.Green;
            }
            else if (next.DueDate is DateOnly dueDate)
            {
                var days = dueDate.DayNumber - today.DayNumber;
                _nextService.Text = days < 0 ? $"Просрочено {Math.Abs(days)} дн." : days == 0 ? "Сегодня" : $"Через {days} дн.";
                _nextService.TextColor = days < 0 ? Theme.Red : days <= 14 ? Theme.Accent : Theme.Green;
            }
            else
            {
                _nextService.Text = next.Name;
                _nextService.TextColor = Theme.Text;
            }
        }
        catch
        {
            _nextService.Text = "Нет связи";
            _nextService.TextColor = Theme.Red;
        }
    }

    private async Task RefreshVehicleVisualAsync(ServerVehicleRecord vehicle)
    {
        _selectedPhoto.Source = null;
        SetPhotoPlaceholder(
            "Ищем фото автомобиля",
            "Подбираем точную модель и цвет…");

        var visual = await _visual.ResolveAsync(vehicle);

        _selectedColor.Text = string.IsNullOrWhiteSpace(visual.ColorName)
            ? "Цвет • не определён"
            : "Цвет • " + visual.ColorName +
              (string.IsNullOrWhiteSpace(visual.PaintCode) ? "" : " • код " + visual.PaintCode);

        _selectedColorSwatch.Color = VehicleVisualService.Swatch(visual.ColorName);

        if (string.IsNullOrWhiteSpace(visual.ColorName))
        {
            SetPhotoPlaceholder(
                "Укажите цвет кузова",
                "После выбора цвета загрузится реальное фото автомобиля.");
            return;
        }

        if (string.IsNullOrWhiteSpace(visual.PhotoUrl))
        {
            SetPhotoPlaceholder(
                "Фото не найдено",
                "Цвет сохранён. Точное фото появится, когда найдётся совпадение модели и цвета.");
            return;
        }

        _selectedPhoto.Source = new UriImageSource
        {
            Uri = new Uri(visual.PhotoUrl),
            CachingEnabled = true,
            CacheValidity = TimeSpan.FromDays(30)
        };
        HidePhotoPlaceholder();
    }

    private void SetPhotoPlaceholder(string title, string message)
    {
        _photoPlaceholderTitle.Text = title;
        _photoPlaceholderText.Text = message;
        if (_photoPlaceholder is not null)
            _photoPlaceholder.IsVisible = true;
    }

    private void HidePhotoPlaceholder()
    {
        if (_photoPlaceholder is not null)
            _photoPlaceholder.IsVisible = false;
    }

    private async Task EditColorAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("AutoDiag Pro", "Сначала выберите или подключите автомобиль.", "OK");
            return;
        }

        var current = await _visual.ResolveAsync(vehicle);
        var color = await DisplayPromptAsync(
            "Цвет автомобиля",
            "Введите подтверждённый цвет кузова. Например: Black, Deep Black Pearl, Синий.",
            initialValue: current.ColorName,
            maxLength: 80);

        if (color is null) return;

        var paint = await DisplayPromptAsync(
            "Код краски",
            "Если код краски известен — введите его. Можно оставить пустым.",
            initialValue: current.PaintCode,
            maxLength: 40);

        _visual.SaveColor(vehicle, color, paint ?? "");
        await RefreshVehicleVisualAsync(vehicle);
    }

    private static bool IsClosed(string? status) =>
        string.Equals(status, "Выдано", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Завершено", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Закрыт", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);

    private static View Kpi(string title, Label value) =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Theme.Eyebrow(title),
                value
            }
        }, new Thickness(14), 16);

    private static Label Value(string text, double size) => new()
    {
        Text = text,
        FontSize = size,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text,
        FontAutoScalingEnabled = false,
        MaxLines = 1,
        LineBreakMode = LineBreakMode.TailTruncation
    };
}