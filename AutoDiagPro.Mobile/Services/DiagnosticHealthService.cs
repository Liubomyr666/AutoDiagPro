using System.Globalization;

namespace AutoDiagPro.Mobile.Services;

public sealed record DiagnosticHealthResult(int Score, string Level, string Summary, IReadOnlyList<string> Findings);

public static class DiagnosticHealthService
{
    public static DiagnosticHealthResult Analyze(
        string? voltageText,
        IReadOnlyCollection<string> dtc,
        IReadOnlyDictionary<string, string> readiness,
        IReadOnlyDictionary<string, string> live)
    {
        var score = 100;
        var findings = new List<string>();

        if (dtc.Count > 0)
        {
            var penalty = Math.Min(45, dtc.Count * 10);
            score -= penalty;
            findings.Add($"Найдено DTC: {dtc.Count}. Сначала подтвердите причины кодов.");
        }
        else findings.Add("Активные OBD-II DTC не найдены.");

        var voltage = ParseNumber(voltageText);
        if (voltage is > 0 and < 11.8)
        {
            score -= 25;
            findings.Add($"Низкое напряжение: {voltage:0.0} V.");
        }
        else if (voltage is >= 11.8 and < 12.2)
        {
            score -= 12;
            findings.Add($"Напряжение АКБ снижено: {voltage:0.0} V.");
        }
        else if (voltage > 15.0)
        {
            score -= 15;
            findings.Add($"Повышенное напряжение: {voltage:0.0} V. Проверьте зарядку.");
        }
        else if (voltage > 0)
            findings.Add($"Напряжение: {voltage:0.0} V.");

        var notReady = readiness.Count(x =>
            x.Value.Contains("not", StringComparison.OrdinalIgnoreCase) ||
            x.Value.Contains("не готов", StringComparison.OrdinalIgnoreCase) ||
            x.Value.Contains("incomplete", StringComparison.OrdinalIgnoreCase));
        if (notReady > 0)
        {
            score -= Math.Min(15, notReady * 3);
            findings.Add($"Readiness не завершён: {notReady} монитор(ов).");
        }

        var coolant = FindNumber(live, "coolant", "ОЖ", "температура");
        if (coolant is > 110)
        {
            score -= 20;
            findings.Add($"Высокая температура ОЖ: {coolant:0} °C.");
        }

        score = Math.Clamp(score, 0, 100);
        var level = score >= 90 ? "Хорошо" : score >= 70 ? "Требует внимания" : score >= 45 ? "Нужна диагностика" : "Критично";
        return new(score, level, $"Состояние: {score}/100 • {level}", findings);
    }

    private static double ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var cleaned = new string(text.Replace(',', '.').Where(c => char.IsDigit(c) || c is '.' or '-').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static double FindNumber(IReadOnlyDictionary<string, string> data, params string[] terms)
    {
        foreach (var pair in data)
            if (terms.Any(t => pair.Key.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return ParseNumber(pair.Value);
        return 0;
    }
}
