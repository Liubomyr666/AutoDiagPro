using AutoDiagPro.Mobile.Models;

namespace AutoDiagPro.Mobile.Services;

public sealed class MobileState
{
    public ServerVehicleRecord? SelectedVehicle { get; set; }
    public List<ServerVehicleRecord> Vehicles { get; set; } = new();
    public DateTimeOffset LastSyncUtc { get; set; }

    public string LastDiagnosticSummary { get; set; } = "";
    public List<string> LastDtcCodes { get; set; } = new();
    public string LastVin { get; set; } = "";
    public DateTimeOffset? LastDiagnosticAtUtc { get; set; }

    public string PendingAiQuestion { get; set; } = "";
    public string PendingModuleTitle { get; set; } = "";
    public string PendingModuleSubtitle { get; set; } = "";
    public string PendingModuleBody { get; set; } = "";
}