using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class PartsPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();

    private readonly Entry _delivery = new() {
        Placeholder = "Страна и индекс доставки (например, DE 31785)",
        BackgroundColor = Theme.Surface, TextColor = Theme.Text, PlaceholderColor = Theme.Muted
    };
    private readonly Editor _sellerReply = new() {
        Placeholder = "Сюда можно вставить предложение и ответ продавца: цена, доставка, гарантия...",
        AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80,
        BackgroundColor = Theme.Surface, TextColor = Theme.Text, PlaceholderColor = Theme.Muted
    };
    private readonly Switch _agentWeb = new();

    private readonly Editor _query = new()
    {
        Placeholder = "Например: передние тормозные колодки, датчик MAF, турбина...",
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 90,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted
    };
    private readonly Label _result = Theme.MutedText("Укажите деталь или проблему.");
    private readonly Label _vehicle = Theme.MutedText("Автомобиль не выбран.");
    private string _lastResult = "";

    public PartsPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Детали по VIN";

        var find = Theme.PrimaryButton("Найти деталь и варианты заказа");
        find.Clicked += async (_, _) => await FindAsync();

        var fromDtc = Theme.SecondaryButton("Подобрать по последним DTC");
        fromDtc.Clicked += (_, _) =>
        {
            _query.Text = _state.LastDtcCodes.Count == 0
                ? "Подбери вероятные детали по последней диагностике, но только после подтверждения причины."
                : "Проверь, какие детали могут понадобиться по DTC: " + string.Join(", ", _state.LastDtcCodes) + ". Не предлагай замену без подтверждения причины.";
        };

        var save = Theme.SecondaryButton("Сохранить в избранное");
        save.Clicked += async (_, _) => await SaveBookmarkAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("PARTS FINDER"),
                    Theme.H1("Детали по VIN"),
                    _vehicle,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            Theme.Body("AutoDiag AI ищет оригинал и нормальные аналоги, но не выдумывает OEM-номер. Совместимость должна подтверждаться VIN, кодом двигателя или номером старой детали."),
                            _query, fromDtc, find, save
                        }
                    }),
                    Theme.CardView(BuildAgentCard()),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children = { Theme.Eyebrow("РЕЗУЛЬТАТ"), _result }
                    })
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var v = _state.SelectedVehicle;
        _vehicle.Text = v is null ? "Сначала выберите автомобиль." : $"{v.DisplayName} • VIN {v.Vin ?? "—"}";
        _vehicle.TextColor = v is null ? Theme.Accent : Theme.TextSoft;
    }

    private View BuildAgentCard()
    {
        var newPart = Theme.PrimaryButton("🔎 Найти новую деталь");
        var used = Theme.SecondaryButton("♻️ Найти б/у деталь");
        var enquiry = Theme.SecondaryButton("✉️ Написать запрос продавцу");
        var compare = Theme.SecondaryButton("📦 Проверить ответ и полную стоимость");
        var share = Theme.SecondaryButton("Поделиться результатом / письмом");
        newPart.Clicked += async (_, _) => await AgentAsync("new");
        used.Clicked += async (_, _) => await AgentAsync("used");
        enquiry.Clicked += async (_, _) => await AgentAsync("enquiry");
        compare.Clicked += async (_, _) => await AgentAsync("compare");
        share.Clicked += async (_, _) => {
            if (!string.IsNullOrWhiteSpace(_lastResult))
                await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(
                    new Microsoft.Maui.ApplicationModel.DataTransfer.ShareTextRequest {
                        Title = "AutoDiag Parts Agent", Text = _lastResult });
        };
        return new VerticalStackLayout {
            Spacing = 10,
            Children = {
                Theme.Eyebrow("AI PARTS AGENT"),
                Theme.MutedText("ИИ ищет детали для любой марки, уточняет совместимость, " +
                    "готовит письмо продавцу и помогает посчитать доставку. " +
                    "Без разрешения пользователя ничего не отправляет и не оплачивает."),
                _delivery,
                new HorizontalStackLayout { Spacing = 10, Children = {
                    _agentWeb, new Label { Text = "Онлайн-поиск (возможен платный OpenAI API)",
                        TextColor = Theme.Text, VerticalTextAlignment = TextAlignment.Center } } },
                newPart, used, enquiry, _sellerReply, compare, share
            }
        };
    }

    private async Task AgentAsync(string action)
    {
        var v = _state.SelectedVehicle;
        if (v is null) {
            _result.Text = "Выберите автомобиль, чтобы проверить совместимость."; return;
        }
        if (string.IsNullOrWhiteSpace(_query.Text)) {
            _result.Text = "Укажите деталь или двигатель."; return;
        }
        if (action == "compare" && string.IsNullOrWhiteSpace(_sellerReply.Text)) {
            _result.Text = "Вставьте текст ответа продавца."; return;
        }
        var region = Preferences.Default.Get("parts_region", "Германия / ЕС");
        var context = "Автомобиль: " + v.DisplayName + "; VIN: " + v.Vin +
            "; пробег: " + v.MileageKm + " км.\nРегион: " + region +
            "; доставка: " + (_delivery.Text ?? "не указана") +
            "\nПоследние DTC: " + string.Join(", ", _state.LastDtcCodes);
        var request = action switch {
            "used" => "Найди б/у деталь: " + _query.Text +
                ". Попроси фото, пробег, OEM маркировку, гарантию, проверку и доставку.",
            "enquiry" => "Составь письмо продавцу на понятном немецком и русском о покупке: " +
                _query.Text + ". Спроси наличие, цену, фото/видео, гарантию, возврат, " +
                "срок и цену доставки по адресу клиента. Не отправляй сам.",
            "compare" => "Проанализируй предложение продавца для " + _query.Text +
                ". Выдели только подтверждённые расходы, цену с доставкой, " +
                "недостающие вопросы и риски совместимости. Ответ продавца:\n" + _sellerReply.Text,
            _ => "Найди новую деталь: " + _query.Text +
                ". Проверь по VIN/OEM, покажи реальные ссылки лишь при наличии веб-поиска; " +
                "узнай цену, срок доставки и гарантию."
        };
        _result.Text = "AI Parts Agent подбирает информацию...";
        _result.TextColor = Theme.Muted;
        try {
            var online = _agentWeb.IsToggled && (action == "used" || action == "new");
            var reply = await _api.RunPartsAgentAsync(request, context, online);
            _lastResult = reply.Answer;
            _result.Text = reply.Answer +
                "\n\nПроверяйте наличие и совместимость у продавца до оплаты.";
            _result.TextColor = Theme.Text;
        } catch (Exception ex) {
            _result.Text = ex.Message;
            _result.TextColor = Theme.Red;
        }
    }

    private async Task FindAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _result.Text = "Сначала выберите автомобиль.";
            _result.TextColor = Theme.Accent;
            return;
        }
        if (string.IsNullOrWhiteSpace(_query.Text))
        {
            _result.Text = "Введите деталь или проблему.";
            return;
        }

        _result.Text = "Ищу совместимые варианты...";
        _result.TextColor = Theme.Muted;
        try
        {
            var region = Preferences.Default.Get("parts_region", "Германия / ЕС");
            var context =
                $"Автомобиль: {v.DisplayName}; VIN: {v.Vin}; пробег: {v.MileageKm} км.\n" +
                $"Последняя диагностика: {_state.LastDiagnosticSummary}\nРегион покупки: {region}.";

            var answer = await _api.AskAiAsync(
                "Подбери деталь по запросу клиента: " + _query.Text + ". " +
                "Сначала укажи, какие данные нужны для 100% совместимости. Не выдумывай OEM-номер. " +
                "Если данных хватает, покажи оригинал и качественные аналоги, ориентировочные цены, плюсы/минусы и актуальные варианты заказа в регионе.",
                context,
                true);

            _lastResult = answer.Answer;
            _result.Text = answer.Answer;
            _result.TextColor = Theme.Text;
        }
        catch (Exception ex)
        {
            _result.Text = ex.Message;
            _result.TextColor = Theme.Red;
        }
    }

    private async Task SaveBookmarkAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null || string.IsNullOrWhiteSpace(_lastResult)) return;
        var db = await _store.LoadAsync();
        db.PartBookmarks.Add(new PartBookmarkMobile
        {
            VehicleId = v.Id,
            Query = _query.Text?.Trim() ?? "",
            Result = _lastResult
        });
        await _store.SaveAsync(db);
        _result.Text = _lastResult + "\n\n✓ Сохранено в локальный список деталей.";
    }
}
