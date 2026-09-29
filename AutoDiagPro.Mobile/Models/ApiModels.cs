namespace AutoDiagPro.Mobile.Models;

public sealed class ServerSession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset AccessExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsPlatformAdmin { get; set; }
    public bool MustChangePassword { get; set; }
    public int DailyAiLimit { get; set; }
}

public sealed class ServerHealth
{
    public string Status { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset Utc { get; set; }
}

public sealed class ServerVinDecodeRecord
{
    public string Vin { get; set; } = "";
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? ModelYear { get; set; }
    public string? VehicleType { get; set; }
    public string? BodyClass { get; set; }
    public string? Engine { get; set; }
    public string? DisplacementL { get; set; }
    public string? FuelType { get; set; }
    public string? Transmission { get; set; }
    public string? DriveType { get; set; }
    public string? Source { get; set; }

    public int? ParsedYear => int.TryParse(ModelYear, out var year) ? year : null;
}

public class ServerVehicleRecord
{
    public Guid Id { get; set; }
    public Guid? ClientId { get; set; }
    public string? Vin { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Plate { get; set; }
    public long? MileageKm { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string DisplayName =>
        $"{Make ?? "Авто"} {Model ?? ""}".Trim() +
        (Year is null ? "" : $" • {Year}") +
        (string.IsNullOrWhiteSpace(Plate) ? "" : $" • {Plate}");
}

public sealed class ServerScanRecord
{
    public Guid Id { get; set; }
    public Guid? VehicleId { get; set; }
    public string? Vin { get; set; }
    public string? Adapter { get; set; }
    public string? Protocol { get; set; }
    public int DtcCount { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
}

public sealed class ServerWorkOrderRecord
{
    public Guid Id { get; set; }
    public Guid? VehicleId { get; set; }
    public string Number { get; set; } = "";
    public string Status { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string EstimateStatus { get; set; } = "Pending";
    public string? ClientDecisionNote { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServerVehicleCreate
{
    public Guid? ClientId { get; set; }
    public string? Vin { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Plate { get; set; }
    public long? MileageKm { get; set; }
}

public sealed class ServerAiResponse
{
    public string Answer { get; set; } = "";
    public string Model { get; set; } = "";
    public bool WebSearch { get; set; }
    public string Provider { get; set; } = "";
    public int UsedToday { get; set; }
    public int DailyLimit { get; set; }
}

public sealed class ExternalLoginStartResponse
{
    public Guid RequestId { get; set; }
    public string AuthorizationUrl { get; set; } = "";
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class ExternalLoginStatusResponse
{
    public string Status { get; set; } = "";
    public ServerSession? Session { get; set; }
    public string? Error { get; set; }
}

public sealed class ServerUserRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public bool MustChangePassword { get; set; }
    public int DailyAiLimit { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string DisplayLabel => $"{DisplayName} • {Role} • {Email}{(IsActive ? "" : " • отключён")}";
}

public sealed class ServerUserCreate
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Client";
    public string? Password { get; set; }
    public int? DailyAiLimit { get; set; }
}

public sealed class ServerUserUpdate
{
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public int? DailyAiLimit { get; set; }
    public string? Password { get; set; }
}

public sealed class ServerUserCreated
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string TemporaryPassword { get; set; } = "";
    public int DailyAiLimit { get; set; }
}

public sealed class ServerPasswordReset
{
    public string TemporaryPassword { get; set; } = "";
}

public sealed class ServerTenantRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long UsersCount { get; set; }
    public long VehiclesCount { get; set; }
    public long ScansCount { get; set; }
    public string DisplayLabel => $"{Name} • пользователей {UsersCount} • авто {VehiclesCount}{(IsActive ? "" : " • отключена")}";
}

public sealed class ServerTenantCreate
{
    public string Name { get; set; } = "";
    public string AdminEmail { get; set; } = "";
    public string AdminDisplayName { get; set; } = "";
    public string? AdminPassword { get; set; }
    public int? DailyAiLimit { get; set; }
}

public sealed class ServerTenantCreated
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = "";
    public Guid AdminUserId { get; set; }
    public string AdminEmail { get; set; } = "";
    public string AdminDisplayName { get; set; } = "";
    public string TemporaryPassword { get; set; } = "";
    public int DailyAiLimit { get; set; }
}

public sealed class ServerServiceIntakeRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public long? MileageKm { get; set; }
    public int? FuelPercent { get; set; }
    public string? Complaint { get; set; }
    public string? DamageNotes { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServerPhotoRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Kind { get; set; } = "";
    public string? Caption { get; set; }
    public string MimeType { get; set; } = "image/jpeg";
    public int SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ServerMaintenanceRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string Name { get; set; } = "";
    public long? DueMileage { get; set; }
    public DateOnly? DueDate { get; set; }
    public long? LastDoneMileage { get; set; }
    public DateTimeOffset? LastDoneAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServerInstalledPartRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Name { get; set; } = "";
    public string? PartNumber { get; set; }
    public string? Manufacturer { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal CustomerPrice { get; set; }
    public long? InstalledMileage { get; set; }
    public DateTimeOffset InstalledAt { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public string? MechanicName { get; set; }
}

public sealed class ServerAppointmentRecord
{
    public Guid Id { get; set; }
    public Guid? VehicleId { get; set; }
    public string ClientName { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public string Work { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServerInvoiceRecord
{
    public Guid Id { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Number { get; set; } = "";
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public bool Paid { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServerDashboardRecord
{
    public long Vehicles { get; set; }
    public long Clients { get; set; }
    public long ActiveOrders { get; set; }
    public long ReadyOrders { get; set; }
    public long UnpaidInvoices { get; set; }
    public decimal PaidToday { get; set; }
}

public sealed class ServerWorkshopSearchResult
{
    public List<ServerWorkshopClientRecord> Clients { get; set; } = new();
    public List<ServerWorkshopVehicleRecord> Vehicles { get; set; } = new();
}

public sealed class ServerWorkshopClientRecord
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
}

public sealed class ServerWorkshopVehicleRecord : ServerVehicleRecord
{
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientEmail { get; set; }
}


public sealed class ServerWearCheckRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public double? TireFrontMm { get; set; }
    public double? TireRearMm { get; set; }
    public double? PadFrontMm { get; set; }
    public double? PadRearMm { get; set; }
    public string? Notes { get; set; }
}

public sealed class ServerInventoryRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTimeOffset? LastReceivedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CreatedIdResponse
{
    public Guid Id { get; set; }
}