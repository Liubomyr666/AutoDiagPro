using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<SessionStore>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<MobileState>();
        builder.Services.AddSingleton<MobileWorkspaceStore>();
        builder.Services.AddSingleton<QrScannerService>();
        builder.Services.AddSingleton<WifiObdTransport>();
        builder.Services.AddSingleton<BleObdTransport>();
        builder.Services.AddSingleton<Elm327Service>();

        return builder.Build();
    }
}
