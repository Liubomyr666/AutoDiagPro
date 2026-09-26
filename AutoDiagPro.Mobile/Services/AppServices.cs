namespace AutoDiagPro.Mobile.Services;

public static class AppServices
{
    public static IServiceProvider Services { get; set; } = null!;

    public static T Get<T>() where T : notnull
    {
        if (Services is null)
            throw new InvalidOperationException("Services are not initialized.");
        return Services.GetRequiredService<T>();
    }
}