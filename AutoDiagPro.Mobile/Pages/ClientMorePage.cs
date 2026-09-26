using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ClientMorePage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();

    public ClientMorePage()
    {
        Title = "Профиль";
        BackgroundColor = Theme.Page;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 92),
                Spacing = 14,
                Children =
                {
                    ProfileCard(),
                    Theme.H2("Мои функции"),
                    Row("AI помощник", "Помощь по неисправностям и деталям", "tab_ai.png", "ai"),
                    Row("История диагностики", "Все ваши проверки автомобиля", "tab_history.png", "history"),
                    Row("Отчёты", "Сводка по автомобилю и работам", "tab_report.png", "reports"),
                    Row("Сервис / ТО", "Интервалы и напоминания", "tab_service.png", "service"),
                    Row("Детали по VIN", "Подбор совместимых деталей", "tab_parts.png", "parts"),
                    Row("Шины / колодки", "Износ и рекомендации", "tab_wear.png", "wear"),
                    Row("Настройки", "Аккаунт и параметры приложения", "tab_settings.png", "settings"),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            Theme.Eyebrow("БЕЗОПАСНОСТЬ"),
                            Theme.Body("Администрирование, сотрудники, склад СТО, счета СТО, QR-приём, программирование ECU, кодирование, ключи и служебные функции скрыты и недоступны клиентской роли.")
                        }
                    })
                }
            }
        };
    }

    private View ProfileCard()
    {
        var s = _api.Session;
        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Image { Source = "client_avatar.png", WidthRequest = 46, HeightRequest = 46 },
                        new VerticalStackLayout
                        {
                            Spacing = 2,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label { Text = s?.DisplayName ?? "Клиент", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, FontAutoScalingEnabled = false },
                                Theme.MutedText(s?.Email ?? "")
                            }
                        }
                    }
                },
                Theme.Pill("CLIENT ACCOUNT", Theme.Green)
            }
        }, new Thickness(16));
    }

    private static View Row(string title, string subtitle, string icon, string route)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 12
        };
        grid.Add(new Image { Source = icon, WidthRequest = 22, HeightRequest = 22 }, 0, 0);
        grid.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, FontAutoScalingEnabled = false },
                Theme.MutedText(subtitle)
            }
        }, 1, 0);
        grid.Add(new Label { Text = "›", FontSize = 26, TextColor = Theme.Accent, VerticalTextAlignment = TextAlignment.Center, FontAutoScalingEnabled = false }, 2, 0);

        var card = Theme.CardView(grid, new Thickness(13));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync(route);
        card.GestureRecognizers.Add(tap);
        return card;
    }
}
