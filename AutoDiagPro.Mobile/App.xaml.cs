using AutoDiagPro.Mobile.Pages;

namespace AutoDiagPro.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
        MainPage = new NavigationPage(new LoginPage())
        {
            BarBackgroundColor = Color.FromArgb("#0A0D0F"),
            BarTextColor = Colors.White
        };
    }

    public static void OpenMain() =>
        Current!.MainPage = new AppShell();

    public static void OpenLogin() =>
        Current!.MainPage = new NavigationPage(new LoginPage());
}