using AutoDiagPro.Mobile.Services;
using Microsoft.Maui.Storage;

namespace AutoDiagPro.Mobile.Pages;

public sealed class AiPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Editor _mediaSymptom = new() {
        Placeholder = "Например: стук на холодном запуске, пропадает после прогрева...",
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 65,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted
    };
    private readonly Label _mediaResult = Theme.MutedText("Снимите видео или загрузите запись звука до 11 МБ.");

    private readonly Editor _question = new()
    {
        Placeholder = "Например: что проверить при ошибке P0299?",
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 110
    };
    private readonly Label _answer = new()
    {
        Text = "Здесь появится ответ AutoDiag AI.",
        TextColor = Theme.Muted,
        FontSize = 13,
        LineBreakMode = LineBreakMode.WordWrap
    };
    private readonly Switch _web = new() { IsToggled = true };
    private readonly Button _ask = new() { Text = "Спросить AI" };
    private readonly Label _contextStatus = Theme.MutedText("Контекст диагностики ещё не загружен.");

    public AiPage()
    {
        Title = "AI";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        _question.BackgroundColor = Color.FromArgb("#0E1316");
        _question.TextColor = Theme.Text;
        _question.PlaceholderColor = Theme.Muted;

        _ask.BackgroundColor = Theme.Accent;
        _ask.TextColor = Color.FromArgb("#111315");
        _ask.CornerRadius = 12;
        _ask.HeightRequest = 48;
        _ask.FontAttributes = FontAttributes.Bold;
        _ask.Clicked += AskClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 24, 18, 40),
                Spacing = 14,
                Children =
                {
                    Theme.H1("AI помощник"),
                    Theme.MutedText("Помогает понять неисправность, план ремонта, нужные детали и где их искать."),
                    Theme.CardView(BuildProblemCard()),
                    Theme.CardView(BuildMediaCard()),
                    Theme.CardView(BuildAskCard()),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "ОТВЕТ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                            _answer
                        }
                    })
                }
            }
        };
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();

        var vehicle = _state.SelectedVehicle;
        var hasDiagnostic = !string.IsNullOrWhiteSpace(_state.LastDiagnosticSummary);

        _contextStatus.Text = hasDiagnostic
            ? $"Последняя диагностика подключена • {_state.LastDiagnosticAtUtc?.LocalDateTime:dd.MM HH:mm}"
            : vehicle is null
                ? "Выберите автомобиль или выполните диагностику."
                : $"Выбран автомобиль: {vehicle.DisplayName}";

        _contextStatus.TextColor = hasDiagnostic ? Theme.Green : Theme.Muted;

        if (!string.IsNullOrWhiteSpace(_state.PendingAiQuestion))
        {
            _question.Text = _state.PendingAiQuestion;
            _state.PendingAiQuestion = "";
        }
    }

    private View BuildProblemCard()
    {
        var explain = QuickButton("Что сломано и насколько срочно?");
        explain.Clicked += (_, _) =>
        {
            _question.Text =
                "Разбери последнюю диагностику. Объясни простым языком, что наиболее вероятно неисправно, " +
                "насколько проблема срочная и можно ли продолжать ездить.";
        };

        var repair = QuickButton("Как исправить по шагам");
        repair.Clicked += (_, _) =>
        {
            _question.Text =
                "Составь пошаговый план ремонта по последней диагностике: что проверить сначала, " +
                "какие измерения сделать, что ремонтировать только после подтверждения причины.";
        };

        var parts = QuickButton("Какая деталь нужна и где купить");
        parts.Clicked += (_, _) =>
        {
            _web.IsToggled = true;
            _question.Text =
                "Определи, какая деталь вероятнее всего нужна по последней диагностике. " +
                "Скажи как точно проверить совместимость по VIN, OEM-номеру или коду двигателя. " +
                "Найди актуальные варианты покупки для моего региона: оригинал и хорошие аналоги, " +
                "ориентировочную цену и где заказать. Не выдумывай каталожный номер.";
        };

        var mechanic = QuickButton("Что сказать мастеру");
        mechanic.Clicked += (_, _) =>
        {
            _question.Text =
                "Подготовь короткий текст для мастера: симптомы, DTC, что уже видно по диагностике, " +
                "что попросить проверить и какие работы не соглашаться менять вслепую.";
        };

        return new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                new Label { Text = "ПОМОЩЬ ПО ПРОБЛЕМЕ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                _contextStatus,
                explain, repair, parts, mechanic
            }
        };
    }

    private View BuildMediaCard()
    {
        var upload = QuickButton("📁 Загрузить видео / аудио / фото");
        var camera = QuickButton("🎥 Снять видео на iPhone");
        upload.Clicked += async (_, _) => await AnalyzeMediaAsync(capture: false);
        camera.Clicked += async (_, _) => await AnalyzeMediaAsync(capture: true);
        return new VerticalStackLayout {
            Spacing = 10,
            Children = {
                new Label { Text = "AI ВИДЕО И ЗВУК", FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text },
                Theme.MutedText("Для звуков двигателя, форсунок, турбины и подвески. " +
                    "До 11 МБ, только с вашего разрешения. Ответ — предварительная диагностика."),
                _mediaSymptom, upload, camera, _mediaResult
            }
        };
    }

    private async Task AnalyzeMediaAsync(bool capture)
    {
        try
        {
            if (!_api.IsLoggedIn) throw new InvalidOperationException(
                "Для анализа необходимо войти в AutoDiag Server.");
            FileResult? media;
            if (capture) {
                if (!MediaPicker.Default.IsCaptureSupported)
                    throw new InvalidOperationException("Камера на этом устройстве недоступна.");
                media = await MediaPicker.Default.CaptureVideoAsync();
            } else {
                media = await FilePicker.Default.PickAsync(
                    new PickOptions { PickerTitle = "Видео, аудио или фото неисправности" });
            }
            if (media is null) return;
            var vehicle = _state.SelectedVehicle;
            var context = vehicle is null ? "Автомобиль не выбран." :
                "Автомобиль: " + vehicle.DisplayName + "; VIN: " + vehicle.Vin +
                "; пробег: " + vehicle.MileageKm + " км.";
            if (!string.IsNullOrWhiteSpace(_state.LastDiagnosticSummary))
                context += "\nПоследняя диагностика: " +
                    _state.LastDiagnosticSummary[..Math.Min(8500, _state.LastDiagnosticSummary.Length)];
            _mediaResult.Text = "Анализирую " + media.FileName + "...";
            _mediaResult.TextColor = Theme.Muted;
            var result = await _api.AnalyzeMediaAsync(media, context, _mediaSymptom.Text ?? "");
            _mediaResult.Text = result.Answer +
                "\n\nЭто гипотезы. Подтвердите причину измерениями и осмотром.";
            _mediaResult.TextColor = Theme.Text;
        }
        catch (Exception ex) {
            _mediaResult.Text = ex.Message;
            _mediaResult.TextColor = Theme.Red;
        }
    }

    private View BuildAskCard()
    {
        var webRow = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                _web,
                new Label
                {
                    Text = "Использовать веб-поиск",
                    TextColor = Theme.Text,
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "ВОПРОС", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                _question,
                webRow,
                _ask
            }
        };
    }

    private async void AskClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_question.Text))
        {
            await DisplayAlert("AutoDiag AI", "Введите вопрос.", "OK");
            return;
        }

        _ask.IsEnabled = false;
        _ask.Text = "Думаю...";
        _answer.Text = "Запрос к AutoDiag AI...";
        _answer.TextColor = Theme.Muted;
        try
        {
            var vehicle = _state.SelectedVehicle;
            var region = Preferences.Default.Get("parts_region", "Германия / ЕС");

            var vehicleContext = vehicle is null
                ? "Автомобиль не выбран."
                : $"Автомобиль: {vehicle.DisplayName}; VIN: {vehicle.Vin}; пробег: {vehicle.MileageKm} км.";

            var diagnosticContext = string.IsNullOrWhiteSpace(_state.LastDiagnosticSummary)
                ? "Последняя диагностика отсутствует."
                : "Последняя диагностика:\n" + _state.LastDiagnosticSummary;

            var cloudContext = "";
            if (vehicle is not null)
            {
                try
                {
                    var cloud = await _api.GetVehicleContextAsync(vehicle.Id);
                    cloudContext = "\nИстория AutoDiag Cloud:\n" + cloud.ToString();
                }
                catch
                {
                    cloudContext = "\nИстория AutoDiag Cloud временно недоступна.";
                }
            }

            var context =
                vehicleContext + "\n" +
                diagnosticContext + cloudContext + "\n" +
                $"Регион поиска запчастей: {region}.";

            var guidedQuestion =
                "Ты помощник AutoDiag Pro для владельца автомобиля. Отвечай понятно, без лишней терминологии. " +
                "Не утверждай, что деталь неисправна только по одному DTC: сначала отделяй факт от вероятной причины. " +
                "Если проблема может быть опасной для движения, скажи это в начале. " +
                "Для ремонта дай порядок проверок от простого/дешёвого к сложному. " +
                "Если нужна деталь, не выдумывай OEM-номер: используй VIN/код двигателя/номер старой детали, " +
                "а если этих данных недостаточно — прямо попроси недостающие данные. " +
                "При веб-поиске по запчастям показывай актуальные варианты, магазин/площадку, цену и отмечай оригинал или аналог. " +
                "В конце дай короткий список 'Что делать сейчас'.\n\nВопрос клиента: " + _question.Text;

            var result = await _api.AskAiAsync(guidedQuestion, context, _web.IsToggled);
            _answer.Text = result.Answer;
            _answer.TextColor = Theme.Text;
        }
        catch (Exception ex)
        {
            _answer.Text = ex.Message;
            _answer.TextColor = Theme.Red;
        }
        finally
        {
            _ask.IsEnabled = true;
            _ask.Text = "Спросить AI";
        }
    }

    private static Button QuickButton(string text) =>
        new()
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#1B242A"),
            TextColor = Theme.Text,
            CornerRadius = 12,
            HeightRequest = 46,
            HorizontalOptions = LayoutOptions.Fill
        };
}
