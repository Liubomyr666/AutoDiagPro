using AutoDiagPro.Mobile.Pages;

namespace AutoDiagPro.Mobile;

public sealed class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        BackgroundColor = Theme.Page;

        Routing.RegisterRoute("ai", typeof(AiPage));
        Routing.RegisterRoute("history", typeof(HistoryPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("module", typeof(ModulePage));

        var tabs = new TabBar();
        tabs.Items.Add(Tab("Главная", "dashboard", typeof(DashboardPage)));
        tabs.Items.Add(Tab("Диагностика", "diagnostics", typeof(DiagnosticsPage)));
        tabs.Items.Add(Tab("Авто", "vehicles", typeof(VehiclesPage)));
        tabs.Items.Add(Tab("СТО", "workshop", typeof(WorkshopPage)));
        tabs.Items.Add(Tab("Ещё", "more", typeof(MorePage)));
        Items.Add(tabs);

        SetTabBarBackgroundColor(this, Color.FromArgb("#0D1114"));
        SetTabBarForegroundColor(this, Theme.Accent);
        SetTabBarUnselectedColor(this, Color.FromArgb("#71818B"));
        SetTabBarTitleColor(this, Theme.Accent);
        SetNavBarIsVisible(this, false);
    }

    private static ShellContent Tab(string title, string route, Type pageType) =>
        new()
        {
            Title = title,
            Route = route,
            ContentTemplate = new DataTemplate(pageType)
        };
}