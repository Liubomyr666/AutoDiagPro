namespace AutoDiagPro.Mobile.Models;

public sealed class ServerAuditRecord
{
    public Guid Id { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
