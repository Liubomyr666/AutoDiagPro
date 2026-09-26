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
public sealed class ServerVehicleRecord
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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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