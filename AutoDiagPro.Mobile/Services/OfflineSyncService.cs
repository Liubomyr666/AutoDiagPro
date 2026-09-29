namespace AutoDiagPro.Mobile.Services;

public static class OfflineSyncService
{
    public static async Task<bool> UploadOrQueueScanAsync(
        ApiService api,
        MobileWorkspaceStore store,
        Guid vehicleId,
        string? vin,
        string adapter,
        string protocol,
        int dtcCount,
        string summary)
    {
        try
        {
            await api.UploadScanAsync(vehicleId, vin, adapter, protocol, dtcCount, summary);
            return true;
        }
        catch
        {
            var db = await store.LoadAsync();
            db.PendingScans.Add(new PendingScanUploadMobile
            {
                VehicleId = vehicleId,
                Vin = vin ?? "",
                Adapter = adapter,
                Protocol = protocol,
                DtcCount = dtcCount,
                Summary = summary,
                CreatedAt = DateTimeOffset.Now
            });
            await store.SaveAsync(db);
            return false;
        }
    }

    public static async Task<int> FlushAsync(ApiService api, MobileWorkspaceStore store)
    {
        var db = await store.LoadAsync();
        if (db.PendingScans.Count == 0) return 0;

        var sent = 0;
        foreach (var item in db.PendingScans.OrderBy(x => x.CreatedAt).ToList())
        {
            try
            {
                await api.UploadScanAsync(item.VehicleId, item.Vin, item.Adapter, item.Protocol, item.DtcCount, item.Summary);
                db.PendingScans.Remove(item);
                sent++;
            }
            catch
            {
                break;
            }
        }

        if (sent > 0) await store.SaveAsync(db);
        return sent;
    }
}
