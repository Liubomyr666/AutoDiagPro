using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class DiagnosticsPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Entry _host = new() { Text = Preferences.Default.Get("obd_host", "192.168.0.10"), Placeholder = "IP адаптера" };
    private readonly Entry _port = new() { Text = Preferences.Default.Get("obd_port", 35000).ToString(), Placeholder = "Порт", Keyboard = Keyboard.Numeric };
    private readonly Picker _transport = new() { Title = "Тип подключения" };
    private readonly Picker _bleDevices = new() { Title = "Bluetooth OBD-адаптер" };
    private readonly Button _bleScan = DarkButton("Найти Bluetooth OBD");
    private readonly Button _connect = AccentButton("Подключить адаптер");
    private readonly Grid _wifiFields = new();
    private List<BleObdDevice> _bleFound = new();
    private readonly Label _connection = Theme.MutedText("Адаптер не подключён");
    private readonly Label _vehicle = Theme.MutedText("VIN • —");
    private readonly Label _protocol = Theme.MutedText("Протокол • —");
    private readonly Label _voltage = Theme.MutedText("Напряжение • —");
    private readonly VerticalStackLayout _results = new() { Spacing = 8 };

    public DiagnosticsPage()
    {
        Title = "Диагностика";
        BackgroundColor = Theme.Page;
        StyleEntry(_host);
        StyleEntry(_port);
        _transport.ItemsSource = new[] { "Bluetooth LE", "Wi-Fi" };
        _transport.SelectedIndex = 0;
        _transport.TextColor = Theme.Text;
        _transport.BackgroundColor = Color.FromArgb("#0E1316");
        _bleDevices.TextColor = Theme.Text;
        _bleDevices.BackgroundColor = Color.FromArgb("#0E1316");
        _transport.SelectedIndexChanged += (_, _) => RefreshTransportUi();
        _bleScan.Clicked += BleScanClicked;
        _connect.Clicked += ConnectClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 24, 18, 40),
                Spacing = 14,
                Children =
                {
                    Theme.H1("Диагностика"),
                    Theme.MutedText("iPhone → OBD → автомобиль. Только безопасное чтение данных."),
                    BuildConnectionCard(),
                    BuildVehicleCard(),
                    BuildActionsCard(),
                    Theme.CardView(_results)
                }
            }
        };

        ShowResult("Результаты появятся здесь.");
    }
    private View BuildConnectionCard()
    {
        _wifiFields.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
        _wifiFields.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _wifiFields.ColumnSpacing = 8;
        _wifiFields.Add(_host, 0, 0);
        _wifiFields.Add(_port, 1, 0);

        var stack = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "ПОДКЛЮЧЕНИЕ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText("На iPhone рекомендуем Bluetooth LE. Wi-Fi OBD тоже поддерживается."),
                _transport,
                _bleDevices,
                _bleScan,
                _wifiFields,
                _connect,
                _connection
            }
        };

        RefreshTransportUi();
        return Theme.CardView(stack);
    }

    private void RefreshTransportUi()
    {
        var ble = _transport.SelectedIndex != 1;
        _bleDevices.IsVisible = ble;
        _bleScan.IsVisible = ble;
        _wifiFields.IsVisible = !ble;
        _connect.Text = ble ? "Подключить Bluetooth OBD" : "Подключить Wi-Fi OBD";
    }

    private async void BleScanClicked(object? sender, EventArgs e)
    {
        _bleScan.IsEnabled = false;
        _bleScan.Text = "Поиск...";
        _connection.Text = "Ищу Bluetooth LE адаптеры...";
        _connection.TextColor = Theme.Accent;

        try
        {
            _bleFound = (await _obd.ScanBleAsync()).ToList();
            _bleDevices.ItemsSource = _bleFound.Select(x => x.Display).ToList();
            if (_bleFound.Count > 0) _bleDevices.SelectedIndex = 0;

            _connection.Text = _bleFound.Count == 0
                ? "BLE OBD не найден. Включи зажигание и проверь питание адаптера."
                : $"Найдено Bluetooth устройств: {_bleFound.Count}";
            _connection.TextColor = _bleFound.Count == 0 ? Theme.Accent : Theme.Green;
        }
        catch (Exception ex)
        {
            _connection.Text = "Bluetooth: " + ex.Message;
            _connection.TextColor = Theme.Red;
        }
        finally
        {
            _bleScan.IsEnabled = true;
            _bleScan.Text = "Найти Bluetooth OBD";
        }
    }

    private View BuildVehicleCard() =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                new Label { Text = "АВТОМОБИЛЬ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                _vehicle, _protocol, _voltage
            }
        });
    private View BuildActionsCard()
    {
        var identify = DarkButton("Определить VIN");
        identify.Clicked += IdentifyClicked;

        var full = AccentButton("Полная диагностика");
        full.Clicked += FullScanClicked;

        var dtc = DarkButton("Ошибки DTC");
        dtc.Clicked += DtcClicked;

        var live = DarkButton("Live Data");
        live.Clicked += LiveClicked;

        var disconnect = DarkButton("Отключить");
        disconnect.Clicked += async (_, _) =>
        {
            await _obd.DisconnectAsync();
            _connection.Text = "Адаптер отключён";
            _connection.TextColor = Theme.Muted;
        };

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                new Label { Text = "БЫСТРЫЕ ДЕЙСТВИЯ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                identify, full, dtc, live, disconnect
            }
        });
    }
    private async void ConnectClicked(object? sender, EventArgs e)
    {
        try
        {
            _connect.IsEnabled = false;
            _connection.Text = "Подключение...";
            _connection.TextColor = Theme.Accent;

            if (_transport.SelectedIndex == 1)
            {
                if (!int.TryParse(_port.Text, out var port))
                    throw new InvalidOperationException("Некорректный Wi-Fi порт.");
                await _obd.ConnectWifiAsync(_host.Text ?? "", port);
            }
            else
            {
                if (_bleDevices.SelectedIndex < 0 || _bleDevices.SelectedIndex >= _bleFound.Count)
                    throw new InvalidOperationException("Сначала нажми «Найти Bluetooth OBD» и выбери адаптер.");
                await _obd.ConnectBleAsync(_bleFound[_bleDevices.SelectedIndex]);
            }

            _connection.Text = $"Подключено • {_obd.TransportName} • {_obd.Endpoint}";
            _connection.TextColor = Theme.Green;
            _protocol.Text = "Протокол • " + await _obd.ProtocolAsync();
            _voltage.Text = "Напряжение • " + await _obd.VoltageAsync();
        }
        catch (Exception ex)
        {
            _connection.Text = "Ошибка подключения";
            _connection.TextColor = Theme.Red;
            await DisplayAlert("OBD", ex.Message, "OK");
        }
        finally
        {
            _connect.IsEnabled = true;
        }
    }
    private async void IdentifyClicked(object? sender, EventArgs e)
    {
        if (!RequireConnection()) return;
        try
        {
            var vin = await _obd.VinAsync();
            _vehicle.Text = string.IsNullOrWhiteSpace(vin) ? "VIN • не прочитан" : $"VIN • {vin}";

            if (!string.IsNullOrWhiteSpace(vin))
            {
                var matched = _state.Vehicles.FirstOrDefault(x =>
                    string.Equals(x.Vin, vin, StringComparison.OrdinalIgnoreCase));
                if (matched is not null) _state.SelectedVehicle = matched;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("VIN", ex.Message, "OK");
        }
    }

    private async void DtcClicked(object? sender, EventArgs e)
    {
        if (!RequireConnection()) return;
        try
        {
            var codes = await _obd.DtcAsync();
            _state.LastDtcCodes = codes;
            _state.LastDiagnosticSummary = codes.Count == 0
                ? "DTC: ошибок нет."
                : "DTC: " + string.Join(", ", codes);
            _state.LastDiagnosticAtUtc = DateTimeOffset.UtcNow;

            var dtcText = codes.Count == 0 ? "DTC: ошибок нет." : "DTC найдены:";
            if (codes.Count == 0)
                ShowResult(dtcText);
            else
                await RenderDtcCardsAsync(dtcText, codes, runAiImmediately: true);
        }
        catch (Exception ex)
        {
            ShowResult("DTC: " + ex.Message, true);
        }
    }
    private async void LiveClicked(object? sender, EventArgs e)
    {
        if (!RequireConnection()) return;
        try
        {
            var values = await _obd.LiveSnapshotAsync();
            ShowResult("Live Data\n" + string.Join("\n", values.Select(x => $"{x.Key}: {x.Value}")));
        }
        catch (Exception ex)
        {
            ShowResult("Live Data: " + ex.Message, true);
        }
    }

    private async void FullScanClicked(object? sender, EventArgs e)
    {
        if (!RequireConnection()) return;

        try
        {
            ShowResult("Идёт диагностика...");
            var vin = await _obd.VinAsync();
            var protocol = await _obd.ProtocolAsync();
            var voltage = await _obd.VoltageAsync();
            var dtc = await _obd.DtcAsync();
            var live = await _obd.LiveSnapshotAsync();

            var summary = $"VIN: {vin}\nПротокол: {protocol}\nНапряжение: {voltage}\n" +
                          (dtc.Count == 0 ? "DTC: ошибок нет" : $"DTC: {string.Join(", ", dtc)}") +
                          "\n" + string.Join("\n", live.Select(x => $"{x.Key}: {x.Value}"));

            _state.LastDiagnosticSummary = summary;
            _state.LastDtcCodes = dtc;
            _state.LastVin = vin;
            _state.LastDiagnosticAtUtc = DateTimeOffset.UtcNow;

            if (dtc.Count == 0)
                ShowResult(summary);
            else
                await RenderDtcCardsAsync(summary, dtc, runAiImmediately: true);

            var vehicle = _state.SelectedVehicle;
            if (vehicle is not null)
            {
                await _api.UploadScanAsync(
                    vehicle.Id,
                    string.IsNullOrWhiteSpace(vin) ? vehicle.Vin : vin,
                    _obd.TransportName,
                    protocol,
                    dtc.Count,
                    summary);

                if (dtc.Count == 0)
                    ShowResult(summary + "\n\n✓ Scan сохранён на AutoDiag Server.");
                else
                    _results.Add(Theme.MutedText("✓ Scan сохранён на AutoDiag Server."));
            }

            _vehicle.Text = $"VIN • {(string.IsNullOrWhiteSpace(vin) ? "—" : vin)}";
            _protocol.Text = "Протокол • " + protocol;
            _voltage.Text = "Напряжение • " + voltage;
        }
        catch (Exception ex)
        {
            ShowResult("Диагностика: " + ex.Message, true);
        }
    }

    private async Task RenderDtcCardsAsync(string header, IReadOnlyList<string> codes, bool runAiImmediately)
    {
        _results.Clear();
        _results.Add(new Label
        {
            Text = header,
            TextColor = Theme.Text,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        });

        foreach (var code in codes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var output = Theme.MutedText("AI-анализ ещё не запущен.");
            output.LineBreakMode = LineBreakMode.WordWrap;

            var aiButton = AccentButton("AI: как исправить");
            var partsButton = DarkButton("Найти деталь");

            async Task RunAsync(bool partsOnly)
            {
                aiButton.IsEnabled = false;
                partsButton.IsEnabled = false;
                output.TextColor = Theme.Muted;
                output.Text = partsOnly
                    ? "Ищу подходящие детали и варианты заказа..."
                    : "Анализирую ошибку...";

                try
                {
                    var vehicle = _state.SelectedVehicle;
                    var region = Preferences.Default.Get("parts_region", "Германия / ЕС");
                    var context =
                        (vehicle is null
                            ? "Автомобиль не выбран."
                            : $"Автомобиль: {vehicle.DisplayName}; VIN: {vehicle.Vin}; пробег: {vehicle.MileageKm} км.") +
                        "\nПоследняя диагностика:\n" + _state.LastDiagnosticSummary +
                        $"\nРегион поиска запчастей: {region}.\nТекущий DTC: {code}.";

                    var question = partsOnly
                        ? $"Для DTC {code} определи, какие детали могут понадобиться только после подтверждения причины. " +
                          "Объясни как точно подобрать их по VIN/OEM/коду двигателя, покажи оригинал и хорошие аналоги, " +
                          "ориентировочные цены и актуальные варианты заказа в моём регионе. Не выдумывай номер или совместимость."
                        : $"Разбери DTC {code}. Объясни простым языком, что он означает, вероятные причины, срочность и можно ли ехать. " +
                          "Дай проверки по шагам от дешёвого к дорогому, затем возможные детали только после подтверждения, " +
                          "как подобрать их точно и где заказать в моём регионе.";

                    output.Text = (await _api.AskAiAsync(question, context, true)).Answer;
                    output.TextColor = Theme.Text;
                }
                catch (Exception ex)
                {
                    output.Text = "AI сейчас недоступен: " + ex.Message;
                    output.TextColor = Theme.Red;
                }
                finally
                {
                    aiButton.IsEnabled = true;
                    partsButton.IsEnabled = true;
                }
            }

            aiButton.Clicked += async (_, _) => await RunAsync(false);
            partsButton.Clicked += async (_, _) => await RunAsync(true);

            var cardContent = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = code,
                        TextColor = Theme.Accent,
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold
                    },
                    Theme.MutedText("Отдельная карточка ошибки • AutoDiag AI"),
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { aiButton, partsButton }
                    },
                    output
                }
            };

            _results.Add(Theme.CardView(cardContent, new Thickness(14)));

            if (runAiImmediately)
                await RunAsync(false);
        }
    }

    private async Task<string> GetAiRepairAdviceAsync(IReadOnlyList<string> codes)
    {
        var vehicle = _state.SelectedVehicle;
        var region = Preferences.Default.Get("parts_region", "Германия / ЕС");
        var context =
            (vehicle is null
                ? "Автомобиль не выбран."
                : $"Автомобиль: {vehicle.DisplayName}; VIN: {vehicle.Vin}; пробег: {vehicle.MileageKm} км.") +
            "\nПоследняя диагностика:\n" + _state.LastDiagnosticSummary +
            $"\nРегион поиска запчастей: {region}.";

        var question =
            "Проанализируй DTC: " + string.Join(", ", codes) + ". " +
            "Для каждого кода объясни простым языком вероятные причины, насколько срочно и можно ли продолжать ехать. " +
            "Дай порядок проверок от простого и дешёвого к сложному. Не предлагай менять деталь без подтверждения причины. " +
            "Если деталь может понадобиться, объясни как точно подобрать её по VIN/OEM/коду двигателя, " +
            "покажи оригинал и хорошие аналоги, примерный диапазон цены и актуальные варианты где заказать в указанном регионе. " +
            "Не выдумывай каталожные номера или совместимость. В конце дай короткий список «Что делать сейчас».";

        return (await _api.AskAiAsync(question, context, true)).Answer;
    }

    private bool RequireConnection()
    {
        if (_obd.IsConnected) return true;
        DisplayAlert("AutoDiag", "Сначала подключите OBD-адаптер.", "OK");
        return false;
    }
    private void ShowResult(string text, bool error = false, bool showAiButton = false)
    {
        _results.Clear();
        _results.Add(new Label
        {
            Text = text,
            TextColor = error ? Theme.Red : Theme.Text,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        });

        if (showAiButton && !error)
        {
            var ai = AccentButton("AI: что делать и что купить?");
            ai.Margin = new Thickness(0, 8, 0, 0);
            ai.Clicked += OpenAiForCurrentProblem;
            _results.Add(ai);

            _results.Add(Theme.MutedText(
                "AI разберёт ошибки простым языком, скажет можно ли ехать, что проверить, какую деталь искать и где её купить."));
        }
    }

    private async void OpenAiForCurrentProblem(object? sender, EventArgs e)
    {
        _state.PendingAiQuestion =
            "Разбери последнюю диагностику моего автомобиля. " +
            "Объясни простым языком, что сломано или наиболее вероятно неисправно, насколько это срочно, " +
            "можно ли продолжать ездить, что проверить по шагам, что ремонтировать первым. " +
            "Если нужна замена детали — укажи как точно определить нужную деталь по VIN/номеру, " +
            "какие OEM и нормальные аналоги искать, примерный диапазон цены и где можно купить. " +
            "Не выдумывай каталожный номер: если данных недостаточно, прямо напиши какие данные нужны.";

        await Shell.Current.GoToAsync("//ai");
    }

    private static Button AccentButton(string text) =>
        new()
        {
            Text = text,
            BackgroundColor = Theme.Accent,
            TextColor = Color.FromArgb("#111315"),
            CornerRadius = 12,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };

    private static Button DarkButton(string text) =>
        new()
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#1B242A"),
            TextColor = Theme.Text,
            CornerRadius = 12,
            HeightRequest = 46
        };

    private static void StyleEntry(Entry entry)
    {
        entry.BackgroundColor = Color.FromArgb("#0E1316");
        entry.TextColor = Theme.Text;
        entry.PlaceholderColor = Theme.Muted;
        entry.HeightRequest = 48;
    }
}