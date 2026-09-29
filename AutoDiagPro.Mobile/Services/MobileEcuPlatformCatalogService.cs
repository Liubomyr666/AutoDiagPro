namespace AutoDiagPro.Mobile.Services;

public sealed record MobileEcuModuleProfile(
    string Address,
    string Name,
    string Purpose,
    string Requirement);

public static class MobileEcuPlatformCatalogService
{
    private static readonly MobileEcuModuleProfile[] BmwModules =
    {
        new("DME/DDE", "Engine", "Двигатель", "Реальное подтверждение: BMW EDIABAS/K+DCAN/ENET read-only IDENT на Windows."),
        new("EGS", "Transmission", "АКПП / коробка", "Точный SGBD зависит от коробки и поколения."),
        new("DSC", "Dynamic Stability Control", "ABS / DSC", "Марочный BMW транспорт обязателен."),
        new("MRS/ACSM", "Airbag", "SRS / Airbag", "Семейство блока зависит от поколения."),
        new("CAS/EWS", "Car Access / Immobilizer", "Доступ / иммобилайзер", "CAS/EWS характерны для разных поколений BMW."),
        new("KOMBI", "Instrument Cluster", "Приборная панель", "Используется как источник контекста одометра/приборки."),
        new("JBBF/FEM/BDC", "Body Gateway", "Кузовная электроника", "Архитектура меняется между E/F/G-series."),
        new("FRM", "Footwell Module", "Свет / кузов", "Характерно для части E/F-series."),
        new("SZL", "Steering Column", "Рулевая колонка", "Требует BMW-specific диагностику."),
        new("IHKA", "Climate", "Климат", "Требует BMW-specific диагностику.")
    };

    private static readonly MobileEcuModuleProfile[] VagModules =
    {
        new("01", "Engine", "Двигатель", "Через совместимый ELM/Vgate AutoDiag может подтвердить Engine UDS read-only."),
        new("02", "Transmission / DSG", "АКПП / DSG", "Через совместимый ELM/Vgate AutoDiag пробует только безопасную UDS идентификацию."),
        new("03", "ABS / ESP", "Тормоза", "Нужен VAG-совместимый марочный транспорт/маршрутизация."),
        new("15", "Airbag / SRS", "Подушки", "Нужен VAG-совместимый марочный транспорт/маршрутизация."),
        new("17", "Instruments", "Приборная панель", "Нужен марочный VAG transport."),
        new("19", "CAN Gateway", "Gateway", "Полный список блоков читается через VAG-specific Gateway scan."),
        new("09", "Central Electrics", "BCM / свет", "Нужен марочный VAG transport."),
        new("44", "Steering Assist", "Рулевое управление", "Нужен марочный VAG transport.")
    };

    public static IReadOnlyList<MobileEcuModuleProfile> GetModules(string? brand)
    {
        var b = (brand ?? "").Trim();

        if (b.Equals("BMW", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("MINI", StringComparison.OrdinalIgnoreCase))
            return BmwModules;

        if (b.Equals("Volkswagen", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("Audi", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("Škoda", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("Skoda", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("SEAT", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("CUPRA", StringComparison.OrdinalIgnoreCase) ||
            b.Equals("Porsche", StringComparison.OrdinalIgnoreCase))
            return VagModules;

        return Array.Empty<MobileEcuModuleProfile>();
    }
}
