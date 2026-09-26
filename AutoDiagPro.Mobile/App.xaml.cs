using AutoDiagPro.Mobile.Pages;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        AppServices.Services = services;

        try
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Dark;
            MainPage = CreateLoginPage();
        }
        catch (Exception ex)
        {
            MainPage = BuildStartupErrorPage(ex);
        }
    }

    private static Page CreateLoginPage() =>
        new NavigationPage(new LoginPage())
        {
            BarBackgroundColor = Color.FromArgb("#0A0D0F"),
            BarTextColor = Colors.White
        };

    private static Page BuildStartupErrorPage(Exception ex)
    {
        var retry = new Button
        {
            Text = "Перезапустить экран входа",
            BackgroundColor = Color.FromArgb("#E7A13B"),
            TextColor = Color.FromArgb("#111315"),
            CornerRadius = 12,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };

        var page = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#090C0E"),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(24, 70, 24, 30),
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
                            Text = ex.Message,
                            FontSize = 13,
                            TextColor = Color.FromArgb("#B8C0C5")
                        },
                        retry
                    }
                }
            }
        };

        retry.Clicked += async (_, _) =>
        {
            try
            {
                Current!.MainPage = CreateLoginPage();
            }
            catch (Exception retryEx)
            {
                await page.DisplayAlert("AutoDiag Pro", retryEx.Message, "OK");
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

    public static void OpenLogin() =>
        Current!.MainPage = CreateLoginPage();
}
