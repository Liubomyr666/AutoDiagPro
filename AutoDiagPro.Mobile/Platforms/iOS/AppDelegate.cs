using AutoDiagPro.Mobile.Services;
using Foundation;

namespace AutoDiagPro.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        var app = MauiProgram.CreateMauiApp();
        AppServices.Services = app.Services;
        return app;
    }
}
