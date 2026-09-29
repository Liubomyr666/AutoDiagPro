using System.Text.Json;

namespace AutoDiagPro.Mobile.Services;

public sealed class MobileWorkspaceStore
{
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "autodiag_mobile_workspace.json");
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MobileWorkspaceDatabase? _cache;

    public async Task<MobileWorkspaceDatabase> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_cache is not null) return _cache;
            if (!File.Exists(_path))
                return _cache = new MobileWorkspaceDatabase();

            var text = await File.ReadAllTextAsync(_path);
            _cache = JsonSerializer.Deserialize<MobileWorkspaceDatabase>(text, _json) ?? new MobileWorkspaceDatabase();
            return _cache;
        }
        catch
        {
            return _cache = new MobileWorkspaceDatabase();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(MobileWorkspaceDatabase database)
    {
        await _gate.WaitAsync();
        try
        {
            _cache = database;
            var temp = _path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(database, _json));
            File.Move(temp, _path, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RepairCaseMobile> GetOrCreateRepairCaseAsync(Guid vehicleId)
    {
        var db = await LoadAsync();
        var existing = db.RepairCases
            .Where(x => x.VehicleId == vehicleId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault(x => !string.Equals(x.Status, "Закрыт", StringComparison.OrdinalIgnoreCase));

        if (existing is not null) return existing;

        var item = new RepairCaseMobile
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Status = "Диагностика",
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
        db.RepairCases.Add(item);
        await SaveAsync(db);
        return item;
    }

    public async Task<ServicePlanMobile> GetOrCreateServicePlanAsync(Guid vehicleId)
    {
        var db = await LoadAsync();
        var existing = db.ServicePlans.FirstOrDefault(x => x.VehicleId == vehicleId);
        if (existing is not null) return existing;

        var item = new ServicePlanMobile { VehicleId = vehicleId };
        db.ServicePlans.Add(item);
        await SaveAsync(db);
        return item;
    }
}

public sealed class MobileWorkspaceDatabase
{
    public List<RepairCaseMobile> RepairCases { get; set; } = new();
    public List<ServicePlanMobile> ServicePlans { get; set; } = new();
    public List<PartBookmarkMobile> PartBookmarks { get; set; } = new();
    public List<ReceivedPartMobile> ReceivedParts { get; set; } = new();
    public List<MobileClientRecord> Clients { get; set; } = new();
    public List<MobileAppointmentRecord> Appointments { get; set; } = new();
    public List<MobileInvoiceRecord> Invoices { get; set; } = new();
    public List<WearCheckMobile> WearChecks { get; set; } = new();
    public List<MobileNotificationRecord> Notifications { get; set; } = new();
    public List<PendingScanUploadMobile> PendingScans { get; set; } = new();
    public List<MobileEmployeeRecord> Employees { get; set; } = new();
}

public sealed class RepairCaseMobile
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string Status { get; set; } = "Диагностика";
    public string Complaint { get; set; } = "";
    public string DtcCodes { get; set; } = "";
    public string BeforeScan { get; set; } = "";
    public string ConfirmedCause { get; set; } = "";
    public string RepairDone { get; set; } = "";
    public string AfterScan { get; set; } = "";
    public string BeforePhotoPath { get; set; } = "";
    public string AfterPhotoPath { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServicePlanMobile
{
    public Guid VehicleId { get; set; }
    public long? CurrentMileageKm { get; set; }
    public long? NextServiceMileageKm { get; set; }
    public DateTimeOffset? NextServiceDate { get; set; }
    public string OilSpec { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class PartBookmarkMobile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public string Query { get; set; } = "";
    public string Result { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}


public sealed class ReceivedPartMobile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? VehicleId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.Now;
}


public sealed class MobileClientRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class MobileAppointmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? VehicleId { get; set; }
    public string ClientName { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.Now.AddHours(1);
    public string Work { get; set; } = "";
    public string Status { get; set; } = "Запланировано";
}

public sealed class MobileInvoiceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? VehicleId { get; set; }
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Paid { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}


public sealed class WearCheckMobile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public double? TireFrontMm { get; set; }
    public double? TireRearMm { get; set; }
    public double? PadFrontMm { get; set; }
    public double? PadRearMm { get; set; }
    public string Notes { get; set; } = "";
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class MobileNotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? VehicleId { get; set; }
    public string Key { get; set; } = "";
    public string Kind { get; set; } = "Инфо";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class PendingScanUploadMobile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public string Vin { get; set; } = "";
    public string Adapter { get; set; } = "";
    public string Protocol { get; set; } = "";
    public int DtcCount { get; set; }
    public string Summary { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class MobileEmployeeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Role { get; set; } = "Механик";
    public string Phone { get; set; } = "";
    public string Skills { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

