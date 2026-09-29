using System.Net.Http.Json;

namespace AutoDiagPro.Mobile.Services;

public sealed class MobileReleaseService
{
    public const string ManifestUrl = "https://api-autodiagpro.duckdns.org/api/releases/current";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<MobileReleaseCheck> CheckAsync(CancellationToken ct = default)
    {
        var manifest = await _http.GetFromJsonAsync<MobileReleaseManifest>(ManifestUrl, ct)
                       ?? throw new InvalidOperationException("Сервер не вернул информацию о релизе.");

        var currentVersion = ParseVersion(AppInfo.Current.VersionString);
        var latestVersion = ParseVersion(manifest.Ios.Version);
        _ = int.TryParse(AppInfo.Current.BuildString, out var currentBuild);

        var newer = latestVersion > currentVersion ||
                    (latestVersion == currentVersion && manifest.Ios.Build > currentBuild);

        return new MobileReleaseCheck(
            AppInfo.Current.VersionString,
            currentBuild,
            manifest.Ios.Version,
            manifest.Ios.Build,
            newer,
            manifest.Ios.Distribution,
            manifest.Ios.Notes,
            manifest.Server);
    }

    private static Version ParseVersion(string? value) =>
        Version.TryParse((value ?? "0.0.0").Trim(), out var version)
            ? version
            : new Version(0, 0, 0);
}

public sealed class MobileReleaseManifest
{
    public MobileWindowsRelease Windows { get; set; } = new();
    public MobileIosRelease Ios { get; set; } = new();
    public string Server { get; set; } = "";
}

public sealed class MobileWindowsRelease
{
    public string Version { get; set; } = "";
}

public sealed class MobileIosRelease
{
    public string Version { get; set; } = "";
    public int Build { get; set; }
    public string Distribution { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed record MobileReleaseCheck(
    string CurrentVersion,
    int CurrentBuild,
    string LatestVersion,
    int LatestBuild,
    bool UpdateAvailable,
    string Distribution,
    string Notes,
    string ServerVersion);
