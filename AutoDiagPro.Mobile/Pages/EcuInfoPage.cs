using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class EcuInfoPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly VerticalStackLayout _result = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Ожидание OBD.");

    public EcuInfoPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "ECU";

        var read = Theme.PrimaryButton("Прочитать ECU / Calibration");
        read.Clicked += async (_, _) => await ReadAsync();

        var pids = Theme.SecondaryButton("Поддерживаемые PID");
        pids.Clicked += async (_, _) => await ReadPidsAsync();

        var readiness = Theme.SecondaryButton("Readiness");
        readiness.Clicked += async (_, _) => await ReadinessAsync();

        var brandScan = Theme.SecondaryButton("Марочный ECU scan");
        brandScan.Clicked += async (_, _) => await BrandScanAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("ECU INSPECTOR"),
                    Theme.H1("Блок управления"),
                    Theme.MutedText("Чтение стандартной OBD-II идентификации: VIN, протокол, Calibration ID, CVN, ECU Name и readiness."),
                    read, pids, readiness, brandScan, _status, _result
                }
            }
        };
    }

    private bool Check()
    {
        if (_obd.IsConnected) return true;
        _status.Text = "Сначала подключите OBD в разделе «Диагностика».";
        _status.TextColor = Theme.Accent;
        return false;
    }

    private async Task ReadAsync()
    {
        if (!Check()) return;
        _status.Text = "Читаю ECU...";
        _result.Clear();
        try
        {
            var data = await _obd.EcuInfoAsync();
            foreach (var x in data)
                _result.Add(Row(x.Key, x.Value));
            _status.Text = "ECU прочитан.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task ReadPidsAsync()
    {
        if (!Check()) return;
        _result.Clear();
        try
        {
            var pids = await _obd.SupportedPidsAsync();
            _result.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    Theme.Eyebrow("SUPPORTED PID"),
                    new Label { Text = pids.Count == 0 ? "Не удалось определить." : string.Join("  ", pids), TextColor = Theme.TextSoft, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap }
                }
            }));
            _status.Text = $"Найдено PID: {pids.Count}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task ReadinessAsync()
    {
        if (!Check()) return;
        _result.Clear();
        try
        {
            var data = await _obd.ReadinessAsync();
            foreach (var x in data)
                _result.Add(Row(x.Key, x.Value));
            _status.Text = "Readiness прочитан.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task BrandScanAsync()
    {
        if (!Check()) return;

        _result.Clear();
        _status.Text = "Проверяю марочный ECU-профиль...";
        _status.TextColor = Theme.Accent;

        try
        {
            var vin = await _obd.VinAsync();
            var brand = _state.SelectedVehicle?.Make;
            if (string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(vin))
                brand = VehicleIdentityService.Decode(vin).Make;

            var isVag = brand is "Volkswagen" or "Audi" or "Škoda" or "SEAT" or "CUPRA" or "Porsche";

            if (!isVag)
            {
                _result.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 7,
                    Children =
                    {
                        Theme.Eyebrow("BRAND ECU SCAN"),
                        Theme.Body($"Марка: {brand ?? "не определена"}"),
                        Theme.MutedText("На iPhone через BLE/Wi-Fi ELM сейчас включён безопасный VAG Engine/TCU read-only scan. Для BMW EDIABAS/K+DCAN/ENET и остальных OEM-протоколов нужен соответствующий марочный транспорт; AutoDiag не будет угадывать адреса блоков.")
                    }
                }));
                _status.Text = "Для этой марки нужен марочный интерфейс.";
                _status.TextColor = Theme.Accent;
                return;
            }

            var topology = await _obd.VagPowertrainTopologyAsync();
            _result.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    Theme.Eyebrow("VAG ECU TOPOLOGY • READ-ONLY"),
                    Theme.MutedText("✓ = блок реально ответил. △ = профиль известен, но текущий интерфейс не подтверждает блок.")
                }
            }));

            foreach (var x in topology)
                _result.Add(Row(x.Key, x.Value));

            _status.Text = "Марочный VAG scan завершён без записи.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View Row(string title, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10
        };
        grid.Add(new Label { Text = title, TextColor = Theme.Muted, FontSize = 12, VerticalTextAlignment = TextAlignment.Center }, 0, 0);
        grid.Add(new Label { Text = value, TextColor = Theme.Text, FontAttributes = FontAttributes.Bold, FontSize = 13, HorizontalOptions = LayoutOptions.End }, 1, 0);
        return Theme.CardView(grid, new Thickness(13));
    }
}
