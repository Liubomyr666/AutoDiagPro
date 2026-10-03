using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;
using System.Net;
using CoreGraphics;
using Foundation;
using UIKit;

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
    private readonly Picker _beforeScan = new() { Title = "Scan ДО ремонта" };
    private readonly Picker _afterScan = new() { Title = "Scan ПОСЛЕ ремонта" };
    private readonly Label _beforeDtc = MetricValue("—");
    private readonly Label _resolvedDtc = MetricValue("—");
    private readonly Label _remainingDtc = MetricValue("—");
    private readonly Label _newDtc = MetricValue("—");
    private readonly Label _compareStatus = Theme.MutedText("Нужны минимум два полных scan одной машины.");
    private readonly Editor _comparison = new()
    {
        IsReadOnly = true,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 260,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text
    };
    private List<DiagnosticScanArchiveMobile> _diagnosticScans = new();
    private RepairScanComparisonMobile? _lastComparison;

    public ReportsPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Отчёты";

        foreach (var picker in new[] { _beforeScan, _afterScan })
        {
            picker.TextColor = Theme.Text;
            picker.BackgroundColor = Theme.Surface;
        }
        _beforeScan.SelectedIndexChanged += (_, _) => CompareSelectedLocalScans();
        _afterScan.SelectedIndexChanged += (_, _) => CompareSelectedLocalScans();

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

        var pdf = Theme.PrimaryButton("Экспорт PDF");
        pdf.Clicked += async (_, _) => await ExportPdfAsync();

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
                    BuildBeforeAfterCard(),
                    refresh, share, pdf, _status,
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

    private View BuildBeforeAfterCard()
    {
        var compare = Theme.PrimaryButton("Сравнить ДО / ПОСЛЕ");
        compare.Clicked += (_, _) => CompareSelectedLocalScans();

        var share = Theme.SecondaryButton("Поделиться сравнением");
        share.Clicked += async (_, _) => await ShareComparisonAsync();

        var metrics = new Grid
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
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        metrics.Add(MetricCard("DTC ДО", _beforeDtc), 0, 0);
        metrics.Add(MetricCard("ИСПРАВЛЕНО", _resolvedDtc), 1, 0);
        metrics.Add(MetricCard("ОСТАЛОСЬ", _remainingDtc), 0, 1);
        metrics.Add(MetricCard("НОВЫЕ", _newDtc), 1, 1);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.Eyebrow("ДО / ПОСЛЕ РЕМОНТА"),
                Theme.MutedText("Выберите два полных scan одной машины. AutoDiag сравнит DTC и общие Live Data параметры."),
                Theme.MutedText("SCAN ДО"),
                _beforeScan,
                Theme.MutedText("SCAN ПОСЛЕ"),
                _afterScan,
                metrics,
                _compareStatus,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { compare, share }
                },
                _comparison
            }
        });
    }

    private static Label MetricValue(string text) => new()
    {
        Text = text,
        TextColor = Theme.Text,
        FontSize = 22,
        FontAttributes = FontAttributes.Bold
    };

    private static View MetricCard(string title, Label value) =>
        Theme.SoftCard(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = title,
                    TextColor = Theme.Muted,
                    FontSize = 10
                },
                value
            }
        });

    private async Task LoadLocalComparisonHistoryAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            _diagnosticScans.Clear();
            _beforeScan.ItemsSource = Array.Empty<string>();
            _afterScan.ItemsSource = Array.Empty<string>();
            ResetLocalComparison("Сначала выберите автомобиль.");
            return;
        }

        var db = await _store.LoadAsync();
        _diagnosticScans = db.DiagnosticScans
            .Where(x => x.VehicleId == vehicle.Id ||
                        (!string.IsNullOrWhiteSpace(vehicle.Vin) &&
                         string.Equals(x.Snapshot.Vin, vehicle.Vin, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Snapshot.CapturedAt)
            .ToList();

        var names = _diagnosticScans.Select(x => x.DisplayName).ToList();
        _beforeScan.ItemsSource = names;
        _afterScan.ItemsSource = names;

        if (_diagnosticScans.Count < 2)
        {
            ResetLocalComparison(_diagnosticScans.Count == 0
                ? "Нет локальных полных scan. Запустите полную диагностику минимум два раза."
                : "Есть только один полный scan. После ремонта выполните контрольный scan.");
            if (_diagnosticScans.Count == 1)
                _afterScan.SelectedIndex = 0;
            return;
        }

        _afterScan.SelectedIndex = 0;
        _beforeScan.SelectedIndex = 1;
        CompareSelectedLocalScans();
    }

    private void CompareSelectedLocalScans()
    {
        if (_beforeScan.SelectedIndex < 0 || _afterScan.SelectedIndex < 0 ||
            _beforeScan.SelectedIndex >= _diagnosticScans.Count ||
            _afterScan.SelectedIndex >= _diagnosticScans.Count)
            return;

        var before = _diagnosticScans[_beforeScan.SelectedIndex];
        var after = _diagnosticScans[_afterScan.SelectedIndex];

        if (before.Id == after.Id)
        {
            ResetLocalComparison("Scan ДО и ПОСЛЕ должны быть разными.");
            return;
        }

        var aVin = VehicleIdentityService.Normalize(before.Snapshot.Vin);
        var bVin = VehicleIdentityService.Normalize(after.Snapshot.Vin);
        if (aVin.Length == 17 && bVin.Length == 17 &&
            !string.Equals(aVin, bVin, StringComparison.OrdinalIgnoreCase))
        {
            ResetLocalComparison("VIN не совпадает — сравнение разных автомобилей заблокировано.");
            return;
        }

        _lastComparison = RepairScanComparisonService.Compare(before.Snapshot, after.Snapshot);
        _beforeDtc.Text = _lastComparison.BeforeCount.ToString();
        _resolvedDtc.Text = _lastComparison.Resolved.Count.ToString();
        _remainingDtc.Text = _lastComparison.Remaining.Count.ToString();
        _newDtc.Text = _lastComparison.Added.Count.ToString();
        _compareStatus.Text = RepairScanComparisonService.Verdict(_lastComparison);
        _compareStatus.TextColor = _lastComparison.Added.Count > 0 ? Theme.Red :
            _lastComparison.Remaining.Count > 0 ? Theme.TextSoft : Theme.Green;

        var vehicleName = _state.SelectedVehicle?.DisplayName ?? "Автомобиль";
        _comparison.Text = RepairScanComparisonService.BuildReport(
            _lastComparison, vehicleName, null, null, null);
    }

    private void ResetLocalComparison(string status)
    {
        _lastComparison = null;
        _beforeDtc.Text = _resolvedDtc.Text = _remainingDtc.Text = _newDtc.Text = "—";
        _compareStatus.Text = status;
        _compareStatus.TextColor = Theme.Muted;
        _comparison.Text = "";
    }

    private async Task ShareComparisonAsync()
    {
        if (_lastComparison is null || string.IsNullOrWhiteSpace(_comparison.Text))
        {
            await DisplayAlert("До / После", "Сначала выберите два полных scan для сравнения.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "AutoDiag Pro — До / После ремонта",
            Text = _comparison.Text
        });
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

        await LoadLocalComparisonHistoryAsync();

        _status.Text = "Собираю данные AutoDiag Cloud...";
        _status.TextColor = Theme.Accent;

        try
        {
            var scansTask = _api.GetScansAsync();
            var ordersTask = _api.GetWorkOrdersAsync();
            var intakesTask = _api.GetIntakesAsync(v.Id);
            var maintenanceTask = _api.GetMaintenanceAsync(v.Id);
            var partsTask = _api.GetInstalledPartsAsync(v.Id);
            var invoicesTask = _api.GetInvoicesAsync(v.Id);
            var photosTask = _api.GetPhotosAsync(v.Id);

            await Task.WhenAll(
                scansTask, ordersTask, intakesTask,
                maintenanceTask, partsTask, invoicesTask, photosTask);

            var scans = (await scansTask)
                .Where(x => x.VehicleId == v.Id || (!string.IsNullOrWhiteSpace(v.Vin) && x.Vin == v.Vin))
                .OrderByDescending(x => x.ScannedAt)
                .Take(10)
                .ToList();

            var orders = (await ordersTask)
                .Where(x => x.VehicleId == v.Id)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(20)
                .ToList();

            var intakes = (await intakesTask).OrderByDescending(x => x.CreatedAt).Take(10).ToList();
            var maintenance = (await maintenanceTask).OrderBy(x => x.DueMileage ?? long.MaxValue).ToList();
            var parts = (await partsTask).OrderByDescending(x => x.InstalledAt).Take(30).ToList();
            var invoices = (await invoicesTask).OrderByDescending(x => x.UpdatedAt).Take(20).ToList();
            var photos = await photosTask;

            ServerVinDecodeRecord? deep = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(v.Vin) && VehicleIdentityService.Normalize(v.Vin).Length == 17)
                    deep = await _api.DecodeVinAsync(v.Vin);
            }
            catch { }

            static string V(string? value, string fallback = "—") =>
                string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

            var lines = new List<string>
            {
                "AUTODIAG PRO — СЕРВИСНЫЙ ОТЧЁТ",
                $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}",
                "",
                $"Автомобиль: {v.DisplayName}",
                $"VIN: {v.Vin ?? "—"}",
                $"Марка: {V(deep?.Make, v.Make ?? "—")}",
                $"Модель: {V(deep?.Model, v.Model ?? "—")}",
                $"Год: {deep?.ParsedYear?.ToString() ?? v.Year?.ToString() ?? "—"}",
                $"Двигатель: {V(deep?.Engine)}" + (string.IsNullOrWhiteSpace(deep?.DisplacementL) ? "" : $" • {deep!.DisplacementL} л"),
                $"Топливо: {V(deep?.FuelType)}",
                $"Коробка: {V(deep?.Transmission)}",
                $"Привод: {V(deep?.DriveType)}",
                $"Кузов: {V(deep?.BodyClass)}",
                $"Госномер: {v.Plate ?? "—"}",
                $"Пробег: {(v.MileageKm is null ? "—" : $"{v.MileageKm:N0} км")}",
                ""
            };

            lines.Add("СРАВНЕНИЕ ДИАГНОСТИК");
            if (_lastComparison is not null)
            {
                lines.Add("Детальный локальный scan ДО / ПОСЛЕ:");
                lines.Add(RepairScanComparisonService.BuildReport(
                    _lastComparison,
                    v.DisplayName,
                    null,
                    null,
                    null));
                lines.Add("");
                lines.Add("AutoDiag Cloud:");
            }

            if (scans.Count == 0)
            {
                lines.Add("Диагностик пока нет.");
            }
            else if (scans.Count == 1)
            {
                var latest = scans[0];
                lines.Add($"Текущая: {latest.ScannedAt.LocalDateTime:dd.MM.yyyy HH:mm} • DTC {latest.DtcCount} • {latest.Protocol}");
                if (!string.IsNullOrWhiteSpace(latest.Summary)) lines.Add(latest.Summary);
            }
            else
            {
                var latest = scans[0];
                var previous = scans[1];
                var delta = latest.DtcCount - previous.DtcCount;
                lines.Add($"ДО:    {previous.ScannedAt.LocalDateTime:dd.MM.yyyy HH:mm} • DTC {previous.DtcCount}");
                lines.Add($"ПОСЛЕ: {latest.ScannedAt.LocalDateTime:dd.MM.yyyy HH:mm} • DTC {latest.DtcCount}");
                lines.Add(delta < 0
                    ? $"Результат: ошибок стало меньше на {Math.Abs(delta)}."
                    : delta > 0
                        ? $"Результат: ошибок стало больше на {delta}."
                        : "Результат: количество DTC не изменилось.");
                if (!string.IsNullOrWhiteSpace(latest.Summary))
                    lines.Add("Последняя проверка: " + latest.Summary);
            }

            lines.Add("");
            lines.Add("ИСТОРИЯ ДИАГНОСТИК");
            foreach (var s in scans)
            {
                lines.Add($"{s.ScannedAt.LocalDateTime:dd.MM.yyyy HH:mm} • DTC {s.DtcCount} • {s.Protocol}");
                if (!string.IsNullOrWhiteSpace(s.Summary)) lines.Add(s.Summary);
            }
            if (scans.Count == 0) lines.Add("Нет данных.");

            lines.Add("");
            lines.Add("ПРИЁМКА");
            if (intakes.Count == 0) lines.Add("Записей приёмки нет.");
            foreach (var intake in intakes)
            {
                lines.Add($"{intake.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm} • {intake.Status}" +
                          (intake.MileageKm is null ? "" : $" • {intake.MileageKm:N0} км") +
                          (intake.FuelPercent is null ? "" : $" • топливо {intake.FuelPercent}%"));
                if (!string.IsNullOrWhiteSpace(intake.Complaint)) lines.Add("Жалоба: " + intake.Complaint);
                if (!string.IsNullOrWhiteSpace(intake.DamageNotes)) lines.Add("Повреждения: " + intake.DamageNotes);
            }

            lines.Add("");
            lines.Add("ЗАКАЗ-НАРЯДЫ / СМЕТЫ");
            if (orders.Count == 0) lines.Add("Нет данных.");
            foreach (var o in orders)
            {
                lines.Add($"{o.Number} • {o.Status} • {o.Title} • {o.TotalAmount:N2} € • смета {FriendlyEstimate(o.EstimateStatus)}");
                if (!string.IsNullOrWhiteSpace(o.ClientDecisionNote))
                    lines.Add("Комментарий клиента: " + o.ClientDecisionNote);
            }

            lines.Add("");
            lines.Add("ПЛАН ТО");
            if (maintenance.Count == 0) lines.Add("План ТО не заполнен.");
            foreach (var m in maintenance)
            {
                var due = new List<string>();
                if (m.DueMileage is not null) due.Add($"{m.DueMileage:N0} км");
                if (m.DueDate is not null) due.Add(m.DueDate.Value.ToString("dd.MM.yyyy"));
                lines.Add($"{m.Name} • {(due.Count == 0 ? "срок не задан" : string.Join(" / ", due))}");
                if (!string.IsNullOrWhiteSpace(m.Notes)) lines.Add(m.Notes);
            }

            lines.Add("");
            lines.Add("УСТАНОВЛЕННЫЕ ДЕТАЛИ");
            if (parts.Count == 0) lines.Add("История деталей пустая.");
            foreach (var part in parts)
            {
                var id = string.Join(" • ", new[] { part.Manufacturer, part.PartNumber }.Where(x => !string.IsNullOrWhiteSpace(x)));
                lines.Add($"{part.InstalledAt.LocalDateTime:dd.MM.yyyy} • {part.Name}" +
                          (string.IsNullOrWhiteSpace(id) ? "" : $" • {id}") +
                          (part.InstalledMileage is null ? "" : $" • {part.InstalledMileage:N0} км"));
                if (part.WarrantyUntil is not null) lines.Add("Гарантия до " + part.WarrantyUntil.Value.ToString("dd.MM.yyyy"));
            }

            lines.Add("");
            lines.Add("СЧЕТА");
            if (invoices.Count == 0) lines.Add("Счетов нет.");
            foreach (var invoice in invoices)
                lines.Add($"{invoice.Number} • {invoice.Amount:N2} € • {(invoice.Paid ? "оплачено" : "не оплачено")}");

            lines.Add("");
            lines.Add($"ФОТО В ИСТОРИИ: {photos.Count}");
            lines.Add($"ДО: {photos.Count(x => x.Kind.Equals("Before", StringComparison.OrdinalIgnoreCase))} • " +
                      $"ПОСЛЕ: {photos.Count(x => x.Kind.Equals("After", StringComparison.OrdinalIgnoreCase))}");

            _report.Text = string.Join(Environment.NewLine, lines);
            _status.Text = "Отчёт готов • AutoDiag Cloud.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task ExportPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(_report.Text))
        {
            await BuildAsync();
            if (string.IsNullOrWhiteSpace(_report.Text)) return;
        }

        try
        {
            _status.Text = "Создаю PDF...";
            _status.TextColor = Theme.Accent;

            var fileName = $"AutoDiag_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            var safe = WebUtility.HtmlEncode(_report.Text)
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>");

            var html =
                "<html><head><meta charset='utf-8'>" +
                "<style>body{font-family:-apple-system,Helvetica,sans-serif;color:#111;font-size:11pt;line-height:1.4}" +
                "h1{font-size:18pt} .brand{color:#b66f0b;font-weight:700}</style></head>" +
                "<body><div class='brand'>AutoDiag Pro</div><br/>" + safe + "</body></html>";

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var renderer = new UIPrintPageRenderer();
                var formatter = new UIMarkupTextPrintFormatter(html);
                renderer.AddPrintFormatter(formatter, 0);

                var paper = new CGRect(0, 0, 595, 842);
                var printable = new CGRect(36, 36, 523, 770);
                renderer.SetValueForKey(NSValue.FromCGRect(paper), new NSString("paperRect"));
                renderer.SetValueForKey(NSValue.FromCGRect(printable), new NSString("printableRect"));

                var data = new NSMutableData();
                UIGraphics.BeginPDFContext(data, paper, null);
                renderer.PrepareForDrawingPages(new NSRange(0, renderer.NumberOfPages));

                for (nint page = 0; page < renderer.NumberOfPages; page++)
                {
                    UIGraphics.BeginPDFPage();
                    renderer.DrawPage(page, paper);
                }

                UIGraphics.EndPDFContext();
                data.Save(NSUrl.FromFilename(path), true);
            });

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "AutoDiag Pro PDF",
                File = new ShareFile(path)
            });

            _status.Text = "PDF готов.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = "PDF: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static string FriendlyEstimate(string? status)
    {
        if (string.Equals(status, "WaitingClient", StringComparison.OrdinalIgnoreCase)) return "ожидает клиента";
        if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)) return "согласована";
        if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)) return "отклонена";
        return "не отправлена";
    }

}
