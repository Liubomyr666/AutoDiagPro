using AutoDiagPro.Mobile.Models;

namespace AutoDiagPro.Mobile.Services;

public sealed class MobileState
{
    public ServerVehicleRecord? SelectedVehicle { get; set; }
    public List<ServerVehicleRecord> Vehicles { get; set; } = new();
    public DateTimeOffset LastSyncUtc { get; set; }

    // Последняя диагностика используется AutoDiag AI как контекст,
    // чтобы клиенту не приходилось вручную копировать VIN/DTC/Live Data.
    public string LastDiagnosticSummary { get; set; } = "";
    public List<string> LastDtcCodes { get; set; } = new();
    public string LastVin { get; set; } = "";
    public DateTimeOffset? LastDiagnosticAtUtc { get; set; }

    // Вопрос, подготовленный другой страницей (например после Full Scan).
    public string PendingAiQuestion { get; set; } = "";
}