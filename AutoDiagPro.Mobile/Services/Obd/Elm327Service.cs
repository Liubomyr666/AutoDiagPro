using System.Globalization;
using System.Text;

namespace AutoDiagPro.Mobile.Services.Obd;

public sealed class Elm327Service
{
    private readonly WifiObdTransport _wifi;
    private readonly BleObdTransport _ble;
    private IObdTransport? _transport;

    public Elm327Service(WifiObdTransport wifi, BleObdTransport ble)
    {
        _wifi = wifi;
        _ble = ble;
    }

    public bool IsConnected => _transport?.IsConnected == true;
    public string TransportName => _transport?.Name ?? "—";
    public string Endpoint => _transport?.Endpoint ?? "—";

    public async Task ConnectWifiAsync(string host, int port, CancellationToken ct = default)
    {
        _wifi.Host = host.Trim();
        _wifi.Port = port;
        _transport = _wifi;
        await _wifi.ConnectAsync(ct);

        await InitializeElmAsync(ct);
    }

    public Task<IReadOnlyList<BleObdDevice>> ScanBleAsync(CancellationToken ct = default) =>
        _ble.ScanAsync(TimeSpan.FromSeconds(8), ct);

    public async Task ConnectBleAsync(BleObdDevice device, CancellationToken ct = default)
    {
        _ble.SelectedDeviceId = device.Id;
        _ble.SelectedDeviceName = device.Name;
        _transport = _ble;
        await _ble.ConnectAsync(ct);
        await InitializeElmAsync(ct);
    }

    private async Task InitializeElmAsync(CancellationToken ct)
    {
        await CommandAsync("ATZ", 2500, ct);
        await CommandAsync("ATE0", 1200, ct);
        await CommandAsync("ATL0", 1200, ct);
        await CommandAsync("ATS0", 1200, ct);
        await CommandAsync("ATH0", 1200, ct);
        await CommandAsync("ATSP0", 1500, ct);
    }

    public async Task DisconnectAsync()
    {
        if (_transport is not null)
            await _transport.DisconnectAsync();
        _transport = null;
    }

    public async Task<string> CommandAsync(string command, int timeoutMs = 1800, CancellationToken ct = default)
    {
        if (_transport is null || !_transport.IsConnected)
            throw new InvalidOperationException("OBD не подключён.");

        await _transport.WriteAsync(command, ct);
        var response = await _transport.ReadUntilPromptAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
        return Clean(response);
    }

    public async Task<string> VoltageAsync(CancellationToken ct = default) =>
        FirstLine(await CommandAsync("ATRV", 1200, ct));

    public async Task<string> ProtocolAsync(CancellationToken ct = default) =>
        FirstLine(await CommandAsync("ATDP", 1200, ct));

    public async Task<string> VinAsync(CancellationToken ct = default)
    {
        var raw = await CommandAsync("0902", 2500, ct);
        if (raw.Contains("NO DATA", StringComparison.OrdinalIgnoreCase)) return "";

        var hex = HexOnly(raw);
        var marker = hex.IndexOf("4902", StringComparison.Ordinal);
        if (marker < 0) return "";
        var data = hex[(marker + 4)..];
        if (data.StartsWith("01") && data.Length >= 36) data = data[2..];

        var bytes = HexBytes(data);
        var vin = new string(Encoding.ASCII.GetString(bytes).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return vin.Length >= 17 ? vin[..17] : vin;
    }
    public async Task<List<string>> DtcAsync(CancellationToken ct = default)
    {
        var raw = await CommandAsync("03", 2200, ct);
        if (raw.Contains("NO DATA", StringComparison.OrdinalIgnoreCase)) return new();

        var hex = HexOnly(raw);
        var marker = hex.IndexOf("43", StringComparison.Ordinal);
        if (marker < 0) return new();

        var data = hex[(marker + 2)..];
        var result = new List<string>();
        for (var i = 0; i + 3 < data.Length; i += 4)
        {
            if (!ushort.TryParse(data.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                continue;
            if (value == 0) continue;

            const string type = "PCBU";
            result.Add($"{type[(value >> 14) & 3]}{(value >> 12) & 3}{(value >> 8) & 15:X}{(value >> 4) & 15:X}{value & 15:X}");
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    public async Task<Dictionary<string, string>> LiveSnapshotAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        result["Напряжение"] = await SafeAsync(() => VoltageAsync(ct), "—");

        var rpm = await ReadPidAsync("010C", "410C", ct);
        if (rpm is { Length: >= 4 })
            result["RPM"] = ((B(rpm, 0) * 256 + B(rpm, 2)) / 4.0).ToString("0", CultureInfo.InvariantCulture);

        var temp = await ReadPidAsync("0105", "4105", ct);
        if (temp is { Length: >= 2 })
            result["ОЖ"] = $"{B(temp, 0) - 40} °C";

        var speed = await ReadPidAsync("010D", "410D", ct);
        if (speed is { Length: >= 2 })
            result["Скорость"] = $"{B(speed, 0)} км/ч";

        var load = await ReadPidAsync("0104", "4104", ct);
        if (load is { Length: >= 2 })
            result["Нагрузка"] = $"{Math.Round(B(load, 0) * 100.0 / 255, 1)} %";

        return result;
    }
    private async Task<string?> ReadPidAsync(string command, string prefix, CancellationToken ct)
    {
        var raw = await CommandAsync(command, 1600, ct);
        var hex = HexOnly(raw);
        var marker = hex.IndexOf(prefix, StringComparison.Ordinal);
        return marker < 0 ? null : hex[(marker + prefix.Length)..];
    }

    private static int B(string hex, int offset) =>
        Convert.ToInt32(hex.Substring(offset, 2), 16);

    private static string Clean(string value) =>
        value.Replace("\r", "\n").Replace(">", "").Trim();

    private static string FirstLine(string value) =>
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "—";

    private static string HexOnly(string value) =>
        new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

    private static byte[] HexBytes(string hex)
    {
        var bytes = new List<byte>();
        for (var i = 0; i + 1 < hex.Length; i += 2)
            if (byte.TryParse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                bytes.Add(b);
        return bytes.ToArray();
    }

    private static async Task<string> SafeAsync(Func<Task<string>> action, string fallback)
    {
        try { return await action(); } catch { return fallback; }
    }
}