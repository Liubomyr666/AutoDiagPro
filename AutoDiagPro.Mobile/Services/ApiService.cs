using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoDiagPro.Mobile.Models;

namespace AutoDiagPro.Mobile.Services;

public sealed class ApiService
{
    public const string BaseUrl = "https://api-autodiagpro.duckdns.org/";
    private readonly HttpClient _http = new() { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(25) };
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly SessionStore _store;

    public ServerSession? Session { get; private set; }
    public bool IsLoggedIn => Session is not null && !string.IsNullOrWhiteSpace(Session.AccessToken);

    public ApiService(SessionStore store) => _store = store;

    public async Task<ServerHealth> GetHealthAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("health", ct);
        return await ReadAsync<ServerHealth>(response, "Проверка сервера", ct);
    }

    public async Task<ExternalLoginStartResponse> StartExternalLoginAsync(string provider, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/external/start", new
        {
            provider = provider.Trim().ToLowerInvariant()
        }, ct);

        return await ReadAsync<ExternalLoginStartResponse>(response, "Внешний вход", ct);
    }

    public async Task<ExternalLoginStatusResponse> GetExternalLoginStatusAsync(Guid requestId, bool remember, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"api/auth/external/status/{requestId}", ct);
        var result = await ReadAsync<ExternalLoginStatusResponse>(response, "Внешний вход", ct);

        if (string.Equals(result.Status, "complete", StringComparison.OrdinalIgnoreCase) && result.Session is not null)
        {
            Session = result.Session;
            if (remember) await _store.SaveAsync(Session);
            else _store.Clear();
        }

        return result;
    }

    public async Task<ServerSession> LoginAsync(string email, string password, bool remember, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/login", new
        {
            email = email.Trim(),
            password
        }, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Неверный email или пароль.");

        Session = await ReadAsync<ServerSession>(response, "Вход", ct);
        if (remember) await _store.SaveAsync(Session);
        else _store.Clear();
        return Session;
    }

    public async Task<bool> TryRestoreAsync(CancellationToken ct = default)
    {
        Session = await _store.LoadAsync();
        if (Session is null || string.IsNullOrWhiteSpace(Session.RefreshToken))
            return false;

        if (await TryRefreshAsync(ct)) return true;
        Session = null;
        _store.Clear();
        return false;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (Session is not null)
        {
            try
            {
                using var request = Authorized(HttpMethod.Post, "api/auth/logout");
                using var _ = await _http.SendAsync(request, ct);
            }
            catch { }
        }

        Session = null;
        _store.Clear();
    }

    public Task<List<ServerVehicleRecord>> GetVehiclesAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerVehicleRecord>>("api/vehicles", ct);

    public Task<List<ServerScanRecord>> GetScansAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerScanRecord>>("api/scans", ct);

    public Task<List<ServerWorkOrderRecord>> GetWorkOrdersAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerWorkOrderRecord>>("api/work-orders", ct);

    public Task<ServerWorkshopSearchResult> SearchWorkshopAsync(string query, CancellationToken ct = default) =>
        GetAuthorizedAsync<ServerWorkshopSearchResult>($"api/workshop/search?q={Uri.EscapeDataString(query)}", ct);

    public Task<List<ServerServiceIntakeRecord>> GetIntakesAsync(Guid? vehicleId = null, CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerServiceIntakeRecord>>(vehicleId is null ? "api/intakes" : $"api/intakes?vehicleId={vehicleId}", ct);

    public async Task<Guid> CreateIntakeAsync(Guid vehicleId, Guid? workOrderId, long? mileageKm, int? fuelPercent, string complaint, string damageNotes, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/intakes", new
        {
            vehicleId, workOrderId, mileageKm, fuelPercent, complaint, damageNotes
        }, ct);
        return created.Id;
    }

    public Task UpdateIntakeAsync(Guid id, long? mileageKm, int? fuelPercent, string? complaint, string? damageNotes, string? status, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/intakes/{id}", new
        {
            mileageKm, fuelPercent, complaint, damageNotes, status
        }, ct);

    public Task<List<ServerPhotoRecord>> GetPhotosAsync(Guid? vehicleId = null, Guid? workOrderId = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (vehicleId is not null) query.Add($"vehicleId={vehicleId}");
        if (workOrderId is not null) query.Add($"workOrderId={workOrderId}");
        var path = "api/photos" + (query.Count == 0 ? "" : "?" + string.Join("&", query));
        return GetAuthorizedAsync<List<ServerPhotoRecord>>(path, ct);
    }

    public async Task<Guid> UploadPhotoAsync(Guid vehicleId, Guid? workOrderId, string kind, string caption, string mimeType, byte[] data, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/photos", new
        {
            vehicleId, workOrderId, kind, caption, mimeType,
            base64Data = Convert.ToBase64String(data)
        }, ct);
        return created.Id;
    }

    public Task<List<ServerMaintenanceRecord>> GetMaintenanceAsync(Guid vehicleId, CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerMaintenanceRecord>>($"api/maintenance?vehicleId={vehicleId}", ct);

    public async Task<Guid> CreateMaintenanceAsync(Guid vehicleId, string name, long? dueMileage, DateOnly? dueDate, long? lastDoneMileage, DateTimeOffset? lastDoneAt, string? notes, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/maintenance", new
        {
            vehicleId, name, dueMileage, dueDate, lastDoneMileage, lastDoneAt, notes
        }, ct);
        return created.Id;
    }

    public Task UpdateMaintenanceAsync(Guid id, string? name = null, long? dueMileage = null, DateOnly? dueDate = null, long? lastDoneMileage = null, DateTimeOffset? lastDoneAt = null, string? notes = null, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/maintenance/{id}", new
        {
            name, dueMileage, dueDate, lastDoneMileage, lastDoneAt, notes
        }, ct);

    public Task<List<ServerInstalledPartRecord>> GetInstalledPartsAsync(Guid vehicleId, CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerInstalledPartRecord>>($"api/installed-parts?vehicleId={vehicleId}", ct);

    public async Task<Guid> AddInstalledPartAsync(Guid vehicleId, Guid? workOrderId, string name, string? partNumber, string? manufacturer, decimal? purchasePrice, decimal? customerPrice, long? installedMileage, DateTimeOffset? installedAt, DateOnly? warrantyUntil, string? mechanicName, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/installed-parts", new
        {
            vehicleId, workOrderId, name, partNumber, manufacturer, purchasePrice, customerPrice,
            installedMileage, installedAt, warrantyUntil, mechanicName
        }, ct);
        return created.Id;
    }

    public Task<List<ServerAppointmentRecord>> GetAppointmentsAsync(Guid? vehicleId = null, CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerAppointmentRecord>>(vehicleId is null ? "api/appointments" : $"api/appointments?vehicleId={vehicleId}", ct);

    public async Task<Guid> CreateAppointmentAsync(Guid? vehicleId, string clientName, DateTimeOffset startsAt, string work, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/appointments", new
        {
            vehicleId, clientName, startsAt, work
        }, ct);
        return created.Id;
    }

    public Task<List<ServerInvoiceRecord>> GetInvoicesAsync(Guid? vehicleId = null, CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerInvoiceRecord>>(vehicleId is null ? "api/invoices" : $"api/invoices?vehicleId={vehicleId}", ct);

    public Task<ServerDashboardRecord> GetDashboardAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<ServerDashboardRecord>("api/dashboard", ct);

    public async Task<Guid> CreateWorkOrderAsync(Guid vehicleId, string title, decimal? totalAmount = null, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/work-orders", new
        {
            vehicleId, title, totalAmount
        }, ct);
        return created.Id;
    }

    public Task UpdateWorkOrderAsync(Guid id, string? status = null, string? title = null, decimal? totalAmount = null, string? estimateStatus = null, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/work-orders/{id}", new
        {
            status, title, totalAmount, estimateStatus
        }, ct);

    public Task DecideWorkOrderAsync(Guid id, bool approved, string? note = null, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(HttpMethod.Post, $"api/work-orders/{id}/decision", new
        {
            decision = approved ? "Approved" : "Rejected",
            note
        }, ct);

    public async Task<byte[]> GetPhotoBytesAsync(Guid photoId, CancellationToken ct = default)
    {
        await EnsureSessionAsync(ct);
        using var response = await SendWithRefreshAsync(() => Authorized(HttpMethod.Get, $"api/photos/{photoId}/content"), ct);
        if (!response.IsSuccessStatusCode)
            _ = await ReadAsync<JsonElement>(response, "Фото", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public Task<JsonElement> GetVehicleContextAsync(Guid vehicleId, CancellationToken ct = default) =>
        GetAuthorizedAsync<JsonElement>($"api/vehicles/{vehicleId}/context", ct);

    public Task<JsonElement> DecodeVinAsync(string vin, CancellationToken ct = default) =>
        GetAuthorizedAsync<JsonElement>($"api/vin/decode/{Uri.EscapeDataString(vin)}", ct);

    public Task<List<ServerWorkshopClientRecord>> GetClientsAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerWorkshopClientRecord>>("api/clients", ct);

    public async Task<Guid> CreateClientAsync(string fullName, string? phone, string? email, string? notes, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/clients", new
        {
            fullName, phone, email, notes
        }, ct);
        return created.Id;
    }

    public Task UpdateAppointmentAsync(Guid id, string? status = null, DateTimeOffset? startsAt = null, string? work = null, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/appointments/{id}", new
        {
            status, startsAt, work
        }, ct);

    public async Task<Guid> CreateInvoiceAsync(Guid? vehicleId, Guid? workOrderId, string? number, string? description, decimal amount, bool paid, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/invoices", new
        {
            vehicleId, workOrderId, number, description, amount, paid
        }, ct);
        return created.Id;
    }

    public Task UpdateInvoiceAsync(Guid id, string? description = null, decimal? amount = null, bool? paid = null, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/invoices/{id}", new
        {
            description, amount, paid
        }, ct);


    public Task<List<ServerWearCheckRecord>> GetWearChecksAsync(Guid vehicleId, CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerWearCheckRecord>>($"api/wear?vehicleId={vehicleId}", ct);

    public async Task<Guid> CreateWearCheckAsync(
        Guid vehicleId,
        double? tireFrontMm,
        double? tireRearMm,
        double? padFrontMm,
        double? padRearMm,
        string? notes,
        CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/wear", new
        {
            vehicleId,
            tireFrontMm,
            tireRearMm,
            padFrontMm,
            padRearMm,
            notes
        }, ct);
        return created.Id;
    }

    public Task<List<ServerInventoryRecord>> GetInventoryAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerInventoryRecord>>("api/inventory", ct);

    public async Task<Guid> ReceiveInventoryAsync(
        Guid? vehicleId,
        string code,
        string name,
        int quantity,
        string? notes = null,
        CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/inventory/receive", new
        {
            vehicleId,
            code,
            name,
            quantity,
            notes
        }, ct);
        return created.Id;
    }

    public Task UpdateInventoryQuantityAsync(Guid id, int quantity, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/inventory/{id}", new
        {
            quantity
        }, ct);

    public Task<List<ServerUserRecord>> GetUsersAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerUserRecord>>("api/admin/users", ct);

    public Task<List<ServerTenantRecord>> GetTenantsAsync(CancellationToken ct = default) =>
        GetAuthorizedAsync<List<ServerTenantRecord>>("api/platform/tenants", ct);

    public async Task<Guid> CreateVehicleAsync(ServerVehicleCreate request, CancellationToken ct = default)
    {
        var created = await PostAuthorizedAsync<CreatedIdResponse>("api/vehicles", request, ct);
        return created.Id;
    }

    public Task<ServerAiResponse> AskAiAsync(string question, string vehicleContext, bool webSearch, CancellationToken ct = default) =>
        PostAuthorizedAsync<ServerAiResponse>("api/ai/ask", new
        {
            question,
            vehicleContext,
            model = "Auto",
            webSearch
        }, ct);

    public async Task UploadScanAsync(Guid vehicleId, string? vin, string adapter, string protocol, int dtcCount, string summary, CancellationToken ct = default)
    {
        await PostAuthorizedAsync<CreatedIdResponse>("api/scans", new
        {
            vehicleId,
            vin,
            adapter,
            protocol,
            dtcCount,
            summary
        }, ct);
    }

    public Task<ServerUserCreated> CreateUserAsync(ServerUserCreate request, CancellationToken ct = default) =>
        PostAuthorizedAsync<ServerUserCreated>("api/admin/users", request, ct);

    public Task UpdateUserAsync(Guid id, ServerUserUpdate request, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/admin/users/{id}", request, ct);

    public Task<ServerPasswordReset> ResetUserPasswordAsync(Guid id, CancellationToken ct = default) =>
        PostAuthorizedAsync<ServerPasswordReset>($"api/admin/users/{id}/reset-password", new { }, ct);

    public Task<ServerTenantCreated> CreateTenantAsync(ServerTenantCreate request, CancellationToken ct = default) =>
        PostAuthorizedAsync<ServerTenantCreated>("api/platform/tenants", request, ct);

    public Task SetTenantActiveAsync(Guid id, bool active, CancellationToken ct = default) =>
        SendAuthorizedNoContentAsync(new HttpMethod("PATCH"), $"api/platform/tenants/{id}", new { isActive = active }, ct);

    private async Task<T> GetAuthorizedAsync<T>(string path, CancellationToken ct)
    {
        await EnsureSessionAsync(ct);
        using var response = await SendWithRefreshAsync(() => Authorized(HttpMethod.Get, path), ct);
        return await ReadAsync<T>(response, "AutoDiag Server", ct);
    }

    private async Task<T> PostAuthorizedAsync<T>(string path, object payload, CancellationToken ct)
    {
        await EnsureSessionAsync(ct);
        using var response = await SendWithRefreshAsync(() =>
        {
            var request = Authorized(HttpMethod.Post, path);
            request.Content = JsonContent.Create(payload);
            return request;
        }, ct);

        return await ReadAsync<T>(response, "AutoDiag Server", ct);
    }

    private async Task SendAuthorizedNoContentAsync(HttpMethod method, string path, object payload, CancellationToken ct)
    {
        await EnsureSessionAsync(ct);
        using var response = await SendWithRefreshAsync(() =>
        {
            var request = Authorized(method, path);
            request.Content = JsonContent.Create(payload);
            return request;
        }, ct);

        if (!response.IsSuccessStatusCode)
            _ = await ReadAsync<JsonElement>(response, "AutoDiag Server", ct);
    }

    private HttpRequestMessage Authorized(HttpMethod method, string path)
    {
        if (Session is null) throw new InvalidOperationException("Сессия не активна.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.AccessToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        using var first = requestFactory();
        var response = await _http.SendAsync(first, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        response.Dispose();

        if (!await TryRefreshAsync(ct)) throw new InvalidOperationException("Сессия истекла. Войдите заново.");
        using var second = requestFactory();
        return await _http.SendAsync(second, ct);
    }

    private async Task EnsureSessionAsync(CancellationToken ct)
    {
        if (Session is null) throw new InvalidOperationException("Сессия не активна.");
        if (Session.AccessExpiresAtUtc <= DateTimeOffset.UtcNow.AddSeconds(30) && !await TryRefreshAsync(ct))
            throw new InvalidOperationException("Сессия истекла. Войдите заново.");
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        if (Session is null || string.IsNullOrWhiteSpace(Session.RefreshToken)) return false;

        try
        {
            using var response = await _http.PostAsJsonAsync("api/auth/refresh", new
            {
                refreshToken = Session.RefreshToken
            }, ct);

            if (!response.IsSuccessStatusCode) return false;
            Session = await response.Content.ReadFromJsonAsync<ServerSession>(_json, ct);
            if (Session is null) return false;
            await _store.SaveAsync(Session);
            return true;
        }
        catch { return false; }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, string action, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            string? detail = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var d)) detail = d.GetString();
                else if (doc.RootElement.TryGetProperty("error", out var e)) detail = e.GetString();
                else if (doc.RootElement.TryGetProperty("title", out var t)) detail = t.GetString();
            }
            catch { }

            throw new InvalidOperationException($"{action}: {detail ?? body} (HTTP {(int)response.StatusCode})");
        }

        if (typeof(T) == typeof(JsonElement) && string.IsNullOrWhiteSpace(body))
            return (T)(object)default(JsonElement);

        return JsonSerializer.Deserialize<T>(body, _json)
               ?? throw new InvalidOperationException($"{action}: пустой ответ.");
    }
}