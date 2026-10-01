using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ClientMorePage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();

    public ClientMorePage()
    {
        Title = "Профиль";
        BackgroundColor = Theme.Page;

        var logout = Theme.SecondaryButton("Выйти из аккаунта");
        logout.TextColor = Theme.Red;
        logout.Clicked += async (_, _) => await LogoutAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 14, 16, 118),
                Spacing = 12,
                Children =
                {
                    ProfileCard(),
                    Theme.H2("Мой автомобиль"),
                    Row("Каталог двигателей", "Модели, поколения и быстрые подсказки", "tab_car.svg", "enginecatalog"),
                    Row("AI помощник", "Разбор неисправностей и вопросов", "tab_ai.png", "ai"),
                    Row("История автомобиля", "Диагностика, ремонт, ТО, детали и счета", "tab_history.png", "timeline"),
                    Row("История диагностики", "Все сохранённые проверки", "tab_history.png", "history"),
                    Row("Отчёты", "Сводка по автомобилю и сервису", "tab_report.png", "reports"),
                    Row("Уведомления", "ТО, DTC, записи и счета", "tab_service.png", "notifications"),
                    Row("План ТО", "Интервалы и напоминания", "tab_service.png", "service"),
                    Row("Детали по VIN", "Поиск оригинала и аналогов", "tab_parts.png", "parts"),
                    Row("Пробег", "Доступные данные автомобиля", "tab_car.png", "mileage"),
                    Row("Распознавание VIN", "Марка, регион и модельный год", "tab_car.png", "vehicleidentity"),
                    Row("Шины / колодки", "Замеры износа и история", "tab_wear.png", "wear"),
                    Theme.H2("Приложение"),
                    Row("Настройки", "Регион деталей и параметры", "tab_settings.png", "settings"),
                    logout,
                    Theme.MutedText($"AutoDiag Pro iOS • {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})")
                }
            }
        };
    }

    private View ProfileCard()
    {
        var session = _api.Session;

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Image
                        {
                            Source = "client_avatar.png",
                            WidthRequest = 48,
                            HeightRequest = 48
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 2,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label
                                {
                                    Text = session?.DisplayName ?? "Клиент",
                                    FontSize = 18,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Theme.Text,
                                    FontAutoScalingEnabled = false,
                                    MaxLines = 1,
                                    LineBreakMode = LineBreakMode.TailTruncation
                                },
                                Theme.MutedText(session?.Email ?? "")
                            }
                        }
                    }
                },
                Theme.Pill("КЛИЕНТ", Theme.Green),
                Theme.MutedText("В этом аккаунте доступны только функции владельца автомобиля. Служебные разделы СТО и администрирование скрыты.")
            }
        }, new Thickness(16), 18);
    }

    private async Task LogoutAsync()
    {
        var yes = await DisplayAlert("Выход", "Выйти из аккаунта AutoDiag Pro?", "Выйти", "Отмена");
        if (!yes) return;

        await _api.LogoutAsync();
        App.OpenLogin();
    }

    private static View Row(string title, string subtitle, string icon, string route)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        grid.Add(new Image
        {
            Source = icon,
            WidthRequest = 22,
            HeightRequest = 22,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        grid.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText(subtitle)
            }
        }, 1, 0);

        grid.Add(new Label
        {
            Text = "›",
            FontSize = 24,
            TextColor = Theme.Accent,
            VerticalTextAlignment = TextAlignment.Center,
            FontAutoScalingEnabled = false
        }, 2, 0);

        var card = Theme.CardView(grid, new Thickness(13), 15);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync(route);
        card.GestureRecognizers.Add(tap);
        return card;
    }
}

[executed on device: Rubakha (3db26fc9-a901-43ec-82ae-bbafd3175a39)]