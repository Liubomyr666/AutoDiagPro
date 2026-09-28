using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ClientServicePage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();

    private readonly VerticalStackLayout _orders = new() { Spacing = 10 };
    private readonly VerticalStackLayout _appointments = new() { Spacing = 10 };
    private readonly VerticalStackLayout _maintenance = new() { Spacing = 10 };
    private readonly VerticalStackLayout _parts = new() { Spacing = 10 };
    private readonly HorizontalStackLayout _photos = new() { Spacing = 10 };
    private readonly Label _status = Theme.MutedText("Загрузка...");
    private readonly Entry _reason = Field("Что нужно сделать? Например: диагностика, замена масла");
    private readonly DatePicker _date = new()
    {
        Date = DateTime.Today.AddDays(1),
        MinimumDate = DateTime.Today,
        Format = "dd.MM.yyyy",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        HeightRequest = 48
    };
    private readonly TimePicker _time = new()
    {
        Time = new TimeSpan(10, 0, 0),
        Format = "HH:mm",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        HeightRequest = 48
    };

    public ClientServicePage()
    {
        Title = "Сервис";
        BackgroundColor = Theme.Page;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 14, 16, 118),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("ОБСЛУЖИВАНИЕ"),
                    Theme.H1("Сервис и обслуживание"),
                    Theme.MutedText("Запись на обслуживание, план ТО и статус текущих работ."),
                    Hero(),
                    BuildBookingCard(),
                    BuildServicePlanButton(),
                    _status,
                    Theme.H2("Работы на СТО"),
                    _orders,
                    Theme.H2("Мои записи"),
                    _appointments,
                    Theme.H2("Фото ремонта"),
                    new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _photos },
                    Theme.H2("План ТО"),
                    _maintenance,
                    Theme.H2("Установленные детали"),
                    _parts
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View Hero()
    {
        var grid = new Grid { HeightRequest = 170 };
        grid.Add(new Image { Source = "login_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.67 });
        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 6,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("СЕРВИС", Theme.Green),
                new Label
                {
                    Text = "Запись, ТО и статус ремонта",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText("Всё по выбранному автомобилю")
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

    private View BuildBookingCard()
    {
        var book = Theme.PrimaryButton("Сохранить запись");
        book.Clicked += async (_, _) => await AddAppointmentAsync();

        var when = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        when.Add(_date, 0, 0);
        when.Add(_time, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.H2("Запланировать визит"),
                Theme.MutedText("Выберите дату, время и причину визита."),
                _reason,
                when,
                book
            }
        }, new Thickness(14), 16);
    }

    private View BuildServicePlanButton()
    {
        var plan = Theme.SecondaryButton("Открыть план ТО");
        plan.Clicked += async (_, _) => await Shell.Current.GoToAsync("service");
        return plan;
    }

    private async Task AddAppointmentAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("Сервис", "Сначала выберите автомобиль.", "OK");
            return;
        }

        var reason = _reason.Text?.Trim() ?? "";
        if (reason.Length < 3)
        {
            await DisplayAlert("Сервис", "Укажите, что нужно сделать.", "OK");
            return;
        }

        var startsAt = new DateTimeOffset(_date.Date.Date + _time.Time);
        if (startsAt < DateTimeOffset.Now.AddMinutes(15))
        {
            await DisplayAlert("Сервис", "Выберите будущее время.", "OK");
            return;
        }

        try
        {
            await _api.CreateAppointmentAsync(
                vehicle.Id,
                _api.Session?.DisplayName ?? "Клиент",
                startsAt,
                reason);

            _reason.Text = "";
            _status.Text = "Запись отправлена на СТО.";
            _status.TextColor = Theme.Green;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Не удалось создать запись: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task LoadAsync()
    {
        _orders.Clear();
        _appointments.Clear();
        _maintenance.Clear();
        _parts.Clear();
        _photos.Clear();

        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            _status.Text = "Сначала выберите автомобиль.";
            _status.TextColor = Theme.Accent;
            _orders.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            _appointments.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            _maintenance.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            _parts.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            return;
        }

        _status.Text = vehicle.DisplayName + " • синхронизация...";
        _status.TextColor = Theme.Accent;

        try
        {
            var ordersTask = _api.GetWorkOrdersAsync();
            var appointmentsTask = _api.GetAppointmentsAsync(vehicle.Id);
            var maintenanceTask = _api.GetMaintenanceAsync(vehicle.Id);
            var partsTask = _api.GetInstalledPartsAsync(vehicle.Id);
            var photosTask = _api.GetPhotosAsync(vehicle.Id);

            await Task.WhenAll(ordersTask, appointmentsTask, maintenanceTask, partsTask, photosTask);

            var orders = (await ordersTask)
                .Where(x => x.VehicleId == vehicle.Id)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(12)
                .ToList();

            foreach (var order in orders)
                _orders.Add(BuildOrderCard(order));

            if (_orders.Count == 0)
                _orders.Add(Theme.CardView(Theme.MutedText("Работ на СТО пока нет.")));

            var appointments = (await appointmentsTask)
                .OrderBy(x => x.StartsAt)
                .ToList();
            foreach (var appointment in appointments)
                _appointments.Add(BuildAppointmentCard(appointment));
            if (_appointments.Count == 0)
                _appointments.Add(Theme.CardView(Theme.MutedText("Запланированных визитов пока нет.")));

            var maintenance = (await maintenanceTask)
                .OrderBy(x => x.DueMileage ?? long.MaxValue)
                .ThenBy(x => x.DueDate)
                .ToList();
            foreach (var item in maintenance)
                _maintenance.Add(BuildMaintenanceCard(item));
            if (_maintenance.Count == 0)
                _maintenance.Add(Theme.CardView(Theme.MutedText("План ТО пока не заполнен.")));

            var parts = (await partsTask)
                .OrderByDescending(x => x.InstalledAt)
                .Take(30)
                .ToList();
            foreach (var part in parts)
                _parts.Add(BuildPartCard(part));
            if (_parts.Count == 0)
                _parts.Add(Theme.CardView(Theme.MutedText("История установленных деталей пока пустая.")));

            var photos = (await photosTask)
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .ToList();
            foreach (var photo in photos)
            {
                try { _photos.Add(await BuildPhotoCardAsync(photo)); }
                catch { }
            }
            if (_photos.Count == 0)
                _photos.Add(Theme.CardView(Theme.MutedText("Фото ремонта пока нет.")));

            _status.Text = vehicle.DisplayName + " • данные обновлены";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка синхронизации: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private View BuildOrderCard(ServerWorkOrderRecord order)
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(order.Title) ? "Работы по автомобилю" : order.Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text,
            FontAutoScalingEnabled = false
        });
        stack.Add(Theme.Pill(FriendlyStatus(order.Status), Theme.Accent));
        stack.Add(Theme.MutedText(
            $"{order.UpdatedAt.LocalDateTime:dd.MM.yyyy HH:mm}" +
            (order.TotalAmount > 0 ? $" • {order.TotalAmount:N2} €" : "")));

        if (order.TotalAmount > 0 || !string.Equals(order.EstimateStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            var estimateColor =
                string.Equals(order.EstimateStatus, "Approved", StringComparison.OrdinalIgnoreCase) ? Theme.Green :
                string.Equals(order.EstimateStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ? Theme.Red :
                Theme.Accent;
            stack.Add(Theme.Pill("СМЕТА • " + FriendlyEstimate(order.EstimateStatus), estimateColor));
        }

        if (string.Equals(order.EstimateStatus, "WaitingClient", StringComparison.OrdinalIgnoreCase))
        {
            var approve = Theme.PrimaryButton($"Согласовать {order.TotalAmount:N2} €");
            var reject = Theme.SecondaryButton("Не согласовать");
            reject.TextColor = Theme.Red;

            approve.Clicked += async (_, _) => await DecideAsync(order, true);
            reject.Clicked += async (_, _) => await DecideAsync(order, false);

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            row.Add(approve, 0, 0);
            row.Add(reject, 1, 0);
            stack.Add(row);
        }

        if (!string.IsNullOrWhiteSpace(order.ClientDecisionNote))
            stack.Add(Theme.MutedText("Комментарий: " + order.ClientDecisionNote));

        return Theme.CardView(stack, new Thickness(14), 16);
    }

    private async Task DecideAsync(ServerWorkOrderRecord order, bool approved)
    {
        string? note = null;
        if (!approved)
        {
            note = await DisplayPromptAsync(
                "Смета",
                "Можно указать причину отказа:",
                "Отправить",
                "Без комментария");
        }

        try
        {
            await _api.DecideWorkOrderAsync(order.Id, approved, note);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Смета", ex.Message, "OK");
        }
    }

    private View BuildAppointmentCard(ServerAppointmentRecord appointment)
    {
        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = appointment.Work,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText($"{appointment.StartsAt.LocalDateTime:dd.MM.yyyy • HH:mm}"),
                Theme.Pill(appointment.Status, Theme.Green)
            }
        }, new Thickness(14), 16);
    }

    private View BuildMaintenanceCard(ServerMaintenanceRecord item)
    {
        var due = new List<string>();
        if (item.DueMileage is not null) due.Add($"{item.DueMileage:N0} км");
        if (item.DueDate is not null) due.Add(item.DueDate.Value.ToString("dd.MM.yyyy"));

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label
                {
                    Text = item.Name,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText(due.Count == 0 ? "Срок не задан" : "Следующее ТО • " + string.Join(" • ", due)),
                string.IsNullOrWhiteSpace(item.Notes) ? Theme.MutedText("Без заметок") : Theme.Body(item.Notes)
            }
        }, new Thickness(13), 16);
    }

    private View BuildPartCard(ServerInstalledPartRecord part)
    {
        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(part.Manufacturer)) meta.Add(part.Manufacturer);
        if (!string.IsNullOrWhiteSpace(part.PartNumber)) meta.Add(part.PartNumber);
        if (part.InstalledMileage is not null) meta.Add($"{part.InstalledMileage:N0} км");

        var warranty = part.WarrantyUntil is null
            ? "Гарантия не указана"
            : "Гарантия до " + part.WarrantyUntil.Value.ToString("dd.MM.yyyy");

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label
                {
                    Text = part.Name,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText(meta.Count == 0 ? $"Установлено {part.InstalledAt.LocalDateTime:dd.MM.yyyy}" : string.Join(" • ", meta)),
                Theme.MutedText(warranty)
            }
        }, new Thickness(13), 16);
    }

    private async Task<View> BuildPhotoCardAsync(ServerPhotoRecord photo)
    {
        var bytes = await _api.GetPhotoBytesAsync(photo.Id);
        var image = new Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
            Aspect = Aspect.AspectFill,
            WidthRequest = 150,
            HeightRequest = 105
        };

        return new Border
        {
            WidthRequest = 160,
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    image,
                    new Label
                    {
                        Text = photo.Kind == "Before" ? "ДО" : photo.Kind == "After" ? "ПОСЛЕ" : photo.Kind,
                        Margin = new Thickness(8, 0, 8, 7),
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Theme.Accent,
                        FontAutoScalingEnabled = false
                    }
                }
            }
        };
    }

    private static string FriendlyStatus(string? status) =>
        (status ?? "").Trim().ToLowerInvariant() switch
        {
            "open" or "accepted" => "Принято",
            "diagnostics" => "Диагностика",
            "waitingparts" => "Ожидание деталей",
            "repairing" => "В ремонте",
            "qualitycheck" => "Проверка",
            "ready" => "Готово",
            "completed" or "closed" => "Завершено",
            _ => string.IsNullOrWhiteSpace(status) ? "В работе" : status
        };

    private static string FriendlyEstimate(string? status)
    {
        if (string.Equals(status, "WaitingClient", StringComparison.OrdinalIgnoreCase)) return "ЖДЁТ ВАШЕГО РЕШЕНИЯ";
        if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)) return "СОГЛАСОВАНА";
        if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)) return "ОТКЛОНЕНА";
        return "НЕ ОТПРАВЛЕНА";
    }

    private static Entry Field(string placeholder) => new()
    {
        Placeholder = placeholder,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48,
        FontAutoScalingEnabled = false
    };
}
