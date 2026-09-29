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

    public async Task<string> AdapterIdAsync(CancellationToken ct = default) =>
        FirstLine(await CommandAsync("ATI", 1200, ct));

    public async Task<string> VoltageAsync(CancellationToken ct = default) =>
        FirstLine(await CommandAsync("ATRV", 1200, ct));

    public async Task<string> ProtocolAsync(CancellationToken ct = default) =>
        FirstLine(await CommandAsync("ATDP", 1200, ct));

    public async Task<string> VinAsync(CancellationToken ct = default)
    {
        var raw = await CommandAsync("0902", 2600, ct);
        return DecodeMode09Ascii(raw, "4902", 17);
    }

    public async Task<Dictionary<string, string>> EcuInfoAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>
        {
            ["Протокол"] = await SafeAsync(() => ProtocolAsync(ct), "—"),
            ["Напряжение"] = await SafeAsync(() => VoltageAsync(ct), "—"),
            ["VIN"] = await SafeAsync(() => VinAsync(ct), "—")
        };

        result["Calibration ID"] = await SafeAsync(async () =>
        {
            var raw = await CommandAsync("0904", 2600, ct);
            var value = DecodeMode09Ascii(raw, "4904", 0);
            return string.IsNullOrWhiteSpace(value) ? "Не поддерживается" : value;
        }, "Не поддерживается");

        result["CVN"] = await SafeAsync(async () =>
        {
            var raw = await CommandAsync("0906", 2600, ct);
            var hex = HexOnly(raw);
            var marker = hex.IndexOf("4906", StringComparison.Ordinal);
            if (marker < 0) return "Не поддерживается";
            var data = hex[(marker + 4)..];
            if (data.StartsWith("01", StringComparison.Ordinal) && data.Length > 2) data = data[2..];
            return data.Length >= 8 ? GroupHex(data, 8) : "Не поддерживается";
        }, "Не поддерживается");

        result["ECU Name"] = await SafeAsync(async () =>
        {
            var raw = await CommandAsync("090A", 2600, ct);
            var value = DecodeMode09Ascii(raw, "490A", 0);
            return string.IsNullOrWhiteSpace(value) ? "Не поддерживается" : value;
        }, "Не поддерживается");

        return result;
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

    public async Task ClearDtcAsync(CancellationToken ct = default)
    {
        var response = await CommandAsync("04", 2500, ct);
        if (response.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("UNABLE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ECU не подтвердил очистку DTC.");
    }

    public async Task<Dictionary<string, string>> ReadinessAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        var data = await ReadPidAsync("0101", "4101", ct);
        if (data is not { Length: >= 8 })
        {
            result["Readiness"] = "Не поддерживается";
            return result;
        }

        var a = B(data, 0);
        var b = B(data, 2);
        var c = B(data, 4);
        var d = B(data, 6);

        result["MIL"] = (a & 0x80) != 0 ? "Включён" : "Выключен";
        result["DTC сохранено"] = (a & 0x7F).ToString(CultureInfo.InvariantCulture);
        result["Тип двигателя"] = (b & 0x08) != 0 ? "Дизель" : "Бензин";
        result["Readiness bytes"] = $"{b:X2} {c:X2} {d:X2}";
        return result;
    }

    public async Task<IReadOnlyList<string>> SupportedPidsAsync(CancellationToken ct = default)
    {
        var supported = new List<string>();
        foreach (var start in new[] { 0x00, 0x20, 0x40, 0x60 })
        {
            var pid = start.ToString("X2", CultureInfo.InvariantCulture);
            var data = await ReadPidAsync($"01{pid}", $"41{pid}", ct);
            if (data is not { Length: >= 8 }) continue;

            var bytes = new[] { B(data, 0), B(data, 2), B(data, 4), B(data, 6) };
            for (var bit = 0; bit < 32; bit++)
            {
                var byteIndex = bit / 8;
                var bitInByte = 7 - (bit % 8);
                if ((bytes[byteIndex] & (1 << bitInByte)) == 0) continue;
                supported.Add($"01{(start + bit + 1):X2}");
            }
        }

        return supported.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Dictionary<string, string>> LiveSnapshotAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>
        {
            ["Напряжение адаптера"] = await SafeAsync(() => VoltageAsync(ct), "—")
        };

        await AddPidAsync(result, "RPM", "010C", "410C", d => ((B(d, 0) * 256 + B(d, 2)) / 4.0).ToString("0", CultureInfo.InvariantCulture), ct);
        await AddPidAsync(result, "Температура ОЖ", "0105", "4105", d => $"{B(d, 0) - 40} °C", ct);
        await AddPidAsync(result, "Скорость", "010D", "410D", d => $"{B(d, 0)} км/ч", ct);
        await AddPidAsync(result, "Нагрузка", "0104", "4104", d => $"{Math.Round(B(d, 0) * 100.0 / 255, 1)} %", ct);
        await AddPidAsync(result, "STFT Bank 1", "0106", "4106", d => $"{Math.Round((B(d, 0) - 128) * 100.0 / 128, 1)} %", ct);
        await AddPidAsync(result, "LTFT Bank 1", "0107", "4107", d => $"{Math.Round((B(d, 0) - 128) * 100.0 / 128, 1)} %", ct);
        await AddPidAsync(result, "Давление топлива", "010A", "410A", d => $"{B(d, 0) * 3} кПа", ct);
        await AddPidAsync(result, "MAP", "010B", "410B", d => $"{B(d, 0)} кПа", ct);
        await AddPidAsync(result, "Угол зажигания", "010E", "410E", d => $"{B(d, 0) / 2.0 - 64:0.0}°", ct);
        await AddPidAsync(result, "Температура впуска", "010F", "410F", d => $"{B(d, 0) - 40} °C", ct);
        await AddPidAsync(result, "MAF", "0110", "4110", d => $"{(B(d, 0) * 256 + B(d, 2)) / 100.0:0.00} г/с", ct);
        await AddPidAsync(result, "Дроссель", "0111", "4111", d => $"{Math.Round(B(d, 0) * 100.0 / 255, 1)} %", ct);
        await AddPidAsync(result, "Время работы", "011F", "411F", d => FormatSeconds(B(d, 0) * 256 + B(d, 2)), ct);
        await AddPidAsync(result, "Уровень топлива", "012F", "412F", d => $"{Math.Round(B(d, 0) * 100.0 / 255, 1)} %", ct);
        await AddPidAsync(result, "Пробег после сброса DTC", "0131", "4131", d => $"{B(d, 0) * 256 + B(d, 2)} км", ct);
        await AddPidAsync(result, "Напряжение ECU", "0142", "4142", d => $"{(B(d, 0) * 256 + B(d, 2)) / 1000.0:0.000} В", ct);
        await AddPidAsync(result, "Температура воздуха", "0146", "4146", d => $"{B(d, 0) - 40} °C", ct);
        await AddPidAsync(result, "Температура масла", "015C", "415C", d => $"{B(d, 0) - 40} °C", ct);
        await AddPidAsync(result, "Расход топлива", "015E", "415E", d => $"{(B(d, 0) * 256 + B(d, 2)) / 20.0:0.00} л/ч", ct);

        return result;
    }


    public async Task<Dictionary<string, string>> InjectorSnapshotAsync(CancellationToken ct = default)
    {
        var live = await LiveSnapshotAsync(ct);
        var result = new Dictionary<string, string>();

        var keys = new[]
        {
            "Давление топлива",
            "STFT Bank 1",
            "LTFT Bank 1",
            "MAF",
            "MAP",
            "RPM",
            "Нагрузка",
            "Расход топлива",
            "Уровень топлива",
            "Температура впуска",
            "Температура масла",
            "Напряжение ECU"
        };

        foreach (var key in keys)
            if (live.TryGetValue(key, out var value))
                result[key] = value;

        if (result.Count == 0)
            result["Статус"] = "Стандартные OBD-II параметры топливной системы недоступны. Для коррекций форсунок по цилиндрам нужен марочный протокол.";

        return result;
    }

    private async Task AddPidAsync(
        Dictionary<string, string> target,
        string label,
        string command,
        string prefix,
        Func<string, string> formatter,
        CancellationToken ct)
    {
        try
        {
            var data = await ReadPidAsync(command, prefix, ct);
            if (!string.IsNullOrWhiteSpace(data))
                target[label] = formatter(data);
        }
        catch
        {
            // PID unsupported or temporarily unavailable.
        }
    }

    private async Task<string?> ReadPidAsync(string command, string prefix, CancellationToken ct)
    {
        var raw = await CommandAsync(command, 1600, ct);
        if (raw.Contains("NO DATA", StringComparison.OrdinalIgnoreCase)) return null;
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

    private static string DecodeMode09Ascii(string raw, string markerText, int maxLength)
    {
        if (raw.Contains("NO DATA", StringComparison.OrdinalIgnoreCase)) return "";
        var hex = HexOnly(raw);
        var marker = hex.IndexOf(markerText, StringComparison.Ordinal);
        if (marker < 0) return "";

        var data = hex[(marker + markerText.Length)..];
        if (data.StartsWith("01", StringComparison.Ordinal) && data.Length > 2)
            data = data[2..];

        var bytes = HexBytes(data);
        var text = new string(Encoding.ASCII.GetString(bytes)
            .Where(c => !char.IsControl(c) && c >= 32 && c <= 126)
            .ToArray()).Trim();

        if (maxLength > 0 && text.Length > maxLength) text = text[..maxLength];
        return text;
    }

    private static byte[] HexBytes(string hex)
    {
        var bytes = new List<byte>();
        for (var i = 0; i + 1 < hex.Length; i += 2)
            if (byte.TryParse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                bytes.Add(b);
        return bytes.ToArray();
    }

    private static string GroupHex(string value, int group)
    {
        var parts = new List<string>();
        for (var i = 0; i < value.Length; i += group)
            parts.Add(value.Substring(i, Math.Min(group, value.Length - i)));
        return string.Join(" ", parts);
    }

    private static string FormatSeconds(int seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}ч {t.Minutes}м" : $"{t.Minutes}м {t.Seconds}с";
    }

    private static async Task<string> SafeAsync(Func<Task<string>> action, string fallback)
    {
        try { return await action(); } catch { return fallback; }
    }
}