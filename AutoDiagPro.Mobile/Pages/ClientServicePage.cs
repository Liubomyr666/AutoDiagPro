using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ClientServicePage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();

    private readonly VerticalStackLayout _orders = new() { Spacing = 10 };
    private readonly VerticalStackLayout _appointments = new() { Spacing = 10 };
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
                    Theme.Eyebrow("МОЙ СЕРВИС"),
                    Theme.H1("Сервис и обслуживание"),
                    Theme.MutedText("Статусы работ загружаются с AutoDiag Server. Личные записи и напоминания сохраняются на этом iPhone."),
                    Hero(),
                    BuildBookingCard(),
                    BuildServicePlanButton(),
                    _status,
                    Theme.H2("Работы на СТО"),
                    _orders,
                    Theme.H2("Мои записи"),
                    _appointments
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

        var db = await _store.LoadAsync();
        db.Appointments.Add(new MobileAppointmentRecord
        {
            VehicleId = vehicle.Id,
            ClientName = _api.Session?.DisplayName ?? "Клиент",
            StartsAt = startsAt,
            Work = reason,
            Status = "Запланировано"
        });
        await _store.SaveAsync(db);

        _reason.Text = "";
        _status.Text = "Запись сохранена на iPhone.";
        _status.TextColor = Theme.Green;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _orders.Clear();
        _appointments.Clear();

        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            _status.Text = "Сначала выберите автомобиль.";
            _status.TextColor = Theme.Accent;
            _orders.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            _appointments.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            return;
        }

        _status.Text = vehicle.DisplayName;
        _status.TextColor = Theme.Green;

        try
        {
            var orders = (await _api.GetWorkOrdersAsync())
                .Where(x => x.VehicleId == vehicle.Id)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(12)
                .ToList();

            foreach (var order in orders)
            {
                var status = string.IsNullOrWhiteSpace(order.Status) ? "В работе" : order.Status;
                _orders.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Label
                        {
                            Text = string.IsNullOrWhiteSpace(order.Title) ? "Работы по автомобилю" : order.Title,
                            FontSize = 15,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Text,
                            FontAutoScalingEnabled = false
                        },
                        Theme.Pill(status, Theme.Accent),
                        Theme.MutedText(
                            $"{order.UpdatedAt.LocalDateTime:dd.MM.yyyy HH:mm}" +
                            (order.TotalAmount > 0 ? $" • {order.TotalAmount:N2} €" : ""))
                    }
                }, new Thickness(14), 16));
            }

            if (_orders.Count == 0)
                _orders.Add(Theme.CardView(Theme.MutedText("Работ на СТО пока нет.")));

            var db = await _store.LoadAsync();
            var appointments = db.Appointments
                .Where(x => x.VehicleId == vehicle.Id)
                .OrderBy(x => x.StartsAt)
                .ToList();

            foreach (var appointment in appointments)
                _appointments.Add(BuildAppointmentCard(appointment));

            if (_appointments.Count == 0)
                _appointments.Add(Theme.CardView(Theme.MutedText("Запланированных визитов пока нет.")));
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка синхронизации: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private View BuildAppointmentCard(MobileAppointmentRecord appointment)
    {
        var cancel = Theme.CompactButton("Удалить");
        cancel.TextColor = Theme.Red;
        cancel.Clicked += async (_, _) =>
        {
            var yes = await DisplayAlert("Удалить запись", "Удалить эту локальную запись?", "Удалить", "Отмена");
            if (!yes) return;

            var db = await _store.LoadAsync();
            db.Appointments.RemoveAll(x => x.Id == appointment.Id);
            await _store.SaveAsync(db);
            await LoadAsync();
        };

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
            Spacing = 4,
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
                Theme.MutedText($"{appointment.StartsAt.LocalDateTime:dd.MM.yyyy • HH:mm}")
            }
        }, 0, 0);
        top.Add(cancel, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                top,
                Theme.Pill(appointment.Status, Theme.Green)
            }
        }, new Thickness(14), 16);
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
