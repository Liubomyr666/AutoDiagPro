using System.Text;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace AutoDiagPro.Mobile.Services.Obd;

public sealed record BleObdDevice(Guid Id, string Name, int Rssi)
{
    public string Display => $"{Name} • {Rssi} dBm";
}

public sealed class BleObdTransport : IObdTransport
{
    private readonly IBluetoothLE _ble = CrossBluetoothLE.Current;
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;
    private readonly Dictionary<Guid, IDevice> _seen = new();
    private readonly StringBuilder _rx = new();
    private readonly SemaphoreSlim _rxSignal = new(0, 1);

    private IDevice? _device;
    private ICharacteristic? _write;
    private ICharacteristic? _notify;

    public Guid SelectedDeviceId { get; set; }
    public string SelectedDeviceName { get; set; } = "";
    public bool IsConnected => _device?.State == Plugin.BLE.Abstractions.DeviceState.Connected && _write is not null;
    public string Name => "Bluetooth LE OBD";
    public string Endpoint => string.IsNullOrWhiteSpace(SelectedDeviceName) ? SelectedDeviceId.ToString() : SelectedDeviceName;
    public string BluetoothState => _ble.State.ToString();

    public async Task<IReadOnlyList<BleObdDevice>> ScanAsync(TimeSpan? duration = null, CancellationToken ct = default)
    {
        if (_ble.State != Plugin.BLE.Abstractions.Contracts.BluetoothState.On)
            throw new InvalidOperationException("Bluetooth выключен или недоступен на iPhone.");

        _seen.Clear();
        var found = new Dictionary<Guid, BleObdDevice>();

        void Handler(object? _, DeviceEventArgs e)
        {
            var d = e.Device;
            if (d is null || d.Id == Guid.Empty) return;
            var name = string.IsNullOrWhiteSpace(d.Name) ? "BLE OBD" : d.Name.Trim();
            _seen[d.Id] = d;
            found[d.Id] = new BleObdDevice(d.Id, name, d.Rssi);
        }

        _adapter.DeviceDiscovered += Handler;
        _adapter.ScanTimeout = (int)(duration ?? TimeSpan.FromSeconds(8)).TotalMilliseconds;
        try
        {
            await _adapter.StartScanningForDevicesAsync(cancellationToken: ct);
        }
        finally
        {
            _adapter.DeviceDiscovered -= Handler;
            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();
        }

        return found.Values
            .OrderByDescending(x => LooksLikeObd(x.Name))
            .ThenByDescending(x => x.Rssi)
            .ToList();
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (SelectedDeviceId == Guid.Empty)
            throw new InvalidOperationException("Сначала выберите Bluetooth OBD-адаптер.");

        await DisconnectAsync();

        if (!_seen.TryGetValue(SelectedDeviceId, out var device))
            device = await _adapter.ConnectToKnownDeviceAsync(SelectedDeviceId, cancellationToken: ct);
        else
            await _adapter.ConnectToDeviceAsync(device, cancellationToken: ct);

        _device = device;
        SelectedDeviceName = string.IsNullOrWhiteSpace(device.Name) ? SelectedDeviceName : device.Name;

        ICharacteristic? firstWrite = null;
        ICharacteristic? firstNotify = null;

        foreach (var service in await device.GetServicesAsync())
        {
            foreach (var characteristic in await service.GetCharacteristicsAsync())
            {
                if (firstWrite is null && characteristic.CanWrite)
                    firstWrite = characteristic;
                if (firstNotify is null && characteristic.CanUpdate)
                    firstNotify = characteristic;

                var id = characteristic.Id.ToString().ToLowerInvariant();
                if (id.Contains("fff2") && characteristic.CanWrite)
                    firstWrite = characteristic;
                if (id.Contains("fff1") && characteristic.CanUpdate)
                    firstNotify = characteristic;
            }
        }

        _write = firstWrite ?? throw new InvalidOperationException("BLE-адаптер не имеет характеристики записи.");
        _notify = firstNotify ?? throw new InvalidOperationException("BLE-адаптер не имеет характеристики уведомлений.");

        _notify.ValueUpdated += NotifyOnValueUpdated;
        await _notify.StartUpdatesAsync();
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_notify is not null)
            {
                _notify.ValueUpdated -= NotifyOnValueUpdated;
                try { await _notify.StopUpdatesAsync(); } catch { }
            }
        }
        catch { }

        try
        {
            if (_device is not null)
                await _adapter.DisconnectDeviceAsync(_device);
        }
        catch { }

        _write = null;
        _notify = null;
        _device = null;
        lock (_rx) _rx.Clear();
    }

    public async Task WriteAsync(string command, CancellationToken ct = default)
    {
        if (_write is null) throw new InvalidOperationException("Bluetooth OBD не подключён.");
        var bytes = Encoding.ASCII.GetBytes(command.Trim() + "\r");
        await _write.WriteAsync(bytes, ct);
    }

    public async Task<string> ReadUntilPromptAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        while (!linked.IsCancellationRequested)
        {
            string snapshot;
            lock (_rx)
            {
                snapshot = _rx.ToString();
                if (snapshot.Contains('>'))
                {
                    _rx.Clear();
                    return snapshot;
                }
            }

            await _rxSignal.WaitAsync(linked.Token);
        }

        lock (_rx)
        {
            var snapshot = _rx.ToString();
            _rx.Clear();
            return snapshot;
        }
    }

    private void NotifyOnValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var bytes = e.Characteristic.Value;
        if (bytes is null || bytes.Length == 0) return;

        lock (_rx)
            _rx.Append(Encoding.ASCII.GetString(bytes));

        if (_rxSignal.CurrentCount == 0)
            _rxSignal.Release();
    }

    private static bool LooksLikeObd(string name)
    {
        var n = name.ToUpperInvariant();
        return n.Contains("VGATE") || n.Contains("ICAR") || n.Contains("OBD") ||
               n.Contains("ELM") || n.Contains("V-LINK") || n.Contains("VLINK");
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}