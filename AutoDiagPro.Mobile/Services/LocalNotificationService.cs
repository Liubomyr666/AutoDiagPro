#if IOS
using UserNotifications;
#endif

namespace AutoDiagPro.Mobile.Services;

public static class LocalNotificationService
{
    public static async Task<bool> EnsurePermissionAsync()
    {
#if IOS
        var result = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound);
        return result.Item1;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public static async Task ScheduleAsync(string id, string title, string body, DateTimeOffset when)
    {
#if IOS
        if (when <= DateTimeOffset.Now.AddSeconds(5)) return;
        if (!await EnsurePermissionAsync()) return;

        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = body,
            Sound = UNNotificationSound.Default
        };

        var delay = Math.Max(5, (when - DateTimeOffset.Now).TotalSeconds);
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(delay, false);
        var request = UNNotificationRequest.FromIdentifier(id, content, trigger);
        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
#else
        await Task.CompletedTask;
#endif
    }
}
