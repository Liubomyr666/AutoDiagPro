using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile;

public static class AccessPolicy
{
    public static string Role => AppServices.Get<ApiService>().Session?.Role?.Trim() ?? "";
    public static bool IsPlatformAdmin => AppServices.Get<ApiService>().Session?.IsPlatformAdmin == true;
    public static bool IsClient => !IsPlatformAdmin && string.Equals(Role, "Client", StringComparison.OrdinalIgnoreCase);
    public static bool IsMechanic => string.Equals(Role, "Mechanic", StringComparison.OrdinalIgnoreCase);
    public static bool IsAdmin => IsPlatformAdmin || string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) || string.Equals(Role, "Owner", StringComparison.OrdinalIgnoreCase);
    public static bool IsStaff => IsMechanic || IsAdmin;

    public static string FriendlyRole => IsPlatformAdmin ? "Platform Admin" : Role switch
    {
        var x when x.Equals("Owner", StringComparison.OrdinalIgnoreCase) => "Владелец СТО",
        var x when x.Equals("Admin", StringComparison.OrdinalIgnoreCase) => "Администратор СТО",
        var x when x.Equals("Mechanic", StringComparison.OrdinalIgnoreCase) => "Механик",
        _ => "Клиент"
    };

    public static async Task<bool> RequireStaffAsync(Page page)
    {
        if (IsStaff) return true;
        await page.DisplayAlert("Доступ ограничен", "Этот раздел доступен только сотрудникам СТО.", "OK");
        await Shell.Current.GoToAsync("//dashboard");
        return false;
    }

    public static async Task<bool> RequireAdminAsync(Page page)
    {
        if (IsAdmin) return true;
        await page.DisplayAlert("Доступ ограничен", "Этот раздел доступен только администраторам СТО.", "OK");
        await Shell.Current.GoToAsync("//dashboard");
        return false;
    }
}
