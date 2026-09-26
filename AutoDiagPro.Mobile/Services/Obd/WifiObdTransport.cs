using System.Net.Sockets;
using System.Text;

namespace AutoDiagPro.Mobile.Services.Obd;

public sealed class WifiObdTransport : IObdTransport
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    public string Host { get; set; } = "192.168.0.10";
    public int Port { get; set; } = 35000;
    public bool IsConnected => _client?.Connected == true;
    public string Name => "Wi-Fi OBD";
    public string Endpoint => $"{Host}:{Port}";

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await DisconnectAsync();
        _client = new TcpClient();
        await _client.ConnectAsync(Host, Port, ct);
        _stream = _client.GetStream();
        _stream.ReadTimeout = 3000;
        _stream.WriteTimeout = 3000;
    }
    public async Task WriteAsync(string command, CancellationToken ct = default)
    {
        if (_stream is null) throw new InvalidOperationException("OBD не подключён.");
        var data = Encoding.ASCII.GetBytes(command.Trim() + "\r");
        await _stream.WriteAsync(data, ct);
        await _stream.FlushAsync(ct);
    }

    public async Task<string> ReadUntilPromptAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        if (_stream is null) throw new InvalidOperationException("OBD не подключён.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        var buffer = new byte[512];
        var sb = new StringBuilder();
        while (!linked.IsCancellationRequested)
        {
            var read = await _stream.ReadAsync(buffer, linked.Token);
            if (read <= 0) break;
            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (sb.ToString().Contains('>')) break;
        }

        return sb.ToString();
    }
    public Task DisconnectAsync()
    {
        try { _stream?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        _stream = null;
        _client = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}