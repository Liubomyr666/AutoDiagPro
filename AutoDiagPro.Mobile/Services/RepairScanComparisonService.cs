using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoDiagPro.Mobile.Services;

public sealed class RepairScanSnapshotMobile
{
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;
    public string Vin { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Voltage { get; set; } = "";
    public List<string> ConfirmedDtc { get; set; } = new();
    public List<string> PendingDtc { get; set; } = new();
    public List<string> PermanentDtc { get; set; } = new();
    public Dictionary<string, string> Live { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AllDtc => ConfirmedDtc
        .Concat(PendingDtc)
        .Concat(PermanentDtc)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim().ToUpperInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

public sealed record MobileLiveChange(
    string Name,
    string Before,
    string After,
    double? Delta,
    double? Percent);
public sealed record RepairScanComparisonMobile(
    RepairScanSnapshotMobile Before,
    RepairScanSnapshotMobile After,
    IReadOnlyList<string> Resolved,
    IReadOnlyList<string> Remaining,
    IReadOnlyList<string> Added,
    IReadOnlyList<MobileLiveChange> LiveChanges)
{
    public int BeforeCount => Before.AllDtc.Count;
    public int AfterCount => After.AllDtc.Count;
}

public static class RepairScanComparisonService
{
    public static RepairScanComparisonMobile Compare(
        RepairScanSnapshotMobile before,
        RepairScanSnapshotMobile after)
    {
        var a = before.AllDtc.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = after.AllDtc.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resolved = a.Except(b, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x).ToList();
        var remaining = a.Intersect(b, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x).ToList();
        var added = b.Except(a, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x).ToList();

        var live = new List<MobileLiveChange>();
        foreach (var key in before.Live.Keys.Intersect(after.Live.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var av = before.Live[key] ?? "";
            var bv = after.Live[key] ?? "";
            if (string.Equals(av.Trim(), bv.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var aOk = TryNumber(av, out var an);
            var bOk = TryNumber(bv, out var bn);
            var delta = aOk && bOk ? bn - an : (double?)null;
            var percent = aOk && bOk && Math.Abs(an) > 0.0001
                ? (bn - an) / Math.Abs(an) * 100d
                : (double?)null;

            live.Add(new MobileLiveChange(key, av, bv, delta, percent));
        }
        live = live
            .OrderByDescending(x => x.Percent.HasValue ? Math.Abs(x.Percent.Value) : -1)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new RepairScanComparisonMobile(
            before, after, resolved, remaining, added, live);
    }

    public static string BuildSnapshotText(RepairScanSnapshotMobile scan)
    {
        var lines = new List<string>
        {
            $"Время: {scan.CapturedAt:dd.MM.yyyy HH:mm:ss}",
            $"VIN: {(string.IsNullOrWhiteSpace(scan.Vin) ? "—" : scan.Vin)}",
            $"Протокол: {(string.IsNullOrWhiteSpace(scan.Protocol) ? "—" : scan.Protocol)}",
            $"Напряжение: {(string.IsNullOrWhiteSpace(scan.Voltage) ? "—" : scan.Voltage)}",
            $"Confirmed DTC: {Codes(scan.ConfirmedDtc)}",
            $"Pending DTC: {Codes(scan.PendingDtc)}",
            $"Permanent DTC: {Codes(scan.PermanentDtc)}",
            "",
            "LIVE DATA:"
        };

        if (scan.Live.Count == 0)
            lines.Add("Нет данных");
        else
            lines.AddRange(scan.Live.Select(x => $"{x.Key}: {x.Value}"));

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildReport(
        RepairScanComparisonMobile result,
        string vehicle,
        string? complaint,
        string? cause,
        string? repair)
    {
        var lines = new List<string>
        {
            "AutoDiag Pro — ДО / ПОСЛЕ РЕМОНТА",
            "===================================",
            $"Автомобиль: {vehicle}",
            $"VIN: {Value(result.After.Vin, result.Before.Vin)}",
            $"ДО: {result.Before.CapturedAt:dd.MM.yyyy HH:mm:ss}",
            $"ПОСЛЕ: {result.After.CapturedAt:dd.MM.yyyy HH:mm:ss}",
            "",
            $"Жалоба: {Empty(complaint)}",
            $"Подтверждённая причина: {Empty(cause)}",
            $"Что сделано: {Empty(repair)}",
            "",
            $"DTC ДО: {result.BeforeCount}",
            $"DTC ПОСЛЕ: {result.AfterCount}",
            $"Исправлено: {result.Resolved.Count}",
            $"Осталось: {result.Remaining.Count}",
            $"Новых: {result.Added.Count}",
            "",
            "ИСПРАВЛЕНО / ИСЧЕЗЛО:",
            CodeLines(result.Resolved),
            "",
            "ОСТАЛОСЬ:",
            CodeLines(result.Remaining),
            "",
            "НОВЫЕ DTC:",
            CodeLines(result.Added),
            "",
            "ИЗМЕНЕНИЯ LIVE DATA:"
        };
        if (result.LiveChanges.Count == 0)
            lines.Add("Нет общих изменившихся параметров.");
        else
            foreach (var change in result.LiveChanges)
            {
                var delta = change.Delta.HasValue
                    ? $" • Δ {change.Delta.Value:+0.###;-0.###;0}"
                    : "";
                var percent = change.Percent.HasValue
                    ? $" • {change.Percent.Value:+0.##;-0.##;0}%"
                    : "";
                lines.Add($"{change.Name}: {change.Before} → {change.After}{delta}{percent}");
            }

        lines.Add("");
        lines.Add("ИТОГ:");
        lines.Add(Verdict(result));
        return string.Join(Environment.NewLine, lines);
    }

    public static string Verdict(RepairScanComparisonMobile result)
    {
        if (result.BeforeCount > 0 && result.AfterCount == 0 && result.Added.Count == 0)
            return "DTC, присутствовавшие ДО ремонта, после контрольного scan не обнаружены.";
        if (result.Added.Count > 0)
            return $"После ремонта появились новые DTC ({result.Added.Count}). Нужна повторная проверка.";
        if (result.Remaining.Count > 0)
            return $"Часть DTC осталась ({result.Remaining.Count}). Нужна дальнейшая диагностика.";
        if (result.BeforeCount == 0 && result.AfterCount == 0)
            return "В обоих scan стандартные DTC отсутствуют. Сравните Live Data и жалобу клиента.";
        return "Контрольный scan отличается от исходного. Оцените изменения в контексте условий измерения.";
    }

    private static string Codes(IEnumerable<string> values)
    {
        var list = values.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return list.Count == 0 ? "нет" : string.Join(", ", list);
    }

    private static string CodeLines(IEnumerable<string> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? "нет" : "• " + string.Join(Environment.NewLine + "• ", list);
    }
    private static string Value(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() :
        !string.IsNullOrWhiteSpace(fallback) ? fallback.Trim() : "—";

    private static string Empty(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();

    private static bool TryNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = Regex.Match(text.Replace(',', '.'), @"[-+]?\d+(?:\.\d+)?");
        return match.Success &&
               double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}