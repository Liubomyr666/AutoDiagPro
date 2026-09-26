namespace AutoDiagPro.Mobile.Services.Obd;

public interface IObdTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    string Name { get; }
    string Endpoint { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task WriteAsync(string command, CancellationToken ct = default);
    Task<string> ReadUntilPromptAsync(TimeSpan timeout, CancellationToken ct = default);
}