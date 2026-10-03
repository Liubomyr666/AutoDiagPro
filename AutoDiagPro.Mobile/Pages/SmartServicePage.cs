using System.Globalization;
using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class SmartServicePage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Label _vehicle = Theme.Body("Автомобиль не определён");
    private readonly Label _vin = Theme.MutedText("VIN • —");
    private readonly Label _adapter = Theme.Body("Интерфейс • не подключено");
    private readonly Label _support = Theme.MutedText("Подключите автомобиль для проверки.");
    private readonly Picker _functions = new() { Title = "Сервисная функция" };
    private readonly Label _featureTitle = Theme.H2("Выберите функцию");
    private readonly Label _featureStatus = Theme.MutedText("—");
    private readonly Label _featureDetail = Theme.Body("Здесь появится статус поддержки.");
    private readonly Label _requirements = Theme.MutedText("—");
    private readonly Label _preflight = Theme.MutedText("Preflight ещё не запускался.");
    private readonly Button _open = Theme.PrimaryButton("Открыть / продолжить");
    private readonly Button _check = Theme.SecondaryButton("Preflight");
    private readonly Button _refresh = Theme.SecondaryButton("Обновить поддержку");

    private List<MobileServiceFunctionAvailability> _items = new();
    private string _fuelType = "";
    private bool _preflightOk;

    public SmartServicePage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Smart Service";

        _functions.TextColor = Theme.Text;
        _functions.BackgroundColor = Theme.Surface;
        _functions.SelectedIndexChanged += (_, _) => RenderSelected();
        _refresh.Clicked += async (_, _) => await RefreshAsync();
        _check.Clicked += async (_, _) => await RunPreflightAsync();
        _open.Clicked += async (_, _) => await OpenSelectedAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("SMART SERVICE"),
                    Theme.H1("Сервисные функции"),
                    Theme.MutedText("AutoDiag фильтрует функции по VIN, марке, типу двигателя и текущему интерфейсу. На iPhone ELM/Vgate не считается универсальным write-интерфейсом."),
                    BuildVehicleCard(),
                    _refresh,
                    BuildFunctionCard(),
                    BuildPreflightCard()
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
        await RefreshAsync();
    }

    private View BuildVehicleCard() =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                Theme.Eyebrow("ТЕКУЩИЙ АВТОМОБИЛЬ"),
                _vehicle,
                _vin,
                _adapter,
                _support
            }
        });

    private View BuildFunctionCard() =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.Eyebrow("ДОСТУПНО ДЛЯ ЭТОГО АВТО"),
                _functions,
                _featureTitle,
                _featureStatus,
                _featureDetail,
                Theme.Eyebrow("ТРЕБОВАНИЯ"),
                _requirements,
                _open
            }
        });

    private View BuildPreflightCard() =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.Eyebrow("PREFLIGHT / БЕЗОПАСНОСТЬ"),
                _check,
                _preflight
            }
        });

    private async Task RefreshAsync()
    {
        _preflightOk = false;
        var v = _state.SelectedVehicle;
        var brand = v?.Make?.Trim() ?? "";
        var vin = (v?.Vin ?? "").Trim().ToUpperInvariant();
        _fuelType = "";

        if (vin.Length == 17)
        {
            try
            {
                var decoded = await _api.DecodeVinAsync(vin);
                if (string.IsNullOrWhiteSpace(brand))
                    brand = decoded.Make ?? "";
                _fuelType = decoded.FuelType ?? "";
            }
            catch
            {
                // Offline/decoder failure: keep known vehicle data and do not guess diesel.
            }
        }

        _vehicle.Text = v is null ? "Автомобиль не выбран" : v.DisplayName;
        _vin.Text = vin.Length == 17 ? "VIN • " + vin : "VIN • —";
        _adapter.Text = _obd.IsConnected
            ? $"Интерфейс • {MobileAdapterCapabilityService.DetectFamily(_obd.TransportName, _obd.TransportName)}"
            : "Интерфейс • не подключено";

        _items = MobileServiceFunctionCatalogService
            .Evaluate(brand, _fuelType, _obd.IsConnected)
            .ToList();

        _functions.ItemsSource = _items.Select(x => x.DisplayName).ToList();
        _functions.SelectedIndex = _items.Count > 0 ? 0 : -1;

        _support.Text = string.IsNullOrWhiteSpace(brand)
            ? "Сначала выберите автомобиль с VIN/маркой."
            : _items.Count == 0
                ? "Подтверждённые функции для текущего профиля не определены."
                : $"{_items.Count} функций по профилю • " +
                  (_obd.IsConnected ? "OBD подключён; ECU write через ELM заблокирован" : "OBD не подключён");

        RenderSelected();
    }

    private MobileServiceFunctionAvailability? Selected() =>
        _functions.SelectedIndex >= 0 && _functions.SelectedIndex < _items.Count
            ? _items[_functions.SelectedIndex]
            : null;

    private void RenderSelected()
    {
        var item = Selected();
        if (item is null)
        {
            _featureTitle.Text = "Нет подтверждённых функций";
            _featureStatus.Text = "—";
            _featureDetail.Text = "Определите автомобиль и обновите поддержку.";
            _requirements.Text = "—";
            _open.IsEnabled = false;
            return;
        }

        _featureTitle.Text = item.Name;
        _featureStatus.Text = item.Status;
        _featureStatus.TextColor = item.WriteReady ? Theme.Green : Theme.Accent;
        _featureDetail.Text = item.Detail;
        _requirements.Text = item.Requirements;
        _open.IsEnabled = true;
        _open.Text = !string.IsNullOrWhiteSpace(item.Route)
            ? "Открыть раздел"
            : item.WriteReady ? "Продолжить" : "Что требуется";
    }

    private async Task RunPreflightAsync()
    {
        if (!_obd.IsConnected)
        {
            _preflightOk = false;
            _preflight.Text = "❌ OBD не подключён. Подключите адаптер в разделе «Диагностика».";
            _preflight.TextColor = Theme.Red;
            return;
        }

        _preflight.Text = "Проверяю VIN, питание, протокол и DTC...";
        _preflight.TextColor = Theme.Accent;
        var lines = new List<string>();
        var ok = true;

        try
        {
            var expectedVin = (_state.SelectedVehicle?.Vin ?? "").Trim().ToUpperInvariant();
            var ecuVin = (await _obd.VinAsync()).Trim().ToUpperInvariant();
            var voltage = await _obd.VoltageAsync();
            var protocol = await _obd.ProtocolAsync();
            var dtc = await _obd.DtcAsync();

            if (expectedVin.Length == 17 && ecuVin.Length == 17 &&
                !string.Equals(expectedVin, ecuVin, StringComparison.OrdinalIgnoreCase))
            {
                ok = false;
                lines.Add($"❌ VIN не совпадает: карточка {expectedVin}, ECU {ecuVin}");
            }
            else
            {
                lines.Add($"✓ VIN: {(ecuVin.Length == 17 ? ecuVin : "не подтверждён")}");
            }

            if (TryVoltage(voltage, out var volts))
            {
                if (volts < 11.8)
                {
                    ok = false;
                    lines.Add($"❌ Питание {volts:0.0} V — слишком низкое для write-процедуры.");
                }
                else
                    lines.Add($"✓ Питание: {volts:0.0} V");
            }
            else
            {
                ok = false;
                lines.Add("△ Напряжение не подтверждено.");
            }

            lines.Add($"✓ Протокол: {protocol}");
            lines.Add($"DTC двигателя: {dtc.Count}");

            var item = Selected();
            if (item is not null && !item.WriteReady && string.IsNullOrWhiteSpace(item.Route))
            {
                ok = false;
                lines.Add("🔒 iPhone BLE/Wi-Fi ELM не является OEM write-интерфейсом для этой функции.");
            }

            lines.Add(ok
                ? "✓ PREFLIGHT ПРОЙДЕН для доступной мобильной операции."
                : "WRITE ЗАБЛОКИРОВАН • устраните пункты выше или используйте поддержанный OEM backend.");
        }
        catch (Exception ex)
        {
            ok = false;
            lines.Add("❌ Ошибка preflight: " + ex.Message);
        }

        _preflightOk = ok;
        _preflight.Text = string.Join(Environment.NewLine, lines);
        _preflight.TextColor = ok ? Theme.Green : Theme.Accent;
    }

    private async Task OpenSelectedAsync()
    {
        var item = Selected();
        if (item is null) return;

        if (item.Route == "service")
        {
            await Shell.Current.GoToAsync("service");
            return;
        }

        if (item.Route == "injectors")
        {
            await Shell.Current.GoToAsync("injectors");
            return;
        }

        if (item.Route == "programming")
        {
            _state.PendingModuleTitle = "АКБ / Battery Coding";
            _state.PendingModuleSubtitle = "Battery registration / coding profile";
            await Shell.Current.GoToAsync("programming");
            return;
        }

        if (!item.WriteReady)
        {
            await DisplayAlert(
                "Smart Service • нужен OEM-интерфейс",
                item.Name + Environment.NewLine + Environment.NewLine + item.Requirements,
                "OK");
            return;
        }

        if (!_preflightOk)
        {
            await DisplayAlert(
                "Smart Service",
                "Сначала выполните Preflight.",
                "OK");
            return;
        }

        await DisplayAlert(
            "Smart Service",
            "Preflight пройден. Для этой функции нужна проверенная ECU-specific write-процедура; универсальная команда не отправлялась.",
            "OK");
    }

    private static bool TryVoltage(string text, out double volts)
    {
        volts = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Replace(',', '.');
        var token = new string(normalized
            .SkipWhile(c => !char.IsDigit(c))
            .TakeWhile(c => char.IsDigit(c) || c == '.')
            .ToArray());
        return double.TryParse(token, NumberStyles.Float,
            CultureInfo.InvariantCulture, out volts);
    }
}