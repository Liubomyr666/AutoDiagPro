using System.Globalization;
using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ProgrammingCenterPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();

    private readonly Label _title = Theme.H1("Programming Center");
    private readonly Label _subtitle = Theme.MutedText("");
    private readonly Label _status = Theme.MutedText("Проверка не запускалась.");
    private readonly VerticalStackLayout _ecu = new() { Spacing = 8 };
    private readonly Entry _batteryAh = Field("Ёмкость АКБ, Ah", Keyboard.Numeric);
    private readonly Picker _batteryType = new() { Title = "Тип АКБ", ItemsSource = new[] { "AGM", "EFB", "Обычный свинцово-кислотный", "Li-ion" } };
    private readonly Entry _batteryMaker = Field("Производитель / серийный номер");
    private readonly VerticalStackLayout _batteryPanel = new() { Spacing = 9 };

    public ProgrammingCenterPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Programming";

        _batteryType.BackgroundColor = Theme.Surface;
        _batteryType.TextColor = Theme.Text;
        _batteryType.SelectedIndex = 0;

        _batteryPanel.Children.Add(Theme.Eyebrow("BATTERY PROFILE"));
        _batteryPanel.Children.Add(_batteryAh);
        _batteryPanel.Children.Add(_batteryType);
        _batteryPanel.Children.Add(_batteryMaker);

        var check = Theme.PrimaryButton("Проверить автомобиль и адаптер");
        check.Clicked += async (_, _) => await CheckAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("PROGRAMMING CENTER"),
                    _title,
                    _subtitle,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            Theme.Eyebrow("ПРАВИЛО БЕЗОПАСНОСТИ"),
                            Theme.Body("AutoDiag сначала читает VIN, ECU/Calibration и питание. Запись в блок не запускается через обычный ELM327/Vgate, если для автомобиля нужен марочный протокол, J2534/DoIP/ENET или другой совместимый интерфейс.")
                        }
                    }),
                    check,
                    _status,
                    _ecu,
                    Theme.CardView(_batteryPanel)
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var name = string.IsNullOrWhiteSpace(_state.PendingModuleTitle) ? "Программирование ECU" : _state.PendingModuleTitle;
        _title.Text = name;
        _subtitle.Text = Description(name);
        _batteryPanel.IsVisible = name.Contains("Battery", StringComparison.OrdinalIgnoreCase) ||
                                  name.Contains("АКБ", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CheckAsync()
    {
        _ecu.Clear();

        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Сначала откройте «Диагностика».";
            _status.TextColor = Theme.Accent;
            return;
        }

        try
        {
            _status.Text = "Читаю идентификацию и питание...";
            _status.TextColor = Theme.Accent;

            var info = await _obd.EcuInfoAsync();
            foreach (var x in info)
                _ecu.Add(Row(x.Key, x.Value));

            var voltageText = info.TryGetValue("Напряжение", out var v) ? v : "";
            var voltage = ParseVoltage(voltageText);
            var module = _title.Text ?? "";

            var compatibility = module switch
            {
                var x when x.Contains("Battery", StringComparison.OrdinalIgnoreCase) || x.Contains("АКБ", StringComparison.OrdinalIgnoreCase)
                    => "Профиль новой АКБ можно подготовить здесь. Фактическая регистрация зависит от марки и требует поддержанного диагностического протокола.",
                var x when x.Contains("Service", StringComparison.OrdinalIgnoreCase) || x.Contains("Сервис", StringComparison.OrdinalIgnoreCase)
                    => "Стандартные OBD-данные доступны. Сервисные reset/EPB/DPF обычно требуют марочного протокола.",
                var x when x.Contains("Keys", StringComparison.OrdinalIgnoreCase) || x.Contains("Ключ", StringComparison.OrdinalIgnoreCase)
                    => "Обычный ELM327 не является интерфейсом программирования ключей/иммобилайзера.",
                var x when x.Contains("Tuning", StringComparison.OrdinalIgnoreCase) || x.Contains("Stage", StringComparison.OrdinalIgnoreCase)
                    => "ECU identification доступна; чтение/запись калибровки требует совместимого flashing-интерфейса и конкретного протокола ECU.",
                var x when x.Contains("Coding", StringComparison.OrdinalIgnoreCase) || x.Contains("Кодирование", StringComparison.OrdinalIgnoreCase)
                    => "Базовая OBD identification доступна; марочные coding/adaptation требуют соответствующего протокола и адресации ECU.",
                _ => "ECU identification доступна; программирование возможно только после добавления поддержанного протокола для конкретного блока."
            };

            _ecu.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    Theme.Eyebrow("CAPABILITY CHECK"),
                    Theme.Body(compatibility)
                }
            }));

            if (voltage is not null && voltage < 12.2)
            {
                _status.Text = $"Питание {voltage:0.0} В — для программирования недостаточно стабильно. Используйте внешний источник питания.";
                _status.TextColor = Theme.Red;
            }
            else
            {
                _status.Text = "Проверка завершена. Запись будет доступна только для явно поддержанных интерфейсов/протоколов.";
                _status.TextColor = Theme.Green;
            }
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
        grid.Add(new Label { Text = title, TextColor = Theme.Muted, FontSize = 11 }, 0, 0);
        grid.Add(new Label { Text = value, TextColor = Theme.Text, FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End }, 1, 0);
        return Theme.CardView(grid, new Thickness(12));
    }

    private static double? ParseVoltage(string text)
    {
        var cleaned = new string(text.Replace(',', '.').Where(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string Description(string name) => name switch
    {
        "Tuning / Stage" => "ECU/TCU identification и подготовка безопасной flashing-сессии.",
        "Кодирование / Адаптации" => "Проверка ECU и подготовка марочных coding/adaptation операций.",
        "Ключи и иммобилайзер" => "Проверка поддерживаемого оборудования перед сервисом ключей.",
        "Сервисные функции ECU" => "Reset / EPB / DPF и другие функции только для поддержанных блоков.",
        "АКБ / Battery Coding" => "Профиль новой батареи и проверка готовности к регистрации.",
        _ => "ECU software / calibration identification и проверка готовности."
    };

    private static Entry Field(string placeholder, Keyboard? keyboard = null) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48
    };
}