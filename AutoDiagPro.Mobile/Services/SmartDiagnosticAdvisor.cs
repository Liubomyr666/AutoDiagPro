using System.Globalization;

namespace AutoDiagPro.Mobile.Services;

public sealed record SmartDiagnosticSummary(
    string Priority,
    string Status,
    IReadOnlyList<string> NextSteps);

public static class SmartDiagnosticAdvisor
{
    public static SmartDiagnosticSummary Analyze(
        string? voltageText,
        IReadOnlyCollection<string> dtcCodes,
        IReadOnlyDictionary<string, string> readiness,
        IReadOnlyDictionary<string, string> live,
        IReadOnlyDictionary<string, string> freezeFrame,
        DiagnosticHealthResult health)
    {
        var steps = new List<string>();
        var voltage = ParseNumber(voltageText);
        var coolant = FindNumber(live, "coolant", "ОЖ", "температура");
        var milOn = readiness.Any(x =>
            x.Key.Contains("MIL", StringComparison.OrdinalIgnoreCase) &&
            (x.Value.Contains("вкл", StringComparison.OrdinalIgnoreCase) ||
             x.Value.Contains("on", StringComparison.OrdinalIgnoreCase)));
        var notReady = readiness.Any(x =>
            x.Value.Contains("not", StringComparison.OrdinalIgnoreCase) ||
            x.Value.Contains("не готов", StringComparison.OrdinalIgnoreCase) ||
            x.Value.Contains("incomplete", StringComparison.OrdinalIgnoreCase));
        string priority;
        string status;

        if (coolant > 110)
        {
            priority = "ВЫСОКИЙ";
            status = $"Температура ОЖ {coolant:0} °C — сначала проверить охлаждение.";
            steps.Add("Не продолжать нагрузочный тест до проверки уровня ОЖ, утечек и вентилятора.");
        }
        else if (voltage is > 0 and < 11.8)
        {
            priority = "ВЫСОКИЙ";
            status = $"Низкое питание {voltage:0.0} V может искажать результаты.";
            steps.Add("Проверить АКБ, клеммы и питание; затем повторить полный скан.");
        }
        else if (milOn && dtcCodes.Count > 0)
        {
            priority = "ВЫСОКИЙ";
            status = $"MIL включён, найдено кодов: {dtcCodes.Count}.";
        }
        else if (dtcCodes.Count > 0)
        {
            priority = "СРЕДНИЙ";
            status = $"Найдено кодов: {dtcCodes.Count}. Причину нужно подтвердить.";
        }
        else if (notReady)
        {
            priority = "СРЕДНИЙ";
            status = "DTC не найдены, но readiness-мониторы ещё не завершены.";
        }
        else
        {
            priority = "НИЗКИЙ";
            status = $"Стандартный OBD-II: критичных признаков не выявлено. Health {health.Score}/100.";
        }

        foreach (var code in dtcCodes.Take(4))
        {
            var suggestion = DtcRepairAdvisor.Analyze(code);
            if (suggestion.Checks.Length > 0)
                steps.Add($"{code}: {suggestion.Checks[0]}.");
            if (suggestion.Checks.Length > 1)
                steps.Add($"{code}: {suggestion.Checks[1]}.");
        }

        if (freezeFrame.Count > 0 && dtcCodes.Count > 0)
            steps.Add("Сравнить Freeze Frame с текущим Live Data при похожем режиме работы двигателя.");

        if (notReady)
            steps.Add("После устранения причин выполнить drive cycle и повторно проверить readiness.");

        if (dtcCodes.Count == 0 && !notReady)
            steps.Add("Для ABS, Airbag, коробки и кузовных блоков выполнить марочный ECU-скан.");

        if (dtcCodes.Count > 0)
            steps.Add("После подтверждённого ремонта повторить скан и сравнить stored / pending / permanent DTC.");

        if (steps.Count == 0)
            steps.Add("Сохранить этот результат как базовый для сравнения следующих сканов.");
        return new SmartDiagnosticSummary(
            priority,
            status,
            steps.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList());
    }

    private static double ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var cleaned = new string(text.Replace(',', '.')
            .Where(c => char.IsDigit(c) || c is '.' or '-').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static double FindNumber(
        IReadOnlyDictionary<string, string> data,
        params string[] terms)
    {
        foreach (var pair in data)
            if (terms.Any(t => pair.Key.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return ParseNumber(pair.Value);
        return 0;
    }
}