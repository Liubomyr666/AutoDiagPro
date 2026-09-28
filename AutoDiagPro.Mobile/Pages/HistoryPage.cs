using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class HistoryPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly Label _status = Theme.MutedText("Загрузка истории...");
    private readonly Picker _filter = new()
    {
        Title = "Тип события",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        TitleColor = Theme.Muted
    };

    private List<TimelineItem> _items = new();

    public HistoryPage()
    {
        Title = "История";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        _filter.ItemsSource = new[]
        {
            "Все", "Диагностика", "Ремонт", "Приёмка", "ТО", "Детали",
            "Износ", "Счета", "Запись", "Фото"
        };
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => Render();

        _list.ItemTemplate = new DataTemplate(BuildCard);

        var refresh = Theme.SecondaryButton("Обновить из облака");
        refresh.Clicked += async (_, _) => await LoadAsync();

        var header = new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.Eyebrow("VEHICLE TIMELINE • CLOUD"),
                Theme.H1("История автомобиля"),
                Theme.MutedText("Диагностика, ремонт, ТО, детали, износ, счета и записи — в одной временной ленте."),
                _status,
                _filter,
                refresh
            }
        };

        var root = new Grid
        {
            Padding = new Thickness(16, 18, 16, 24),
            RowSpacing = 14,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(header, 0, 0);
        root.Add(_list, 0, 1);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _status.Text = "Синхронизация истории...";
        _status.TextColor = Theme.Accent;

        try
        {
            if (_state.Vehicles.Count == 0)
                _state.Vehicles = await _api.GetVehiclesAsync();

            var selected = _state.SelectedVehicle;
            var scope = selected is null
                ? _state.Vehicles
                : _state.Vehicles.Where(x => x.Id == selected.Id).ToList();
            var allowedIds = scope.Select(x => x.Id).ToHashSet();

            var healthTask = _api.GetHealthAsync();
            var scansTask = _api.GetScansAsync();
            var ordersTask = _api.GetWorkOrdersAsync();
            var intakesTask = _api.GetIntakesAsync(selected?.Id);
            var appointmentsTask = _api.GetAppointmentsAsync(selected?.Id);
            var invoicesTask = _api.GetInvoicesAsync(selected?.Id);
            var photosTask = _api.GetPhotosAsync(selected?.Id);

            await Task.WhenAll(
                healthTask, scansTask, ordersTask, intakesTask,
                appointmentsTask, invoicesTask, photosTask);

            var events = new List<TimelineItem>();

            foreach (var scan in await scansTask)
            {
                if (scan.VehicleId is Guid vehicleId && !allowedIds.Contains(vehicleId)) continue;
                if (selected is not null && scan.VehicleId is null &&
                    !string.Equals(scan.Vin, selected.Vin, StringComparison.OrdinalIgnoreCase)) continue;

                var vehicle = scan.VehicleId is Guid id ? VehicleLabel(id) : scan.Vin ?? "Автомобиль";
                events.Add(new TimelineItem(
                    scan.ScannedAt,
                    "Диагностика",
                    $"DTC {scan.DtcCount} • {scan.Protocol ?? "OBD"}",
                    Join(vehicle, scan.Summary),
                    scan.DtcCount > 0 ? "ВНИМАНИЕ" : "OK"));
            }

            foreach (var order in await ordersTask)
            {
                if (order.VehicleId is not Guid id || !allowedIds.Contains(id)) continue;
                events.Add(new TimelineItem(
                    order.UpdatedAt == default ? order.CreatedAt : order.UpdatedAt,
                    "Ремонт",
                    $"{order.Number} • {order.Status}",
                    Join(VehicleLabel(id), order.Title, $"{order.TotalAmount:N2} €", "смета " + order.EstimateStatus),
                    order.Status));
            }

            foreach (var intake in await intakesTask)
            {
                if (!allowedIds.Contains(intake.VehicleId)) continue;
                var details = new List<string> { VehicleLabel(intake.VehicleId) };
                if (intake.MileageKm is not null) details.Add($"{intake.MileageKm:N0} км");
                if (intake.FuelPercent is not null) details.Add($"топливо {intake.FuelPercent}%");
                if (!string.IsNullOrWhiteSpace(intake.Complaint)) details.Add(intake.Complaint);
                events.Add(new TimelineItem(intake.CreatedAt, "Приёмка", intake.Status, string.Join(" • ", details), "СТО"));
            }

            foreach (var appointment in await appointmentsTask)
            {
                if (appointment.VehicleId is Guid id && !allowedIds.Contains(id)) continue;
                events.Add(new TimelineItem(
                    appointment.StartsAt,
                    "Запись",
                    appointment.Work,
                    Join(appointment.VehicleId is Guid vid ? VehicleLabel(vid) : appointment.ClientName, appointment.Status),
                    appointment.Status));
            }

            foreach (var invoice in await invoicesTask)
            {
                if (invoice.VehicleId is Guid id && !allowedIds.Contains(id)) continue;
                events.Add(new TimelineItem(
                    invoice.CreatedAt,
                    "Счета",
                    $"{invoice.Number} • {invoice.Amount:N2} €",
                    Join(invoice.VehicleId is Guid vid ? VehicleLabel(vid) : null, invoice.Description),
                    invoice.Paid ? "ОПЛАЧЕНО" : "НЕ ОПЛАЧЕНО"));
            }

            foreach (var photo in await photosTask)
            {
                if (!allowedIds.Contains(photo.VehicleId)) continue;
                events.Add(new TimelineItem(
                    photo.CreatedAt,
                    "Фото",
                    string.IsNullOrWhiteSpace(photo.Kind) ? "Фото автомобиля" : photo.Kind,
                    Join(VehicleLabel(photo.VehicleId), photo.Caption, $"{photo.SizeBytes / 1024.0:0} КБ"),
                    "ФОТО"));
            }

            foreach (var vehicle in scope)
            {
                var maintenanceTask = _api.GetMaintenanceAsync(vehicle.Id);
                var partsTask = _api.GetInstalledPartsAsync(vehicle.Id);
                var wearTask = _api.GetWearChecksAsync(vehicle.Id);
                await Task.WhenAll(maintenanceTask, partsTask, wearTask);

                foreach (var m in await maintenanceTask)
                {
                    var due = new List<string>();
                    if (m.DueMileage is not null) due.Add($"{m.DueMileage:N0} км");
                    if (m.DueDate is not null) due.Add(m.DueDate.Value.ToString("dd.MM.yyyy"));
                    events.Add(new TimelineItem(
                        m.UpdatedAt,
                        "ТО",
                        m.Name,
                        Join(vehicle.DisplayName, due.Count == 0 ? null : "следующее: " + string.Join(" / ", due), m.Notes),
                        "СЕРВИС"));
                }

                foreach (var part in await partsTask)
                {
                    var partId = Join(part.Manufacturer, part.PartNumber);
                    events.Add(new TimelineItem(
                        part.InstalledAt,
                        "Детали",
                        part.Name,
                        Join(vehicle.DisplayName, partId, part.InstalledMileage is null ? null : $"{part.InstalledMileage:N0} км",
                            part.WarrantyUntil is null ? null : "гарантия до " + part.WarrantyUntil.Value.ToString("dd.MM.yyyy")),
                        "УСТАНОВЛЕНО"));
                }

                foreach (var wear in await wearTask)
                {
                    var attention =
                        (wear.TireFrontMm is not null && wear.TireFrontMm < 2) ||
                        (wear.TireRearMm is not null && wear.TireRearMm < 2) ||
                        (wear.PadFrontMm is not null && wear.PadFrontMm < 3) ||
                        (wear.PadRearMm is not null && wear.PadRearMm < 3);

                    events.Add(new TimelineItem(
                        wear.CheckedAt,
                        "Износ",
                        $"Шины {Fmt(wear.TireFrontMm)}/{Fmt(wear.TireRearMm)} мм • колодки {Fmt(wear.PadFrontMm)}/{Fmt(wear.PadRearMm)} мм",
                        Join(vehicle.DisplayName, wear.Notes),
                        attention ? "ПРОВЕРИТЬ" : "OK"));
                }
            }

            _items = events
                .OrderByDescending(x => x.Date)
                .Take(300)
                .ToList();

            Render();

            var health = await healthTask;
            _status.Text = selected is null
                ? $"AutoDiag Cloud {health.Version} • событий {_items.Count} • все доступные авто"
                : $"AutoDiag Cloud {health.Version} • {selected.DisplayName} • событий {_items.Count}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _items.Clear();
            _list.ItemsSource = Array.Empty<TimelineItem>();
            _status.Text = "История: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private void Render()
    {
        var filter = _filter.SelectedItem?.ToString() ?? "Все";
        _list.ItemsSource = filter == "Все"
            ? _items
            : _items.Where(x => x.Category == filter).ToList();
    }

    private string VehicleLabel(Guid id) =>
        _state.Vehicles.FirstOrDefault(x => x.Id == id)?.DisplayName ?? "Автомобиль";

    private static View BuildCard()
    {
        var date = new Label { FontSize = 11, TextColor = Theme.Muted, FontAutoScalingEnabled = false };
        date.SetBinding(Label.TextProperty, nameof(TimelineItem.DateLabel));

        var category = new Label
        {
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Accent,
            FontAutoScalingEnabled = false
        };
        category.SetBinding(Label.TextProperty, nameof(TimelineItem.Category));

        var title = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text,
            LineBreakMode = LineBreakMode.WordWrap,
            FontAutoScalingEnabled = false
        };
        title.SetBinding(Label.TextProperty, nameof(TimelineItem.Title));

        var subtitle = new Label
        {
            FontSize = 11,
            TextColor = Theme.Muted,
            MaxLines = 5,
            LineBreakMode = LineBreakMode.TailTruncation,
            FontAutoScalingEnabled = false
        };
        subtitle.SetBinding(Label.TextProperty, nameof(TimelineItem.Subtitle));

        var badge = new Label
        {
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Green,
            FontAutoScalingEnabled = false
        };
        badge.SetBinding(Label.TextProperty, nameof(TimelineItem.Badge));

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children = { date, category, title, subtitle, badge }
        }, new Thickness(14), 16);
    }

    private static string Join(params string?[] parts) =>
        string.Join(" • ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Fmt(double? value) =>
        value is null ? "—" : value.Value.ToString("0.0");

    private sealed record TimelineItem(
        DateTimeOffset Date,
        string Category,
        string Title,
        string Subtitle,
        string Badge)
    {
        public string DateLabel => Date.LocalDateTime.ToString("dd.MM.yyyy • HH:mm");
    }
}
