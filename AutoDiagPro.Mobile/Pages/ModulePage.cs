using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ModulePage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Label _title = Theme.H1("");
    private readonly Label _subtitle = Theme.MutedText("");
    private readonly Label _body = Theme.Body("");

    public ModulePage()
    {
        BackgroundColor = Theme.Page;
        Title = "AutoDiag";

        var back = Theme.CompactButton("‹ Назад");
        back.Clicked += async (_, _) => await Shell.Current.GoToAsync("..");

        var diag = Theme.PrimaryButton("Открыть диагностику");
        diag.Clicked += async (_, _) => await Shell.Current.GoToAsync("//diagnostics");

        var ai = Theme.SecondaryButton("Спросить AI по этому разделу");
        ai.Clicked += async (_, _) =>
        {
            _state.PendingAiQuestion = $"Помоги мне с разделом «{_state.PendingModuleTitle}» для выбранного автомобиля. Объясни, что можно проверить безопасно, какие данные нужны и какой правильный порядок действий.";
            await Shell.Current.GoToAsync("ai");
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    back,
                    Theme.Eyebrow("AUTODIAG MODULE"),
                    _title,
                    _subtitle,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            Theme.Pill("MOBILE WORKSPACE"),
                            _body
                        }
                    }),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            Theme.Eyebrow("БЕЗОПАСНЫЙ СЦЕНАРИЙ"),
                            Theme.Body("Сначала чтение VIN/ECU/DTC и проверка питания. Запись, кодирование или программирование не выполняются без подтверждения совместимости и явного действия пользователя.")
                        }
                    }),
                    diag,
                    ai
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _title.Text = string.IsNullOrWhiteSpace(_state.PendingModuleTitle) ? "Модуль" : _state.PendingModuleTitle;
        _subtitle.Text = _state.PendingModuleSubtitle;
        _body.Text = _state.PendingModuleBody;
    }
}