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