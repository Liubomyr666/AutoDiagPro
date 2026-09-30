using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;
using AutoDiagPro.SharedDiagnostics;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace AutoDiagPro.Mobile.Pages;

public sealed class InjectorPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly Label _status = Theme.MutedText("Подключите OBD и запустите проверку.");
    private readonly VerticalStackLayout _values = new() { Spacing = 9 };
    private bool _busy;
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Entry _auditMake = Input("Марка");
    private readonly Entry _auditModel = Input("Модель / двигатель");
    private readonly Entry _auditYear = Input("Год", Keyboard.Numeric);
    private readonly Entry _auditVin = Input("VIN из карточки (не проверен ECU)");
    private readonly Picker _auditCylinders = new()
    {
        Title = "Количество цилиндров", BackgroundColor = Theme.Surface, TextColor = Theme.Text,
        ItemsSource = Enumerable.Range(3, 14).Select(x => x.ToString()).ToArray()
    };
    private readonly Editor _auditMarkings = MultiInput("1=КОД с корпуса форсунки\\n2=..."),
                            _auditOemCodes = MultiInput("1=КОД из OEM-отчёта\\n2=...");
    private readonly Label _auditSummary = Theme.MutedText("Данные ещё не сравнивались.");
    private readonly Label _auditResult = Theme.MutedText(
        "Фактического чтения прописанных кодов ЭБУ в AutoDiag сейчас нет. " +
        "Этот раздел сравнивает коды, введённые из внешнего OEM-отчёта.");
    private readonly Button _auditShare = Theme.PrimaryButton("Поделиться отчётом");
    private Guid? _auditVehicleId;
    private string _lastAuditText = "";

    public InjectorPage()
    {
        Title = "Форсунки";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        var scan = Theme.PrimaryButton("Проверить топливную систему");
        scan.Clicked += async (_, _) => await RefreshAsync();
        _auditCylinders.SelectedIndex = 1; // 4 cylinders initially, adjustable up to 16.
        _auditShare.IsEnabled = false;
        foreach (var entry in new[] { _auditMake, _auditModel, _auditYear, _auditVin })
            entry.TextChanged += (_, _) => InvalidateAudit();
        _auditMarkings.TextChanged += (_, _) => InvalidateAudit();
        _auditOemCodes.TextChanged += (_, _) => InvalidateAudit();
        _auditCylinders.SelectedIndexChanged += (_, _) => InvalidateAudit();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("INJECTORS / FUEL"),
                    Theme.H1("Форсунки"),
                    Theme.MutedText("AutoDiag показывает только реально прочитанные значения ECU. Коррекции по цилиндрам не подменяются выдуманными данными."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 7,
                        Children =
                        {
                            Theme.Eyebrow("ВАЖНО"),
                            Theme.Body("Через обычный ELM327 доступны только стандартные OBD-II PID. Марочные коррекции форсунок, rail-specific параметры и кодирование форсунок требуют совместимого диагностического интерфейса.")
                        }
                    }),
                    scan,
                    _status,
                    _values,
                    Theme.CardView(BuildAuditPanel())
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
        UseSelectedVehicle(onlyIfChanged: true);
        if (_obd.IsConnected) await RefreshAsync();
    }

    private static Entry Input(string placeholder, Keyboard? keyboard = null) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 45
    };

    private static Editor MultiInput(string placeholder) => new()
    {
        Placeholder = placeholder.Replace("\\n", "\n"),
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 145,
        FontFamily = "Menlo",
        FontSize = 13
    };

    private View BuildAuditPanel()
    {
        var useVehicle = new Button
        {
            Text = "Взять выбранный автомобиль",
            BackgroundColor = Theme.Surface,
            TextColor = Theme.Text,
            HeightRequest = 46,
            CornerRadius = 12
        };
        useVehicle.Clicked += (_, _) => UseSelectedVehicle(onlyIfChanged: false);
        var compare = Theme.PrimaryButton("Сверить коды по цилиндрам");
        compare.Clicked += async (_, _) =>
        {
            if (!await AccessPolicy.RequireStaffAsync(this)) return;
            CompareAudit();
        };
        _auditShare.Clicked += async (_, _) =>
        {
            if (!await AccessPolicy.RequireStaffAsync(this) ||
                string.IsNullOrWhiteSpace(_lastAuditText)) return;
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "AutoDiag Pro • отчёт по форсункам",
                Text = _lastAuditText
            });
        };
        _auditResult.LineBreakMode = LineBreakMode.WordWrap;
        return new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.Eyebrow("ВСЕ МАРКИ · ПРОВЕРКА КОДОВ ФОРСУНОК"),
                Theme.H2("Совпадают ли коды по цилиндрам?"),
                Theme.MutedText(InjectorCodeAudit.SourceNotice),
                Theme.MutedText(InjectorCodeAudit.MarkingNotice),
                _auditMake,
                _auditModel,
                _auditYear,
                _auditVin,
                useVehicle,
                Theme.Body("Количество цилиндров (3–16):"),
                _auditCylinders,
                Theme.Eyebrow("КАЛИБРОВОЧНЫЕ КОДЫ С КОРПУСОВ ФОРСУНОК"),
                Theme.MutedText("Вводи построчно: 1=ABC123, 2=DEF456 ...; ориентируйся на нумерацию цилиндров производителя."),
                _auditMarkings,
                Theme.Eyebrow("КОДЫ ИЗ ВНЕШНЕГО OEM-ДИАГНОСТИЧЕСКОГО ОТЧЁТА"),
                Theme.MutedText("Вводи отдельно 1=... 2=... из Xentry/ISTA/другого совместимого отчёта. AutoDiag их не читал."),
                _auditOemCodes,
                compare,
                _auditSummary,
                _auditResult,
                _auditShare,
                Theme.MutedText("Сравнение введённых кодов не доказывает фактическую привязку в ЭБУ. " +
                    "Для подтверждения требуется реальное чтение марочным протоколом и проверка ECU HW/SW.")
            }
        };
    }

    private void UseSelectedVehicle(bool onlyIfChanged)
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            if (!onlyIfChanged)
                _auditSummary.Text = "Автомобиль не выбран. Можно ввести марку, модель и VIN вручную.";
            return;
        }
        if (onlyIfChanged && _auditVehicleId == vehicle.Id) return;
        _auditVehicleId = vehicle.Id;
        _auditMake.Text = vehicle.Make ?? "";
        _auditModel.Text = vehicle.Model ?? "";
        _auditYear.Text = vehicle.Year?.ToString() ?? "";
        _auditVin.Text = vehicle.Vin ?? "";
        _auditSummary.Text = "Данные из карточки авто. VIN и коды форсунок этим действием не считываются.";
    }

    private void InvalidateAudit()
    {
        _lastAuditText = "";
        _auditShare.IsEnabled = false;
        _auditSummary.Text = "Данные изменились — выполните сверку повторно.";
    }

    private void CompareAudit()
    {
        if (_auditCylinders.SelectedItem is not string selected ||
            !int.TryParse(selected, out var count) || count is < 3 or > 16)
        {
            _auditSummary.Text = "Выбери число цилиндров от 3 до 16.";
            return;
        }
        var markings = InjectorCodeAudit.ParseCylinderLines(_auditMarkings.Text, count);
        var ecuReport = InjectorCodeAudit.ParseCylinderLines(_auditOemCodes.Text, count);
        var errors = markings.Errors.Select(x => "Код с форсунки: " + x)
            .Concat(ecuReport.Errors.Select(x => "OEM-отчёт: " + x)).ToArray();
        if (errors.Length > 0)
        {
            InvalidateAudit();
            _auditSummary.Text = "Исправь формат строк и повтори сверку.";
            _auditResult.Text = string.Join("\n", errors);
            return;
        }
        var items = Enumerable.Range(1, count)
            .Select(i => new InjectorCodeInput(i,
                markings.Values.TryGetValue(i, out var marking) ? marking : null,
                ecuReport.Values.TryGetValue(i, out var code) ? code : null));
        var report = InjectorCodeAudit.Compare(items);
        var year = int.TryParse(_auditYear.Text, out var parsed) &&
                   parsed is >= 1886 and <= 2100 ? parsed : (int?)null;
        _lastAuditText = InjectorCodeAudit.BuildTextReport(
            _auditMake.Text, _auditModel.Text, year, _auditVin.Text, report);
        _auditSummary.Text = report.Summary;
        _auditSummary.TextColor = report.Mismatching > 0 ? Theme.Red : Theme.Accent;
        _auditResult.Text = _lastAuditText;
        _auditShare.IsEnabled = true;
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _values.Clear();

        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Сначала откройте «Диагностика».";
            _status.TextColor = Theme.Accent;
            return;
        }

        _busy = true;
        _status.Text = "Читаю топливные параметры...";
        _status.TextColor = Theme.Accent;

        try
        {
            var data = await _obd.InjectorSnapshotAsync();
            foreach (var item in data)
            {
                _values.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        Theme.Eyebrow(item.Key),
                        new Label
                        {
                            Text = item.Value,
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Text,
                            LineBreakMode = LineBreakMode.WordWrap,
                            FontAutoScalingEnabled = false
                        }
                    }
                }, new Thickness(12)));
            }

            _status.Text = $"ONLINE • {_obd.TransportName} • {DateTime.Now:HH:mm:ss}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
        finally
        {
            _busy = false;
        }
    }
}
