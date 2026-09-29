using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class NotificationsPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly VerticalStackLayout _list = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Проверяю события...");

    public NotificationsPage()
    {
        Title = "Уведомления";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        var read = Theme.SecondaryButton("Отметить всё прочитанным");
        read.Clicked += async (_, _) => await MarkAllReadAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 36),
                Spacing = 13,
                Children =
                {
                    Theme.Eyebrow("NOTIFICATION CENTER"),
                    Theme.H1("Уведомления"),
                    Theme.MutedText("ТО, ошибки диагностики, записи, неоплаченные счета и синхронизация."),
                    read,
                    _status,
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await GenerateAsync();
        await RenderAsync();
    }

    private async Task GenerateAsync()
    {
        var db = await _store.LoadAsync();
        var vehicle = _state.SelectedVehicle;

        void Add(Guid? vehicleId, string key, string kind, string title, string message)
        {
            if (db.Notifications.Any(x => x.Key == key)) return;
            db.Notifications.Add(new MobileNotificationRecord
            {
                VehicleId = vehicleId,
                Key = key,
                Kind = kind,
                Title = title,
                Message = message,
                CreatedAt = DateTimeOffset.Now
            });
        }

        if (vehicle is not null)
        {
            var plan = db.ServicePlans.FirstOrDefault(x => x.VehicleId == vehicle.Id);
            if (plan?.NextServiceDate is DateTimeOffset date && date <= DateTimeOffset.Now.AddDays(30))
            {
                Add(vehicle.Id, $"service-date-{vehicle.Id}-{date:yyyyMMdd}", "ТО", "Скоро обслуживание",
                    $"План ТО: {date.LocalDateTime:dd.MM.yyyy}.");
                var remindAt = date.AddDays(-1);
                if (remindAt <= DateTimeOffset.Now) remindAt = DateTimeOffset.Now.AddMinutes(2);
                await LocalNotificationService.ScheduleAsync(
                    $"service-{vehicle.Id}-{date:yyyyMMdd}",
                    "AutoDiag Pro • ТО",
                    $"Обслуживание запланировано на {date.LocalDateTime:dd.MM.yyyy}.",
                    remindAt);
            }

            if (plan?.NextServiceMileageKm is long km && vehicle.MileageKm is long current && km - current <= 1500)
                Add(vehicle.Id, $"service-km-{vehicle.Id}-{km}", "ТО", "Скоро обслуживание",
                    $"До планового ТО осталось примерно {Math.Max(0, km - current):N0} км.");

            try
            {
                var last = (await _api.GetScansAsync())
                    .Where(x => x.VehicleId == vehicle.Id ||
                                (!string.IsNullOrWhiteSpace(vehicle.Vin) &&
                                 string.Equals(x.Vin, vehicle.Vin, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(x => x.ScannedAt)
                    .FirstOrDefault();
                if (last is { DtcCount: > 0 })
                    Add(vehicle.Id, $"scan-dtc-{last.Id}", "DTC", "Найдены ошибки",
                        $"Последняя диагностика: {last.DtcCount} DTC. Откройте отчёт и AI-разбор.");
            }
            catch { }
        }

        foreach (var appointment in db.Appointments.Where(x =>
                     x.Status != "Готово" &&
                     x.StartsAt >= DateTimeOffset.Now &&
                     x.StartsAt <= DateTimeOffset.Now.AddHours(48)))
        {
            Add(appointment.VehicleId, $"appointment-{appointment.Id}", "ЗАПИСЬ", "Скоро запись на СТО",
                $"{appointment.StartsAt.LocalDateTime:dd.MM HH:mm} • {appointment.Work}");

            var remindAt = appointment.StartsAt.AddHours(-1);
            if (remindAt <= DateTimeOffset.Now) remindAt = DateTimeOffset.Now.AddMinutes(2);
            await LocalNotificationService.ScheduleAsync(
                $"appointment-{appointment.Id}",
                "AutoDiag Pro • Запись на СТО",
                $"{appointment.StartsAt.LocalDateTime:dd.MM HH:mm} • {appointment.Work}",
                remindAt);
        }

        foreach (var invoice in db.Invoices.Where(x => !x.Paid))
            Add(invoice.VehicleId, $"invoice-{invoice.Id}", "СЧЁТ", "Есть неоплаченный счёт",
                $"{invoice.Number} • {invoice.Amount:N2} €");

        if (db.PendingScans.Count > 0)
            Add(vehicle?.Id, $"offline-{db.PendingScans.Count}", "СИНХРОНИЗАЦИЯ", "Есть данные без синхронизации",
                $"Ожидают отправки scan: {db.PendingScans.Count}. Они отправятся при доступном сервере.");

        await _store.SaveAsync(db);
    }

    private async Task RenderAsync()
    {
        _list.Clear();
        var db = await _store.LoadAsync();
        var vehicle = _state.SelectedVehicle;
        var items = db.Notifications
            .Where(x => vehicle is null || x.VehicleId is null || x.VehicleId == vehicle.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToList();

        var unread = items.Count(x => !x.IsRead);
        _status.Text = $"Непрочитанных: {unread} • всего: {items.Count}";
        _status.TextColor = unread > 0 ? Theme.Accent : Theme.Green;

        foreach (var item in items)
        {
            var mark = Theme.CompactButton(item.IsRead ? "Прочитано" : "Прочитать");
            mark.IsEnabled = !item.IsRead;
            mark.Clicked += async (_, _) =>
            {
                var data = await _store.LoadAsync();
                var target = data.Notifications.FirstOrDefault(x => x.Id == item.Id);
                if (target is not null) target.IsRead = true;
                await _store.SaveAsync(data);
                await RenderAsync();
            };

            _list.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { Theme.Pill(item.Kind, item.IsRead ? Theme.Muted : Theme.Accent),
                                     Theme.MutedText(item.CreatedAt.LocalDateTime.ToString("dd.MM • HH:mm")) }
                    },
                    new Label { Text = item.Title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                    Theme.Body(item.Message),
                    mark
                }
            }, new Thickness(14), 16));
        }

        if (items.Count == 0)
            _list.Add(Theme.CardView(Theme.MutedText("Новых событий нет.")));
    }

    private async Task MarkAllReadAsync()
    {
        var db = await _store.LoadAsync();
        foreach (var item in db.Notifications) item.IsRead = true;
        await _store.SaveAsync(db);
        await RenderAsync();
    }
}
