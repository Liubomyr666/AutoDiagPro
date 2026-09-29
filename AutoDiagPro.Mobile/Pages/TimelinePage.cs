using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class TimelinePage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly VerticalStackLayout _items = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Собираю историю...");

    public TimelinePage()
    {
        Title = "История автомобиля";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 36),
                Spacing = 13,
                Children =
                {
                    Theme.Eyebrow("VEHICLE TIMELINE"),
                    Theme.H1("Вся история автомобиля"),
                    Theme.MutedText("Диагностика • ремонт • ТО • детали • счета • записи • износ"),
                    _status,
                    _items
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
        _items.Clear();
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            _status.Text = "Сначала выберите автомобиль.";
            _status.TextColor = Theme.Accent;
            return;
        }

        var events = new List<TimelineItem>();
        try
        {
            var scansTask = _api.GetScansAsync();
            var ordersTask = _api.GetWorkOrdersAsync();
            var dbTask = _store.LoadAsync();
            await Task.WhenAll(scansTask, ordersTask, dbTask);

            foreach (var scan in (await scansTask).Where(x =>
                         x.VehicleId == vehicle.Id ||
                         (!string.IsNullOrWhiteSpace(vehicle.Vin) &&
                          string.Equals(x.Vin, vehicle.Vin, StringComparison.OrdinalIgnoreCase))))
                events.Add(new(scan.ScannedAt, "ДИАГНОСТИКА",
                    $"DTC {scan.DtcCount} • {scan.Protocol ?? "протокол —"}",
                    string.IsNullOrWhiteSpace(scan.Summary) ? "Scan сохранён" : Short(scan.Summary)));

            foreach (var order in (await ordersTask).Where(x => x.VehicleId == vehicle.Id))
                events.Add(new(order.UpdatedAt, "РЕМОНТ",
                    $"{order.Number} • {FriendlyStatus(order.Status)}",
                    $"{order.Title}{(order.TotalAmount > 0 ? $" • {order.TotalAmount:N2} €" : "")}"));

            var db = await dbTask;

            foreach (var item in db.RepairCases.Where(x => x.VehicleId == vehicle.Id))
                events.Add(new(item.UpdatedAt, "REPAIR BRAIN", item.Status,
                    Join(item.ConfirmedCause, item.RepairDone, item.Notes)));

            foreach (var item in db.Appointments.Where(x => x.VehicleId == vehicle.Id))
                events.Add(new(item.StartsAt, "ЗАПИСЬ", item.Status, item.Work));

            foreach (var item in db.Invoices.Where(x => x.VehicleId == vehicle.Id))
                events.Add(new(item.CreatedAt, "СЧЁТ",
                    $"{item.Number} • {item.Amount:N2} € • {(item.Paid ? "ОПЛАЧЕНО" : "НЕ ОПЛАЧЕНО")}",
                    item.Description));

            foreach (var item in db.ReceivedParts.Where(x => x.VehicleId == vehicle.Id))
                events.Add(new(item.ReceivedAt, "ДЕТАЛЬ", item.Name,
                    $"{item.Code} • {item.Quantity} шт."));

            foreach (var item in db.WearChecks.Where(x => x.VehicleId == vehicle.Id))
                events.Add(new(item.CheckedAt, "ИЗНОС", "Шины / колодки",
                    $"Шины: {Mm(item.TireFrontMm)}/{Mm(item.TireRearMm)} мм • колодки: {Mm(item.PadFrontMm)}/{Mm(item.PadRearMm)} мм"));

            var plan = db.ServicePlans.FirstOrDefault(x => x.VehicleId == vehicle.Id);
            if (plan is not null)
                events.Add(new(DateTimeOffset.Now, "ПЛАН ТО", "Следующее обслуживание",
                    $"Пробег: {plan.NextServiceMileageKm?.ToString("N0") ?? "—"} км • дата: {plan.NextServiceDate?.LocalDateTime:dd.MM.yyyy}"));

            foreach (var item in events.OrderByDescending(x => x.Date).Take(250))
                _items.Add(Card(item));

            _status.Text = $"{vehicle.DisplayName} • событий: {events.Count}";
            _status.TextColor = Theme.Green;
            if (events.Count == 0)
                _items.Add(Theme.CardView(Theme.MutedText("История пока пустая.")));
        }
        catch (Exception ex)
        {
            _status.Text = "История: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View Card(TimelineItem item) => Theme.CardView(new VerticalStackLayout
    {
        Spacing = 5,
        Children =
        {
            new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    Theme.Pill(item.Category, Theme.Accent),
                    Theme.MutedText(item.Date.LocalDateTime.ToString("dd.MM.yyyy • HH:mm"))
                }
            },
            new Label { Text = item.Title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
            Theme.MutedText(string.IsNullOrWhiteSpace(item.Details) ? "—" : item.Details)
        }
    }, new Thickness(14), 16);

    private static string Mm(double? value) => value.HasValue ? value.Value.ToString("0.0") : "—";
    private static string Short(string? text) => string.IsNullOrWhiteSpace(text)
        ? ""
        : text.Replace("\r", " ").Replace("\n", " ").Trim() is var s && s.Length > 220 ? s[..220] + "…" : s;
    private static string Join(params string?[] values) =>
        string.Join(" • ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    private static string FriendlyStatus(string? value) => string.IsNullOrWhiteSpace(value) ? "В работе" : value!;
    private sealed record TimelineItem(DateTimeOffset Date, string Category, string Title, string Details);
}
