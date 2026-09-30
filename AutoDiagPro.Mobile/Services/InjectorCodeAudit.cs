using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AutoDiagPro.SharedCoding;

namespace AutoDiagPro.SharedDiagnostics;

// Manufacturer calibration codes are not standardized OBD-II PIDs.
// ReportCode is a human-transcribed OEM diagnostic report field. It is NOT a live ECU read.
public sealed record InjectorCodeInput(int Cylinder, string? MarkingCode, string? ReportCode);
public sealed record InjectorCodeResult(
    int Cylinder, string MarkingCode, string ReportCode, string Status,
    bool Match, bool Complete, string Warning);

public sealed record InjectorCodeAuditReport(
    IReadOnlyList<InjectorCodeResult> Rows, int Matching, int Mismatching,
    int Missing, int Warnings, bool AllManualPairsMatch)
{
    public string Summary => Rows.Count == 0 ? "Нет цилиндров для проверки."
        : $"По введённым данным: совпали {Matching}, различаются {Mismatching}, " +
          $"неполные {Missing}, предупреждений {Warnings}. " +
          (AllManualPairsMatch
            ? "Все ВВЕДЁННЫЕ пары совпали. Фактическая запись в ЭБУ приложением НЕ подтверждена."
            : "Проверьте несовпадения и исходный OEM-отчёт. Фактическая запись в ЭБУ приложением НЕ подтверждена.");
}

public static class InjectorCodeAudit
{
    public const string SourceNotice =
        "Коды ЭБУ ниже введены вручную из внешнего диагностического OEM-отчёта, " +
        "а не считаны AutoDiag с машины. Сверка НЕ подтверждает, что форсунки прописаны в текущем ЭБУ.";
    public const string MarkingNotice =
        "Вводите именно калибровочный/коррекционный код форсунки, а НЕ каталожный номер детали. " +
        "Для некоторых двигателей такая кодировка не применяется.";

    public static string EquipmentGuide(string? make)
    {
        var group = SharedCodingCatalog.GroupForMake(make);
        var guidance = group switch
        {
            "VAG" or "PORSCHE" => "Для реального чтения кодов проверьте поддержку конкретного ECU в ODIS/VCDS или OEM-совместимой диагностике.",
            "MERCEDES" => "Для Mercedes CDI используйте Xentry либо подтверждённое решение для точного двигателя и блока CDI.",
            "BMW" or "LUXURY" => "Проверьте точный двигатель и ECU через ISTA или документацию и совместимое OEM-оборудование.",
            "TOYOTA" => "Проверьте поддержку чтения Injector Compensation через Techstream/OEM для конкретного двигателя.",
            "FORD" => "Проверьте поддержанный марочный доступ IDS/FDRS к параметрам именно этого ECU.",
            "HYUNDAI" => "Проверьте совместимую OEM-диагностику GDS по двигателю и версии ECU.",
            "STELLANTIS" => "Марочный интерфейс зависит от бренда, модели, двигателя и защищённого шлюза.",
            "RENAULT" => "Проверьте Renault/Dacia OEM-диагностику и профиль конкретного ЭБУ.",
            "NISSAN" => "Проверьте поддержку Nissan/Infiniti CONSULT и точный двигатель.",
            "HONDA" => "Проверьте поддержку Honda/Acura HDS и конкретный ЭБУ.",
            "MAZDA" => "Проверьте OEM Mazda диагностику и документацию по данному двигателю.",
            "MITSUBISHI" => "Проверьте Mitsubishi OEM/MUT-совместимый инструмент и конкретный ЭБУ.",
            "SUBARU" => "Проверьте Subaru OEM/SSM и модельный профиль ЭБУ.",
            "VOLVO" => "Проверьте Volvo VIDA либо заводскую документацию и поддержанный ЭБУ.",
            "JLR" => "Проверьте JLR OEM диагностику, поколение блока и двигатель.",
            "GM" => "Проверьте совместимую GM OEM диагностику и поддерживаемый двигатель.",
            "EV_OEM" => "Если автомобиль полностью электрический, топливных форсунок у него нет: проверка не применяется.",
            _ => "Нужна документация изготовителя и OEM-совместимый сканер для точного двигателя/ЭБУ."
        };
        return guidance + " AutoDiag пока не реализует автоматическое OEM-чтение кодов форсунок для этой марки.";
    }

    public static string NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = value.ToUpperInvariant().Where(c =>
            c is not (' ' or '-' or '_' or '.') && !char.IsWhiteSpace(c)).ToArray();
        return new string(chars);
    }

    public static bool HasReadableShape(string code) =>
        code.Length is >= 3 and <= 48 && code.All(char.IsAsciiLetterOrDigit);

    public static InjectorCodeAuditReport Compare(IEnumerable<InjectorCodeInput> inputs)
    {
        var items = inputs.OrderBy(x => x.Cylinder).ToList();
        var markingCounts = items.Select(x => NormalizeCode(x.MarkingCode))
            .Where(HasReadableShape).GroupBy(x => x, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var reportCounts = items.Select(x => NormalizeCode(x.ReportCode))
            .Where(HasReadableShape).GroupBy(x => x, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var reportCylinder = items.Where(x => HasReadableShape(NormalizeCode(x.ReportCode)))
            .GroupBy(x => NormalizeCode(x.ReportCode), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Cylinder).ToArray(), StringComparer.Ordinal);

        var result = new List<InjectorCodeResult>();
        foreach (var item in items)
        {
            var marking = NormalizeCode(item.MarkingCode);
            var report = NormalizeCode(item.ReportCode);
            var complete = marking.Length > 0 && report.Length > 0;
            var formatOk = (!marking.Any() || HasReadableShape(marking)) &&
                           (!report.Any() || HasReadableShape(report));
            var match = complete && formatOk && string.Equals(marking, report, StringComparison.Ordinal);
            var notes = new List<string>();

            if (!formatOk)
                notes.Add("Проверьте символы/формат кода и инструкцию OEM; глобальная контрольная сумма не проверяется.");
            if (HasReadableShape(marking) && markingCounts[marking] > 1)
                notes.Add("Одинаковая маркировка введена у нескольких цилиндров.");
            if (HasReadableShape(report) && reportCounts[report] > 1)
                notes.Add("Одинаковый код из отчёта указан для нескольких цилиндров.");
            if (complete && !match && HasReadableShape(marking) &&
                reportCylinder.TryGetValue(marking, out var others))
            {
                var other = others.Where(x => x != item.Cylinder).ToArray();
                if (other.Length > 0)
                    notes.Add("Этот код в отчёте указан для другого цилиндра: " +
                        string.Join(", ", other) + ". Проверьте нумерацию цилиндров и переписывание.");
            }
            string status = !complete
                ? "Недостаточно данных"
                : !formatOk
                    ? "Проверить формат"
                    : match
                        ? "Совпало в переписанных кодах"
                        : "НЕ СОВПАДАЕТ";
            result.Add(new(item.Cylinder, marking, report, status,
                match, complete && formatOk, string.Join(" ", notes)));
        }

        var matching = result.Count(x => x.Match);
        var mismatching = result.Count(x => x.Complete && !x.Match);
        var missing = result.Count(x => !x.Complete);
        var warnings = result.Count(x => x.Warning.Length > 0);
        return new(result, matching, mismatching, missing, warnings,
            result.Count > 0 && matching == result.Count && warnings == 0);
    }

    // Only a strict "cylinder=CODE" transcription format is accepted; never parse raw OEM bytes.
    public static (IReadOnlyDictionary<int, string> Values, IReadOnlyList<string> Errors)
        ParseCylinderLines(string? text, int cylinderCount)
    {
        var found = new Dictionary<int, string>();
        var errors = new List<string>();
        var lines = (text ?? "").Split(new[] { "\r\n", "\n", "\r" },
            StringSplitOptions.None);
        for (int index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0) continue;
            var match = Regex.Match(line,
                @"^(?:(?:cylinder|cyl|zylinder|injector|цилиндр|форсунка)\s*)?[#№]?\s*(\d{1,2})\s*[:=;\t]\s*(\S[\S ]*)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var cylinder) ||
                cylinder < 1 || cylinder > cylinderCount)
            {
                errors.Add($"Строка {index + 1}: ожидается 1=КОД (до цилиндра {cylinderCount}).");
                continue;
            }
            var code = NormalizeCode(match.Groups[2].Value);
            if (!HasReadableShape(code))
            {
                errors.Add($"Строка {index + 1}: код имеет неподдерживаемые символы или длину.");
                continue;
            }
            if (!found.TryAdd(cylinder, code))
                errors.Add($"Строка {index + 1}: цилиндр {cylinder} повторяется.");
        }
        return (found, errors);
    }

    public static string BuildTextReport(string? make, string? model, int? year, string? vin,
        InjectorCodeAuditReport audit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AUTODIAG PRO — СВЕРКА КОДОВ ФОРСУНОК (РУЧНОЙ ОТЧЁТ)");
        sb.AppendLine($"UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Автомобиль: {make ?? "не указан"} {model ?? ""} {(year.HasValue && year > 0 ? year.ToString() : "")}".Trim());
        sb.AppendLine($"VIN из карточки (не подтверждён ECU): {(string.IsNullOrWhiteSpace(vin) ? "не указан" : vin)}");
        sb.AppendLine(SourceNotice);
        sb.AppendLine(MarkingNotice);
        sb.AppendLine(EquipmentGuide(make));
        sb.AppendLine();
        sb.AppendLine("Цилиндр | Код с форсунки | Код по OEM-отчёту | Результат");
        foreach (var row in audit.Rows)
        {
            sb.AppendLine($"{row.Cylinder} | {Display(row.MarkingCode)} | {Display(row.ReportCode)} | {row.Status}");
            if (!string.IsNullOrEmpty(row.Warning)) sb.AppendLine("  Внимание: " + row.Warning);
        }
        sb.AppendLine();
        sb.AppendLine(audit.Summary);
        sb.AppendLine("НЕ является подтверждением правильного присвоения кодов в ECU. " +
            "Для этого требуется чтение coding/adaptation из настоящего ECU и проверенный профиль производителя.");
        return sb.ToString();
    }

    private static string Display(string code) => code.Length > 0 ? code : "не введён";
}
