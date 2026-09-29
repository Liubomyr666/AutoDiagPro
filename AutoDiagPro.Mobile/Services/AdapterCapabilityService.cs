namespace AutoDiagPro.Mobile.Services;

public sealed record MobileAdapterCapabilityItem(
    string Name,
    string Status,
    string Detail);

public sealed record MobileAdapterCapabilityReport(
    string AdapterFamily,
    string Transport,
    string Protocol,
    IReadOnlyList<MobileAdapterCapabilityItem> Items)
{
    public string ToDisplayText()
    {
        var lines = new List<string>
        {
            $"Адаптер: {AdapterFamily}",
            $"Транспорт: {Transport}",
            $"Протокол: {Protocol}",
            ""
        };

        foreach (var item in Items)
        {
            lines.Add($"{item.Status}  {item.Name}");
            lines.Add(item.Detail);
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines).TrimEnd();
    }
}

public static class MobileAdapterCapabilityService
{
    public static MobileAdapterCapabilityReport Evaluate(
        string? adapterId,
        string? transport,
        string? protocol,
        string? brand)
    {
        var family = DetectFamily(adapterId, transport);
        var tx = string.IsNullOrWhiteSpace(transport) ? "—" : transport.Trim();
        var proto = string.IsNullOrWhiteSpace(protocol) ? "—" : protocol.Trim();
        var connected = !string.IsNullOrWhiteSpace(transport) && transport != "—";

        var items = new List<MobileAdapterCapabilityItem>
        {
            Item("OBD-II диагностика",
                connected ? "✓ ДОСТУПНО" : "○ НЕТ ПОДКЛЮЧЕНИЯ",
                connected
                    ? "DTC, VIN, readiness и стандартные OBD-II данные доступны через текущий адаптер."
                    : "Сначала подключите BLE или Wi-Fi OBD-адаптер."),

            Item("Live Data / PID",
                connected ? "✓ ДОСТУПНО" : "○ НЕТ ПОДКЛЮЧЕНИЯ",
                connected
                    ? "AutoDiag показывает только PID, которые реально отвечает ECU."
                    : "Поддерживаемые PID проверяются после подключения."),

            Item("ECU / Calibration ID / CVN",
                connected ? "✓ READ-ONLY" : "○ НЕТ ПОДКЛЮЧЕНИЯ",
                connected
                    ? "Чтение идентификаторов безопасно и не меняет настройки ECU."
                    : "Нужно подключение к автомобилю."),

            Item("Марочная диагностика",
                BrandStatus(brand),
                BrandDetail(brand, family)),

            Item("Кодирование / адаптации",
                "🔒 НУЖЕН МАРОЧНЫЙ ИНТЕРФЕЙС",
                "iPhone BLE/Wi-Fi ELM/Vgate используется для диагностики. Coding/adaptation не разблокируется только по факту подключения ELM."),

            Item("Сервисные функции",
                "△ ЗАВИСИТ ОТ ПРОТОКОЛА",
                "Service reset, EPB, DPF и другие функции требуют проверенного марочного протокола для конкретной машины."),

            Item("ECU/TCU Flash",
                "🔒 НЕДОСТУПНО ЧЕРЕЗ ELM",
                "Для реальной записи нужен совместимый J2534/DoIP/ENET/bench/OEM интерфейс и точный профиль ECU HW/SW. Обычный Vgate/ELM для flash не используется."),

            Item("Battery Coding",
                "△ ПО МАРКЕ / VIN",
                "AutoDiag может подготовить профиль АКБ, но фактическая регистрация зависит от марки, ECU и поддерживаемого марочного интерфейса.")
        };

        return new MobileAdapterCapabilityReport(family, tx, proto, items);
    }

    public static string DetectFamily(string? adapterId, string? transport)
    {
        var text = $"{adapterId} {transport}".ToLowerInvariant();

        if (text.Contains("obdlink")) return "OBDLink / STN";
        if (text.Contains("vlink") || text.Contains("vlinker")) return "vLinker / Vgate";
        if (text.Contains("vgate") || text.Contains("icar")) return "Vgate iCar family";
        if (text.Contains("carista")) return "Carista-compatible";
        if (text.Contains("elm327") || text.Contains("elm 327")) return "ELM327";
        if (text.Contains("bluetooth") || text.Contains("ble")) return "BLE OBD / ELM";
        if (text.Contains("wi-fi") || text.Contains("wifi")) return "Wi-Fi OBD / ELM";

        return string.IsNullOrWhiteSpace(adapterId) ? "Не определён" : adapterId.Trim();
    }

    private static MobileAdapterCapabilityItem Item(string name, string status, string detail) =>
        new(name, status, detail);

    private static string BrandStatus(string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand)) return "△ БАЗОВЫЙ OBD-II";
        return brand is "Volkswagen" or "Audi" or "Škoda" or "SEAT" or "CUPRA" or "Porsche"
            ? "△ ENGINE UDS / OBD-II"
            : "△ ОГРАНИЧЕНО";
    }

    private static string BrandDetail(string? brand, string family)
    {
        if (string.IsNullOrWhiteSpace(brand))
            return "Марка не определена. Доступны стандартные OBD-II функции.";

        if (brand is "Volkswagen" or "Audi" or "Škoda" or "SEAT" or "CUPRA" or "Porsche")
            return $"{brand}: {family} подходит для стандартного OBD-II и поддержанных read-only идентификаторов Engine ECU. Для Gateway/ABS/Airbag/DSG нужен марочный интерфейс.";

        return $"{brand}: через {family} доступны стандартные OBD-II функции. Остальные блоки требуют марочного транспорта.";
    }
}
