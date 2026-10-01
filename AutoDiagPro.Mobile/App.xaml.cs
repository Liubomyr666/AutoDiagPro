using AutoDiagPro.Mobile.Pages;

namespace AutoDiagPro.Mobile;

public partial class App : Application
{
    private readonly ContentPage _bootPage;

    public App()
    {
        UserAppTheme = AppTheme.Dark;

        _bootPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#070B10"),
            Content = new Grid
            {
                Padding = new Thickness(28),
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 12,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label
                            {
                                Text = "AutoDiag Pro",
                                FontSize = 30,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Colors.White,
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            new Label
                            {
                                Text = "Запуск приложения…",
                                FontSize = 14,
                                TextColor = Color.FromArgb("#2F80FF"),
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        }
                    }
                }
            }
        };

        MainPage = _bootPage;

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
#if SMOKE_CLIENT_UI
            MainPage = new AppShell();
#else
            OpenLoginSafe();
#endif
        });
    }

    private static Page CreateLoginPage() =>
        new NavigationPage(new LoginPage())
        {
            BarBackgroundColor = Color.FromArgb("#0A0D0F"),
            BarTextColor = Colors.White
        };

    private void OpenLoginSafe()
    {
        try
        {
            MainPage = CreateLoginPage();
        }
        catch (Exception ex)
        {
            MainPage = BuildStartupErrorPage(ex);
        }
    }

    private static Page BuildStartupErrorPage(Exception ex)
    {
        var retry = new Button
        {
            Text = "Повторить запуск",
            BackgroundColor = Color.FromArgb("#2F80FF"),
            TextColor = Color.FromArgb("#111315"),
            CornerRadius = 12,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };

        var detail = new Label
        {
            Text = ex.ToString(),
            FontSize = 11,
            TextColor = Color.FromArgb("#B8C0C5"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var page = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#070B10")
        };

        retry.Clicked += (_, _) =>
        {
            try
            {
                Current!.MainPage = CreateLoginPage();
            }
            catch (Exception retryEx)
            {
                detail.Text = retryEx.ToString();
            }
        };

        page.Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 64, 24, 30),
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "AutoDiag Pro",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    },
                    new Label
                    {
                        Text = "Ошибка запуска",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#EE636B")
                    },
                    new Label
                    {
                        Text = "Скопируй или сфотографируй текст ниже — теперь приложение не должно оставаться на пустом чёрном экране.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#D7DDE0")
                    },
                    detail,
                    retry
                }
            }
        };

        return page;
    }

    public static void OpenMain()
    {
        try
        {
            Current!.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            Current!.MainPage = BuildStartupErrorPage(ex);
        }
    }

    public static void OpenLogin()
    {
        try
        {
            Current!.MainPage = CreateLoginPage();
        }
        catch (Exception ex)
        {
            Current!.MainPage = BuildStartupErrorPage(ex);
        }
    }
}
