using AutoDiagPro.Mobile.Models;
using System.Text.Json;

namespace AutoDiagPro.Mobile.Services;

public sealed record VehicleVisualInfo(
    string PhotoUrl,
    string ColorName,
    string PaintCode,
    string ColorSource);

public sealed class VehicleVisualService
{
    private static readonly HttpClient Http = CreateHttp();
    private readonly ApiService _api;

    public VehicleVisualService(ApiService api) => _api = api;

    public async Task<VehicleVisualInfo> ResolveAsync(ServerVehicleRecord vehicle, CancellationToken ct = default)
    {
        var key = VehicleKey(vehicle);
        var color = Preferences.Default.Get(key + "_color", "");
        var paint = Preferences.Default.Get(key + "_paint", "");
        var source = Preferences.Default.Get(key + "_source", "");

        if ((string.IsNullOrWhiteSpace(color) || string.IsNullOrWhiteSpace(paint)) &&
            !string.IsNullOrWhiteSpace(vehicle.Vin))
        {
            try
            {
                var data = await _api.DecodeVinRawAsync(vehicle.Vin);
                color = First(data, color,
                    "exteriorColor", "exteriorColorName", "paintColor", "color", "bodyColor", "colour");
                paint = First(data, paint,
                    "paintCode", "exteriorColorCode", "colorCode", "paintColourCode");

                if (!string.IsNullOrWhiteSpace(color) || !string.IsNullOrWhiteSpace(paint))
                {
                    source = "VIN / OEM";
                    SaveColor(vehicle, color, paint, source);
                }
            }
            catch
            {
                // Body colour is not a standard OBD-II PID and often is absent from VIN providers.
            }
        }

        var photoUrl = Preferences.Default.Get(key + "_photo", "");
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            var modelQuery = BuildPhotoQuery(vehicle, color);
            photoUrl = await ResolvePhotoUrlAsync(modelQuery, ct) ?? "";

            if (string.IsNullOrWhiteSpace(photoUrl) && !string.IsNullOrWhiteSpace(color))
                photoUrl = await ResolvePhotoUrlAsync(BuildPhotoQuery(vehicle, ""), ct) ?? "";

            if (!string.IsNullOrWhiteSpace(photoUrl))
                Preferences.Default.Set(key + "_photo", photoUrl);
        }

        return new VehicleVisualInfo(photoUrl, color, paint, source);
    }

    public void SaveColor(ServerVehicleRecord vehicle, string? colorName, string? paintCode, string source = "Подтверждено пользователем")
    {
        var key = VehicleKey(vehicle);
        Preferences.Default.Set(key + "_color", (colorName ?? "").Trim());
        Preferences.Default.Set(key + "_paint", (paintCode ?? "").Trim());
        Preferences.Default.Set(key + "_source", source);
        // Colour can affect the photo query, so re-resolve on the next refresh.
        Preferences.Default.Remove(key + "_photo");
    }

    public static Color Swatch(string? colorName)
    {
        var value = (colorName ?? "").Trim().ToLowerInvariant();
        if (value.StartsWith("#") && (value.Length == 7 || value.Length == 9))
        {
            try { return Color.FromArgb(value); } catch { }
        }

        if (value.Contains("black") || value.Contains("schwarz") || value.Contains("чёр") || value.Contains("черн"))
            return Color.FromArgb("#17191C");
        if (value.Contains("white") || value.Contains("weiß") || value.Contains("weiss") || value.Contains("бел"))
            return Color.FromArgb("#ECEFF2");
        if (value.Contains("silver") || value.Contains("silber") || value.Contains("сереб"))
            return Color.FromArgb("#B9C1C8");
        if (value.Contains("gray") || value.Contains("grey") || value.Contains("grau") || value.Contains("сер") || value.Contains("графит"))
            return Color.FromArgb("#66717B");
        if (value.Contains("red") || value.Contains("rot") || value.Contains("крас"))
            return Color.FromArgb("#C92F3A");
        if (value.Contains("blue") || value.Contains("blau") || value.Contains("син") || value.Contains("голуб"))
            return Color.FromArgb("#2B66B1");
        if (value.Contains("green") || value.Contains("grün") || value.Contains("gruen") || value.Contains("зел"))
            return Color.FromArgb("#3B7155");
        if (value.Contains("yellow") || value.Contains("gelb") || value.Contains("желт"))
            return Color.FromArgb("#D8B62C");
        if (value.Contains("orange") || value.Contains("оранж"))
            return Color.FromArgb("#D56A2E");
        if (value.Contains("brown") || value.Contains("braun") || value.Contains("корич"))
            return Color.FromArgb("#73513D");
        if (value.Contains("beige") || value.Contains("беж"))
            return Color.FromArgb("#B7A98B");

        return Theme.Line;
    }

    private static string First(JsonElement data, string current, params string[] names)
    {
        if (!string.IsNullOrWhiteSpace(current))
            return current;

        foreach (var name in names)
        {
            if (!data.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                continue;

            var text = value.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text) ||
                text.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Not Applicable", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Not Available", StringComparison.OrdinalIgnoreCase))
                continue;

            return text;
        }

        return "";
    }

    private static string VehicleKey(ServerVehicleRecord vehicle)
    {
        var source = !string.IsNullOrWhiteSpace(vehicle.Vin)
            ? vehicle.Vin!
            : vehicle.Id.ToString("N");
        var safe = new string(source.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return "vehicle_visual_" + safe;
    }

    private static string BuildPhotoQuery(ServerVehicleRecord vehicle, string color)
    {
        var parts = new[]
        {
            vehicle.Make ?? "",
            vehicle.Model ?? "",
            vehicle.Year?.ToString() ?? "",
            color,
            "car"
        };
        return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

    private static async Task<string?> ResolvePhotoUrlAsync(string search, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(search))
            return null;

        try
        {
            var api =
                "https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrnamespace=6&gsrlimit=4" +
                "&gsrsearch=" + Uri.EscapeDataString(search) +
                "&prop=imageinfo&iiprop=url&iiurlwidth=1200&format=json&origin=*";

            using var response = await Http.GetAsync(api, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!document.RootElement.TryGetProperty("query", out var query) ||
                !query.TryGetProperty("pages", out var pages))
                return null;

            foreach (var page in pages.EnumerateObject())
            {
                if (!page.Value.TryGetProperty("imageinfo", out var infos) || infos.GetArrayLength() == 0)
                    continue;

                var info = infos[0];
                if (info.TryGetProperty("thumburl", out var thumb) && Uri.TryCreate(thumb.GetString(), UriKind.Absolute, out _))
                    return thumb.GetString();
                if (info.TryGetProperty("url", out var original) && Uri.TryCreate(original.GetString(), UriKind.Absolute, out _))
                    return original.GetString();
            }
        }
        catch
        {
            // The app keeps its bundled fallback image when network photo lookup is unavailable.
        }

        return null;
    }

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AutoDiagPro-iOS/3.28");
        return client;
    }
}
