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
        Routing.RegisterRoute("live", typeof(LiveDataPage));
        Routing.RegisterRoute("ecu", typeof(EcuInfoPage));
        Routing.RegisterRoute("repair", typeof(RepairBrainPage));
        Routing.RegisterRoute("service", typeof(ServicePlannerPage));
        Routing.RegisterRoute("parts", typeof(PartsPage));
        Routing.RegisterRoute("reports", typeof(ReportsPage));
        Routing.RegisterRoute("admin", typeof(AdminPage));
        Routing.RegisterRoute("addvehicle", typeof(AddVehiclePage));
        Routing.RegisterRoute("programming", typeof(ProgrammingCenterPage));
        Routing.RegisterRoute("qrparts", typeof(QrPartsPage));
        Routing.RegisterRoute("workshopmanager", typeof(WorkshopManagerPage));
        Routing.RegisterRoute("mileage", typeof(MileagePage));
        Routing.RegisterRoute("wear", typeof(WearPage));
        Routing.RegisterRoute("inventory", typeof(InventoryPage));
        Routing.RegisterRoute("diesel", typeof(DieselPage));
        Routing.RegisterRoute("injectors", typeof(InjectorPage));

        var tabs = new TabBar();

        if (AccessPolicy.IsClient)
        {
            tabs.Items.Add(Tab("Главная", "dashboard", typeof(ClientDashboardPage), "tab_home.png"));
            tabs.Items.Add(Tab("Авто", "vehicles", typeof(VehiclesPage), "tab_car.png"));
            tabs.Items.Add(Tab("Сканер", "diagnostics", typeof(DiagnosticsPage), "tab_scan.png"));
            tabs.Items.Add(Tab("Сервис", "clientservice", typeof(ClientServicePage), "tab_service.png"));
            tabs.Items.Add(Tab("Профиль", "more", typeof(ClientMorePage), "tab_profile.png"));
        }
        else
        {
            tabs.Items.Add(Tab("Главная", "dashboard", typeof(DashboardPage), "tab_home.png"));
            tabs.Items.Add(Tab("Диагностика", "diagnostics", typeof(DiagnosticsPage), "tab_scan.png"));
            tabs.Items.Add(Tab("Авто", "vehicles", typeof(VehiclesPage), "tab_car.png"));
            tabs.Items.Add(Tab("СТО", "workshop", typeof(WorkshopPage), "tab_workshop.png"));
            tabs.Items.Add(Tab("Ещё", "more", typeof(MorePage), "tab_more.png"));
        }

        Items.Add(tabs);
        SetTabBarBackgroundColor(this, Color.FromArgb("#0B0F12"));
        SetTabBarForegroundColor(this, Theme.Accent);
        SetTabBarUnselectedColor(this, Color.FromArgb("#7B858C"));
        SetTabBarTitleColor(this, Theme.Accent);
        SetNavBarIsVisible(this, false);
    }

    private static ShellContent Tab(string title, string route, Type pageType, string icon) =>
        new()
        {
            Title = title,
            Route = route,
            Icon = icon,
            ContentTemplate = new DataTemplate(pageType)
        };
}
