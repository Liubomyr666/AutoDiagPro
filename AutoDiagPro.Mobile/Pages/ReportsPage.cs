using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ReportsPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly Editor _report = new()
    {
        IsReadOnly = true,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 300,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text
    };
    private readonly Label _status = Theme.MutedText("Готовлю отчёт...");

    public ReportsPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Отчёты";

        var refresh = Theme.PrimaryButton("Собрать отчёт");
        refresh.Clicked += async (_, _) => await BuildAsync();

        var share = Theme.SecondaryButton("Поделиться отчётом");
        share.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_report.Text)) return;
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "AutoDiag Pro Report",
                Text = _report.Text
            });
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("REPORT CENTER"),
                    Theme.H1("Отчёт по автомобилю"),
                    Theme.MutedText(AccessPolicy.IsClient ? "Диагностика, DTC, работы на СТО и сервисный план выбранного автомобиля." : "История scan, DTC, заказ-наряды, Repair Brain и сервисный план в одном отчёте."),
                    refresh, share, _status,
                    Theme.CardView(_report)
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await BuildAsync();
    }

    private async Task BuildAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _status.Text = "Сначала выберите автомобиль.";
            _status.TextColor = Theme.Accent;
            return;
        }

        _status.Text = "Собираю данные...";
        _status.TextColor = Theme.Accent;

        try
        {
            var scans = (await _api.GetScansAsync())
                .Where(x => x.VehicleId == v.Id || (!string.IsNullOrWhiteSpace(v.Vin) && x.Vin == v.Vin))
                .OrderByDescending(x => x.ScannedAt)
                .Take(10)
                .ToList();

            var orders = (await _api.GetWorkOrdersAsync())
                .Where(x => x.VehicleId == v.Id)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(10)
                .ToList();

            var db = await _store.LoadAsync();
            var repair = db.RepairCases.Where(x => x.VehicleId == v.Id).OrderByDescending(x => x.UpdatedAt).Take(5).ToList();
            var plan = db.ServicePlans.FirstOrDefault(x => x.VehicleId == v.Id);

            var lines = new List<string>
            {
                "AUTODIAG PRO — ОТЧЁТ",
                $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}",
                "",
                $"Автомобиль: {v.DisplayName}",
                $"VIN: {v.Vin ?? "—"}",
                $"Пробег: {(v.MileageKm is null ? "—" : $"{v.MileageKm:N0} км")}",
                ""
            };

            lines.Add("ПОСЛЕДНЯЯ ДИАГНОСТИКА");
            if (scans.Count == 0) lines.Add("Нет данных.");
            foreach (var s in scans)
            {
                lines.Add($"{s.ScannedAt.LocalDateTime:dd.MM.yyyy HH:mm} • DTC {s.DtcCount} • {s.Protocol}");
                if (!string.IsNullOrWhiteSpace(s.Summary)) lines.Add(s.Summary);
                lines.Add("");
            }

            lines.Add("ЗАКАЗ-НАРЯДЫ");
            if (orders.Count == 0) lines.Add("Нет данных.");
            foreach (var o in orders)
                lines.Add($"{o.Number} • {o.Status} • {o.Title} • {o.TotalAmount:N2} €");

            if (AccessPolicy.IsStaff)
            {
                lines.Add("");
                lines.Add("REPAIR BRAIN");
                if (repair.Count == 0) lines.Add("Нет локальных кейсов.");
                foreach (var x in repair)
                {
                    lines.Add($"{x.UpdatedAt.LocalDateTime:dd.MM.yyyy} • {x.Status}");
                    if (!string.IsNullOrWhiteSpace(x.DtcCodes)) lines.Add("DTC: " + x.DtcCodes);
                    if (!string.IsNullOrWhiteSpace(x.ConfirmedCause)) lines.Add("Причина: " + x.ConfirmedCause);
                    if (!string.IsNullOrWhiteSpace(x.RepairDone)) lines.Add("Ремонт: " + x.RepairDone);
                    lines.Add("");
                }
            }

            lines.Add("");
            lines.Add("СЕРВИС / ТО");
            if (plan is null)
            {
                lines.Add("План ТО не заполнен.");
            }
            else
            {
                lines.Add($"Текущий пробег: {plan.CurrentMileageKm?.ToString("N0") ?? "—"} км");
                lines.Add($"Следующее ТО: {plan.NextServiceMileageKm?.ToString("N0") ?? "—"} км • {plan.NextServiceDate?.LocalDateTime:dd.MM.yyyy}");
                if (!string.IsNullOrWhiteSpace(plan.OilSpec)) lines.Add("Масло: " + plan.OilSpec);
                if (!string.IsNullOrWhiteSpace(plan.Notes)) lines.Add("Работы: " + plan.Notes);
            }

            _report.Text = string.Join(Environment.NewLine, lines);
            _status.Text = "Отчёт готов.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }
}
