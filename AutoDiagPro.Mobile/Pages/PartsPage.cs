using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class PartsPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();

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
