namespace AutoDiagPro.Mobile.Services;

public static class MobileDiagnosticRouteService
{
    public static string Describe(string? brand)
    {
        var group = MobileEcuPlatformCatalogService.Group(brand);

        return group switch
        {
            "VAG" =>
                "iPhone: BLE/Wi-Fi ELM → стандартный OBD-II + безопасная VAG UDS read-only идентификация. " +
                "Windows fallback: native J2534; старые legacy ECU требуют отдельного VAG транспорта.",

            "BMW" =>
                "iPhone: универсальный OBD-II через BLE/Wi-Fi ELM. " +
                "Глубокий BMW/MINI read-only IDENT: Windows + EDIABAS/K+DCAN/ENET; новые платформы могут требовать Ethernet/DoIP.",

            "MERCEDES" or "TOYOTA" or "FORD" =>
                "iPhone: responder-derived OBD + UDS identity/DTC + Mode 09 VIN/CALID/CVN/ECU Name. " +
                "Глубокие ABS/SRS/BCM/Gateway подтверждаются через Windows J2534/DoIP/OEM профиль конкретной платформы.",

            "VOLVO" or "JLR" or "RENAULT" or "HYUNDAI_KIA" =>
                "iPhone: универсальный OBD-II + responder-derived powertrain identity. Глубокая диагностика: Windows J2534/DoIP/OEM профиль по платформе.",

            "TESLA" =>
                "Стандартный OBD зависит от модели. Глубокие ECU требуют OEM CAN/Ethernet транспорта; приложение не подменяет их generic ELM.",

            _ =>
                "iPhone: универсальный OBD-II через BLE/Wi-Fi ELM. Глубокие ECU подтверждаются через Windows J2534/DoIP/OEM профиль конкретной марки."
        };
    }
}
