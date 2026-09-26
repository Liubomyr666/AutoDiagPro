using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile;

public static class AccessPolicy
{
    private static string Role =>
        AppServices.Get<ApiService>().Session?.Role?.Trim() ?? "";

    public static bool IsPlatformAdmin =>
        AppServices.Get<ApiService>().Session?.IsPlatformAdmin == true;

    public static bool IsAdmin =>
        IsPlatformAdmin ||
        Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("Owner", StringComparison.OrdinalIgnoreCase);

    public static bool IsManager =>
        Role.Equals("Manager", StringComparison.OrdinalIgnoreCase);

    public static bool IsMechanic =>
        Role.Equals("Mechanic", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("Technician", StringComparison.OrdinalIgnoreCase) ||
        Role.Equals("Staff", StringComparison.OrdinalIgnoreCase);

    public static bool IsStaff =>
        IsAdmin || IsManager || IsMechanic;

    public static bool IsClient => !IsStaff;

    public static string FriendlyRole => IsPlatformAdmin ? "Platform Admin" :
        IsAdmin ? (Role.Equals("Owner", StringComparison.OrdinalIgnoreCase) ? "Владелец СТО" : "Администратор СТО") :
        IsManager ? "Менеджер СТО" :
        IsMechanic ? "Механик" :
        "Клиент";

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
