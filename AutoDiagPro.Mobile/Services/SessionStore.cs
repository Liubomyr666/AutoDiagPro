using System.Text.Json;
using AutoDiagPro.Mobile.Models;

namespace AutoDiagPro.Mobile.Services;

public sealed class SessionStore
{
    private const string SessionKey = "autodiag.session.v1";
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task SaveAsync(ServerSession session)
    {
        var payload = JsonSerializer.Serialize(session, _json);
        await SecureStorage.Default.SetAsync(SessionKey, payload);
    }

    public async Task<ServerSession?> LoadAsync()
    {
        try
        {
            var payload = await SecureStorage.Default.GetAsync(SessionKey);
            return string.IsNullOrWhiteSpace(payload)
                ? null
                : JsonSerializer.Deserialize<ServerSession>(payload, _json);
        }
        catch { return null; }
    }

    public void Clear() => SecureStorage.Default.Remove(SessionKey);
}