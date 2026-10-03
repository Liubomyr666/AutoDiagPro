using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class DiagnosticsPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly VehicleVisualService _visual;

    private readonly Entry _host = new() { Text = Preferences.Default.Get("obd_host", "192.168.0.10"), Placeholder = "IP адаптера" };
    private readonly Entry _port = new() { Text = Preferences.Default.Get("obd_port", 35000).ToString(), Placeholder = "Порт", Keyboard = Keyboard.Numeric };
    private readonly Picker _transport = new() { Title = "Тип подключения" };
    private readonly Picker _bleDevices = new() { Title = "Bluetooth OBD-адаптер" };
    private readonly Button _bleScan = DarkButton("Найти Bluetooth OBD");
    private readonly Button _connect = AccentButton("Подключить адаптер");
    private readonly Grid _wifiFields = new();
    private List<BleObdDevice> _bleFound = new();
    private readonly Label _connection = Theme.MutedText("Адаптер не подключён");
    private readonly Label _vehicleName = new()
    {
        Text = "Автомобиль не определён",
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text,
        FontAutoScalingEnabled = false
    };
    private readonly Label _vehicle = Theme.MutedText("VIN • —");
    private readonly Image _vehiclePhoto = new() { Source = "hero_car.jpg", Aspect = Aspect.AspectFill };
    private readonly Label _vehicleColor = Theme.MutedText("Цвет • не определён");
    private readonly BoxView _vehicleColorSwatch = new() { WidthRequest = 14, HeightRequest = 14, Color = Theme.Line };
    private readonly Label _protocol = Theme.MutedText("Протокол • —");
    private readonly Label _voltage = Theme.MutedText("Напряжение • —");
    private readonly VerticalStackLayout _results = new() { Spacing = 8 };

    public DiagnosticsPage()
    {
        _visual = new VehicleVisualService(_api);
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
                Padding = new Thickness(16, 14, 16, 118),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("OBD СКАНЕР"),
                    Theme.H1("Диагностика"),
                    Theme.MutedText("Подключите адаптер, выберите автомобиль и запустите проверку."),
                    BuildDiagnosticHero(),
                    BuildConnectionCard(),
                    BuildVehicleCard(),
                    BuildActionsCard(),
                    Theme.CardView(_results)
                }
            }
        };

        ShowResult("Результаты появятся здесь.");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var flushed = await OfflineSyncService.FlushAsync(_api, _store);
            if (flushed > 0)
                ShowResult($"Синхронизировано offline scan: {flushed}.");
        }
        catch { }

        var selected = _state.SelectedVehicle;
        _vehicle.Text = selected is null
            ? "VIN • автомобиль не выбран"
            : "VIN • " + (string.IsNullOrWhiteSpace(selected.Vin) ? "—" : selected.Vin);
        await RefreshVehicleVisualAsync(selected);

        if (_obd.IsConnected)
        {
            _connection.Text = $"Подключено • {_obd.TransportName} • {_obd.Endpoint}";
            _connection.TextColor = Theme.Green;
        }
    }

    private View BuildDiagnosticHero()
    {
        var grid = new Grid { HeightRequest = 165 };
        grid.Add(new Image { Source = "adapter.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.70 });
        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 7,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("OBD СКАНЕР"),
                new Label { Text = "Подключите OBD и запустите диагностику", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, FontAutoScalingEnabled = false },
                new Label { Text = "VIN, ошибки и результат scan сохраняются в истории AutoDiag.", FontSize = 11, TextColor = Theme.TextSoft, FontAutoScalingEnabled = false }
            }
        });

        return new Border
        {
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = grid
        };
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

    private View BuildVehicleCard()
    {
        var photo = new Border
        {
            HeightRequest = 150,
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Content = _vehiclePhoto
        };

        var colorRow = new HorizontalStackLayout { Spacing = 7 };
        colorRow.Add(new Border
        {
            WidthRequest = 20,
            HeightRequest = 20,
            Padding = 3,
            BackgroundColor = Theme.Surface,
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = _vehicleColorSwatch
        });
        _vehicleColor.VerticalTextAlignment = TextAlignment.Center;
        colorRow.Add(_vehicleColor);

        var editColor = DarkButton("Указать цвет кузова");
        editColor.Clicked += async (_, _) => await EditConnectedVehicleColorAsync();

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "ПОДКЛЮЧЕННЫЙ АВТОМОБИЛЬ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                photo,
                _vehicleName,
                _vehicle,
                colorRow,
                _protocol,
                _voltage,
                editColor
            }
        });
    }
    private async Task EditConnectedVehicleColorAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("AutoDiag Pro", "Сначала подключите и определите автомобиль.", "OK");
            return;
        }

        var current = await _visual.ResolveAsync(vehicle);
        var color = await DisplayPromptAsync(
            "Цвет кузова",
            "Введите подтверждённый цвет автомобиля.",
            initialValue: current.ColorName,
            maxLength: 80);
        if (color is null) return;

        var paint = await DisplayPromptAsync(
            "Код краски",
            "Введите код краски, если он известен. Можно оставить пустым.",
            initialValue: current.PaintCode,
            maxLength: 40);
        if (paint is null) return;

        _visual.SaveColor(vehicle, color, paint);
        await RefreshVehicleVisualAsync(vehicle);
    }

    private View BuildActionsCard()
    {
        var identify = DarkButton("Определить автомобиль по VIN");
        identify.Clicked += IdentifyClicked;

        var full = AccentButton("Полная диагностика");
        full.Clicked += FullScanClicked;

        var dtc = DarkButton("Проверить ошибки");
        dtc.Clicked += DtcClicked;

        var live = DarkButton("Параметры в реальном времени");
        live.Clicked += async (_, _) => await Shell.Current.GoToAsync("live");

        var ecu = DarkButton("Данные блоков управления");
        ecu.Clicked += async (_, _) => await Shell.Current.GoToAsync("ecu");

        var injectors = DarkButton("Форсунки / топливо");
        injectors.Clicked += async (_, _) => await Shell.Current.GoToAsync("injectors");

        var diesel = DarkButton("Дизель / Fuel Live");
        diesel.Clicked += async (_, _) => await Shell.Current.GoToAsync("diesel");

        var repair = DarkButton("Repair Brain");
        repair.Clicked += async (_, _) => await Shell.Current.GoToAsync("repair");

        var clear = DarkButton("Очистить DTC");
        clear.TextColor = Theme.Red;
        clear.Clicked += ClearDtcClicked;

        var disconnect = DarkButton("Отключить");
        disconnect.Clicked += async (_, _) =>
        {
            await _obd.DisconnectAsync();
            _connection.Text = "Адаптер отключён";
            _connection.TextColor = Theme.Muted;
        };

        var actions = new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.H2("Диагностика автомобиля"),
                identify, full, dtc, live, ecu
            }
        };

        if (AccessPolicy.IsStaff)
        {
            actions.Children.Add(injectors);
            actions.Children.Add(diesel);
            actions.Children.Add(repair);
            actions.Children.Add(clear);
        }

        actions.Children.Add(disconnect);
        return Theme.CardView(actions);
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

                var host = (_host.Text ?? "").Trim();
                await _obd.ConnectWifiAsync(host, port);
                Preferences.Default.Set("obd_host", host);
                Preferences.Default.Set("obd_port", port);
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

            try
            {
                await IdentifyVehicleAsync(showResult: false);
            }
            catch
            {
                // Connection remains valid even when this ECU does not expose VIN automatically.
            }
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
            await IdentifyVehicleAsync(showResult: true);
        }
        catch (Exception ex)
        {
            await DisplayAlert("VIN", ex.Message, "OK");
        }
    }

    private async Task<ServerVehicleRecord?> IdentifyVehicleAsync(bool showResult)
    {
        var vin = await _obd.VinAsync();
        var identity = VehicleIdentityService.Decode(vin);

        if (string.IsNullOrWhiteSpace(vin))
        {
            _vehicleName.Text = "Автомобиль не определён";
            _vehicle.Text = "VIN • не прочитан";
            await RefreshVehicleVisualAsync(null);
            return null;
        }

        _state.LastVin = identity.Vin;
        var decoded = await TryDecodeVinAsync(identity.Vin);
        var matched = await EnsureVehicleForVinAsync(identity, decoded);

        var make = Value(decoded?.Make, identity.Make);
        var model = Value(decoded?.Model);
        var year = decoded?.ParsedYear?.ToString() ?? identity.ModelYear?.ToString() ?? "—";

        _vehicleName.Text = string.Join(" ", new[] { make, model }
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != "—")).Trim();
        if (string.IsNullOrWhiteSpace(_vehicleName.Text))
            _vehicleName.Text = "Автомобиль определён";

        _vehicle.Text = $"VIN • {identity.Vin} • {year}";
        await RefreshVehicleVisualAsync(matched ?? _state.SelectedVehicle);

        if (showResult)
        {
            ShowResult($"Автомобиль определён\nVIN: {identity.Vin}\nМарка: {make}\nМодель: {model}\nМодельный год: {year}\nДвигатель: {Value(decoded?.Engine)}{Volume(decoded?.DisplacementL)}\nТопливо: {Value(decoded?.FuelType)}\nКоробка: {Value(decoded?.Transmission)}\nПривод: {Value(decoded?.DriveType)}\nКузов: {Value(decoded?.BodyClass)}\nРегион: {identity.Country}\nWMI: {identity.Wmi}\nИсточник: {Value(decoded?.Source, "VIN / ECU")}\nAutoDiag: {(matched is null ? "VIN определён, но автомобиль не синхронизирован" : "автомобиль выбран и синхронизирован")}");
        }

        return matched;
    }

    private async Task RefreshVehicleVisualAsync(ServerVehicleRecord? vehicle)
    {
        if (vehicle is null)
        {
            _vehiclePhoto.Source = "hero_car.jpg";
            _vehicleColor.Text = "Цвет • не определён";
            _vehicleColorSwatch.Color = Theme.Line;
            return;
        }

        _vehicleName.Text = vehicle.DisplayName;
        var visual = await _visual.ResolveAsync(vehicle);

        _vehicleColor.Text = string.IsNullOrWhiteSpace(visual.ColorName)
            ? "Цвет • не определён"
            : "Цвет • " + visual.ColorName +
              (string.IsNullOrWhiteSpace(visual.PaintCode) ? "" : " • код " + visual.PaintCode);

        _vehicleColorSwatch.Color = VehicleVisualService.Swatch(visual.ColorName);
        _vehiclePhoto.Source = string.IsNullOrWhiteSpace(visual.PhotoUrl)
            ? "hero_car.jpg"
            : new UriImageSource
            {
                Uri = new Uri(visual.PhotoUrl),
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromDays(30)
            };
    }

    private async void DtcClicked(object? sender, EventArgs e)
    {
        if (!RequireConnection()) return;
        try
        {
            var confirmed = await _obd.DtcAsync();
            var pending = await _obd.PendingDtcAsync();
            var permanent = await _obd.PermanentDtcAsync();
            var allCodes = confirmed
                .Concat(pending)
                .Concat(permanent)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dtcText =
                $"CONFIRMED: {(confirmed.Count == 0 ? "нет" : string.Join(", ", confirmed))}\n" +
                $"PENDING: {(pending.Count == 0 ? "нет" : string.Join(", ", pending))}\n" +
                $"PERMANENT: {(permanent.Count == 0 ? "нет" : string.Join(", ", permanent))}";

            _state.LastDtcCodes = allCodes;
            _state.LastDiagnosticSummary = "OBD-II DTC LAYERS\n" + dtcText;
            _state.LastDiagnosticAtUtc = DateTimeOffset.UtcNow;

            if (allCodes.Count == 0)
                ShowResult("DTC: ошибок нет.\n\n" + dtcText);
            else
                await RenderDtcCardsAsync("DTC найдены:\n\n" + dtcText, allCodes, runAiImmediately: true);
        }
        catch (Exception ex)
        {
            ShowResult("DTC: " + ex.Message, true);
        }
    }

    private async void ClearDtcClicked(object? sender, EventArgs e)
    {
        if (!RequireConnection()) return;

        var yes = await DisplayAlert(
            "Очистить DTC",
            "Очистка удалит сохранённые OBD-II ошибки и может сбросить readiness. Делайте это после ремонта/диагностики. Продолжить?",
            "Очистить",
            "Отмена");

        if (!yes) return;

        try
        {
            await _obd.ClearDtcAsync();
            await Task.Delay(900);
            var confirmed = await _obd.DtcAsync();
            var pending = await _obd.PendingDtcAsync();
            var permanent = await _obd.PermanentDtcAsync();
            var remaining = confirmed
                .Concat(pending)
                .Concat(permanent)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _state.LastDtcCodes = remaining;
            _state.LastDiagnosticAtUtc = DateTimeOffset.UtcNow;
            ShowResult(remaining.Count == 0
                ? "DTC очищены. После поездки проверьте readiness и выполните контрольный scan."
                : "После очистки остались DTC. Permanent-коды могут исчезнуть только после подтверждённых успешных циклов ECU:\n" +
                  string.Join(", ", remaining),
                remaining.Count > 0);
        }
        catch (Exception ex)
        {
            ShowResult("Очистка DTC: " + ex.Message, true);
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
            var confirmedDtc = await _obd.DtcAsync();
            var pendingDtc = await _obd.PendingDtcAsync();
            var permanentDtc = await _obd.PermanentDtcAsync();
            var allDtc = confirmedDtc
                .Concat(pendingDtc)
                .Concat(permanentDtc)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var readiness = await _obd.ReadinessAsync();
            var ecu = await _obd.EcuInfoAsync();
            var live = await _obd.LiveSnapshotAsync();
            var freezeFrame = await _obd.FreezeFrameAsync();
            var mode06 = await _obd.Mode06MonitorResultsAsync();
            var iupr = await _obd.InUsePerformanceTrackingAsync();

            var identity = VehicleIdentityService.Decode(vin);
            var decoded = await TryDecodeVinAsync(identity.Vin);
            var obdEngineType = readiness.TryGetValue("Тип двигателя", out var detectedEngineType)
                ? detectedEngineType
                : "";
            var summary =
                $"VIN: {identity.Vin}\nМарка: {Value(decoded?.Make, identity.Make)}\nМодель: {Value(decoded?.Model)}\nМодельный год: {decoded?.ParsedYear?.ToString() ?? identity.ModelYear?.ToString() ?? "—"}\nДвигатель: {Value(decoded?.Engine)}{Volume(decoded?.DisplacementL)}\nТопливо: {Value(decoded?.FuelType)}\nКоробка: {Value(decoded?.Transmission)}\nПривод: {Value(decoded?.DriveType)}\nКузов: {Value(decoded?.BodyClass)}\nРегион: {identity.Country}\nПротокол: {protocol}\nНапряжение: {voltage}\n" +
                ($"CONFIRMED DTC: {(confirmedDtc.Count == 0 ? "нет" : string.Join(", ", confirmedDtc))}\n" +
                 $"PENDING DTC: {(pendingDtc.Count == 0 ? "нет" : string.Join(", ", pendingDtc))}\n" +
                 $"PERMANENT DTC: {(permanentDtc.Count == 0 ? "нет" : string.Join(", ", permanentDtc))}") +
                "\n\nECU / CALIBRATION\n" + string.Join("\n", ecu.Select(x => $"{x.Key}: {x.Value}")) +
                "\n\nREADINESS\n" + string.Join("\n", readiness.Select(x => $"{x.Key}: {x.Value}")) +
                "\n\nLIVE DATA\n" + string.Join("\n", live.Select(x => $"{x.Key}: {x.Value}")) +
                "\n\nFREEZE FRAME\n" +
                    (freezeFrame.Count == 0
                        ? "Не поддерживается / ECU не сохранил freeze frame"
                        : string.Join("\n", freezeFrame.Select(x => $"{x.Key}: {x.Value}"))) +
                "\n\nMODE 06 MONITOR RESULTS (RAW)\n" +
                    (mode06.Count == 0
                        ? "Не поддерживается / monitor results не возвращены"
                        : string.Join("\n", mode06.Take(32))) +
                $"\n\nIUPR / MODE 09 PID 08\n{iupr}";

            var health = DiagnosticHealthService.Analyze(voltage, allDtc, readiness, live);
            summary += "\n\nHEALTH SCORE\n" + health.Summary +
                       "\n• " + string.Join("\n• ", health.Findings);

            _state.LastDiagnosticSummary = summary;
            _state.LastDtcCodes = allDtc;
            _state.LastVin = vin;
            _state.LastDiagnosticAtUtc = DateTimeOffset.UtcNow;

            if (allDtc.Count == 0)
                ShowResult(summary);
            else
                await RenderDtcCardsAsync(summary, allDtc, runAiImmediately: true);

            var vehicle = await EnsureVehicleForVinAsync(identity, decoded);
            if (vehicle is not null)
            {
                var uploaded = await OfflineSyncService.UploadOrQueueScanAsync(
                    _api,
                    _store,
                    vehicle.Id,
                    string.IsNullOrWhiteSpace(vin) ? vehicle.Vin : vin,
                    _obd.TransportName,
                    protocol,
                    allDtc.Count,
                    summary);

                var syncText = uploaded
                    ? "✓ Scan сохранён на AutoDiag Server."
                    : "OFFLINE • Scan сохранён на iPhone и будет отправлен автоматически.";

                if (allDtc.Count == 0)
                    ShowResult(summary + "\n\n" + syncText);
                else
                    _results.Add(Theme.MutedText(syncText));
            }

            _vehicle.Text = string.IsNullOrWhiteSpace(vin)
                ? "VIN • —"
                : $"VIN • {vin} • {Value(decoded?.Make, identity.Make)} {Value(decoded?.Model, "")}".Trim();
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
            var suggestion = DtcRepairAdvisor.Analyze(code);
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
                    new Label
                    {
                        Text = suggestion.Summary,
                        TextColor = Theme.Text,
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold
                    },
                    Theme.MutedText("Что проверить:\n• " + string.Join("\n• ", suggestion.Checks)),
                    Theme.MutedText("Возможные детали после подтверждения:\n• " + string.Join("\n• ", suggestion.PossibleParts)),
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

    private async Task<ServerVinDecodeRecord?> TryDecodeVinAsync(string vin)
    {
        var normalized = VehicleIdentityService.Normalize(vin);
        if (normalized.Length != 17 || !_api.IsLoggedIn) return null;
        try { return await _api.DecodeVinAsync(normalized); }
        catch { return null; }
    }

    private async Task<ServerVehicleRecord?> EnsureVehicleForVinAsync(
        VehicleIdentityResult identity,
        ServerVinDecodeRecord? decoded = null)
    {
        if (!identity.IsValid) return _state.SelectedVehicle;

        var existing = _state.Vehicles.FirstOrDefault(x =>
            string.Equals(VehicleIdentityService.Normalize(x.Vin), identity.Vin, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.Make) && !string.IsNullOrWhiteSpace(decoded?.Make)) existing.Make = decoded!.Make;
            if (string.IsNullOrWhiteSpace(existing.Model) && !string.IsNullOrWhiteSpace(decoded?.Model)) existing.Model = decoded!.Model;
            if (existing.Year is null) existing.Year = decoded?.ParsedYear ?? identity.ModelYear;
            _state.SelectedVehicle = existing;
            return existing;
        }

        try
        {
            var id = await _api.CreateVehicleAsync(new ServerVehicleCreate
            {
                Vin = identity.Vin,
                Make = !string.IsNullOrWhiteSpace(decoded?.Make)
                    ? decoded!.Make
                    : string.Equals(identity.Make, "Не определено", StringComparison.OrdinalIgnoreCase) ? null : identity.Make,
                Model = string.IsNullOrWhiteSpace(decoded?.Model) ? null : decoded!.Model,
                Year = decoded?.ParsedYear ?? identity.ModelYear
            });

            var vehicles = await _api.GetVehiclesAsync();
            _state.Vehicles = vehicles;
            var created = vehicles.FirstOrDefault(x => x.Id == id) ??
                          vehicles.FirstOrDefault(x =>
                              string.Equals(VehicleIdentityService.Normalize(x.Vin), identity.Vin, StringComparison.OrdinalIgnoreCase));
            if (created is not null) _state.SelectedVehicle = created;
            return created;
        }
        catch
        {
            return _state.SelectedVehicle;
        }
    }

    private static string Value(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Volume(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : $" • {value.Trim()} л";

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

        await Shell.Current.GoToAsync("ai");
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