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

    public ClientServicePage()
    {
        Title = "Сервис";
        BackgroundColor = Theme.Page;
        var book = Theme.PrimaryButton("Новая запись");
        book.Clicked += async (_, _) => await AddAppointmentAsync();
        var plan = Theme.SecondaryButton("План ТО");
        plan.Clicked += async (_, _) => await Shell.Current.GoToAsync("service");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("CLIENT SERVICE"),
                    Theme.H1("Мой сервис"),
                    Theme.MutedText("Запись на СТО, статус работ и сервисная история только по вашему автомобилю."),
                    Hero(),
                    new HorizontalStackLayout { Spacing = 8, Children = { book, plan } },
                    _status,
                    Theme.H2("Работы на СТО"), _orders,
                    Theme.H2("Мои записи"), _appointments
                }
            }
        };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }

    private View Hero()
    {
        var grid = new Grid { HeightRequest = 165 };
        grid.Add(new Image { Source = "login_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.67 });
        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16), Spacing = 6, VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("MY SERVICE", Theme.Green),
                new Label { Text = "Запись и статус ремонта", FontSize = 21, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                Theme.MutedText("Без служебных функций СТО")
            }
        });
        return new Border { Stroke = Theme.Line, StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Content = grid };
    }

    private async Task AddAppointmentAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null) { await DisplayAlert("Запись", "Сначала выберите автомобиль.", "OK"); return; }
        var reason = await DisplayPromptAsync("Запись на СТО", "Что нужно сделать?", "Сохранить", "Отмена", "Например: диагностика, замена масла", maxLength: 120);
        if (string.IsNullOrWhiteSpace(reason)) return;
        var dateText = await DisplayPromptAsync("Дата", "Введите дату в формате ДД.ММ.ГГГГ", "Далее", "Отмена", DateTime.Today.AddDays(1).ToString("dd.MM.yyyy"));
        if (!DateTime.TryParseExact(dateText, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
        { await DisplayAlert("Запись", "Неверный формат даты.", "OK"); return; }

        var db = await _store.LoadAsync();
        db.Appointments.Add(new MobileAppointmentRecord
        {
            VehicleId = v.Id,
            ClientName = _api.Session?.DisplayName ?? "Клиент",
            StartsAt = new DateTimeOffset(date.Date.AddHours(10)),
            Work = reason.Trim(),
            Status = "Запланировано"
        });
        await _store.SaveAsync(db);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _orders.Clear(); _appointments.Clear();
        var v = _state.SelectedVehicle;
        if (v is null) { _status.Text = "Сначала выберите автомобиль."; _status.TextColor = Theme.Accent; return; }

        try
        {
            var orders = (await _api.GetWorkOrdersAsync()).Where(x => x.VehicleId == v.Id).OrderByDescending(x => x.UpdatedAt).Take(10).ToList();
            foreach (var o in orders)
                _orders.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label { Text = string.IsNullOrWhiteSpace(o.Title) ? "Работы" : o.Title, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                        Theme.Pill(string.IsNullOrWhiteSpace(o.Status) ? "В работе" : o.Status, Theme.Accent),
                        Theme.MutedText($"{o.UpdatedAt.LocalDateTime:dd.MM.yyyy HH:mm}" + (o.TotalAmount > 0 ? $" • {o.TotalAmount:N2} €" : ""))
                    }
                }, new Thickness(13)));
            if (_orders.Count == 0) _orders.Add(Theme.CardView(Theme.MutedText("Активных работ нет.")));

            var db = await _store.LoadAsync();
            foreach (var a in db.Appointments.Where(x => x.VehicleId == v.Id).OrderByDescending(x => x.StartsAt))
                _appointments.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label { Text = a.Work, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                        Theme.MutedText($"{a.StartsAt.LocalDateTime:dd.MM.yyyy HH:mm} • {a.Status}")
                    }
                }, new Thickness(13)));
            if (_appointments.Count == 0) _appointments.Add(Theme.CardView(Theme.MutedText("Записей пока нет.")));
            _status.Text = v.DisplayName; _status.TextColor = Theme.Green;
        }
        catch (Exception ex) { _status.Text = ex.Message; _status.TextColor = Theme.Red; }
    }
}
