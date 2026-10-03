namespace AutoDiagPro.Mobile.Services;

public sealed record MobileServiceFunctionAvailability(
    string Id,
    string Name,
    string Category,
    string Status,
    string Detail,
    string Requirements,
    string Route,
    bool WriteReady)
{
    public string DisplayName => $"{Status}  {Name}";
}

public static class MobileServiceFunctionCatalogService
{
    private sealed record Def(
        string Id,
        string Name,
        string Category,
        string[] ModuleTerms,
        bool DieselOnly,
        string Route,
        string Note);

    private static readonly Def[] Definitions =
    {
        new("service-reset", "Сервисный интервал / ТО", "ТО",
            new[] { "Engine", "ECM", "PCM", "DME", "ME/CDI" }, false, "service",
            "План ТО доступен на iPhone; фактический ECU reset требует марочную процедуру."),
        new("epb", "EPB • сервисный режим колодок", "Тормоза",
            new[] { "EPB", "Parking Brake" }, false, "",
            "Сервисный режим электроручника зависит от конкретного тормозного ECU."),
        new("abs-bleed", "ABS/ESP • прокачка", "Тормоза",
            new[] { "ABS", "ESP", "DSC", "VSA", "EBCM" }, false, "",
            "Активная прокачка требует марочную процедуру гидроблока."),
        new("sas", "SAS / датчик угла руля", "Рулевое",
            new[] { "EPS", "Steering", "PSCM", "MDPS" }, false, "",
            "Калибровка выполняется только через проверенный OEM-профиль."),
        new("battery", "Регистрация / замена АКБ", "Электропитание",
            new[] { "Engine", "ECM", "DME", "BCM", "CEM" }, false, "programming",
            "iPhone показывает совместимость и preflight; регистрация требует поддержанный марочный backend."),
        new("dpf", "DPF • сервис / регенерация", "Двигатель",
            new[] { "Engine", "ECM", "PCM", "DDE", "CDI", "INJECTION" }, true, "",
            "Принудительная регенерация не запускается через универсальный ELM/Vgate."),
        new("adblue", "SCR / AdBlue • сервис", "Двигатель",
            new[] { "Engine", "ECM", "PCM", "DDE", "CDI", "INJECTION" }, true, "",
            "SCR-процедуры требуют конкретный ECU и OEM-команды."),
        new("injectors", "Форсунки • проверка / коды", "Топливо",
            new[] { "Engine", "ECM", "PCM", "DDE", "CDI", "INJECTION" }, false, "injectors",
            "Стандартные топливные Live Data доступны; кодирование требует марочный протокол."),
        new("throttle", "Дроссель • адаптация", "Двигатель",
            new[] { "Engine", "ECM", "PCM", "DME", "ME/CDI" }, false, "",
            "Не существует одной универсальной OBD-II команды адаптации."),
        new("transmission", "АКПП / DSG / CVT • адаптации", "Коробка",
            new[] { "Transmission", "TCM", "EGS", "DSG", "CVT", "VGS" }, false, "",
            "Сброс адаптаций разрешается только для подтверждённой TCU-процедуры."),
        new("tpms", "TPMS • обучение / сервис", "Колёса",
            new[] { "TPMS", "Tyre Pressure" }, false, "",
            "Прямая и косвенная TPMS используют разные процедуры.")
    };

    public static IReadOnlyList<MobileServiceFunctionAvailability> Evaluate(
        string? brand,
        string? fuelType,
        bool connected)
    {
        if (string.IsNullOrWhiteSpace(brand))
            return Array.Empty<MobileServiceFunctionAvailability>();

        var group = MobileEcuPlatformCatalogService.Group(brand);
        var modules = MobileEcuPlatformCatalogService.GetModules(brand);
        var diesel = (fuelType ?? "").Contains("diesel", StringComparison.OrdinalIgnoreCase) ||
                     (fuelType ?? "").Contains("диз", StringComparison.OrdinalIgnoreCase);

        var result = new List<MobileServiceFunctionAvailability>();
        foreach (var def in Definitions)
        {
            if (group == "TESLA" && def.Id is not "tpms") continue;
            if (def.DieselOnly && !diesel) continue;
            if (!Fits(def.ModuleTerms, modules)) continue;

            var routeOnly = def.Route is "service" or "injectors";
            var status = routeOnly
                ? "✓ ДОСТУПНО"
                : connected
                    ? "🔒 НУЖЕН OEM-ИНТЕРФЕЙС"
                    : "○ НЕТ ПОДКЛЮЧЕНИЯ";

            result.Add(new MobileServiceFunctionAvailability(
                def.Id,
                def.Name,
                def.Category,
                status,
                $"{brand} • {def.Note}",
                Requirements(def, connected),
                def.Route,
                routeOnly));
        }

        return result;
    }

    private static bool Fits(
        IReadOnlyList<string> terms,
        IReadOnlyList<MobileEcuModuleProfile> modules) =>
        modules.Any(module => terms.Any(term =>
            module.Address.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            module.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            module.Purpose.Contains(term, StringComparison.OrdinalIgnoreCase)));

    private static string Requirements(Def def, bool connected)
    {
        var items = new List<string>
        {
            "VIN и выбранный автомобиль должны совпадать",
            "стабильное питание автомобиля",
            "проверка релевантных DTC до активной процедуры",
            "для ECU write — совместимый OEM/J2534/DoIP/ENET/EDIABAS backend"
        };

        if (def.DieselOnly)
            items.Add("подтверждённый дизельный двигатель");
        if (!connected)
            items.Add("сейчас OBD не подключён");

        return "• " + string.Join(Environment.NewLine + "• ", items);
    }
}