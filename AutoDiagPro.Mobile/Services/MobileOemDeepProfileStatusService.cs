namespace AutoDiagPro.Mobile.Services;

public static class MobileOemDeepProfileStatusService
{
    public static string Describe(string? brand)
    {
        var group = MobileEcuPlatformCatalogService.Group(brand);

        return group switch
        {
            "VAG" =>
                "Windows/J2534: verified JSON deep-profile engine активен для текущего VAG UDS pack. " +
                "ECU помечается найденным только после реального положительного ответа.",

            "BMW" =>
                "BMW/MINI deep read-only идёт через Windows EDIABAS/K+DCAN/ENET. " +
                "JSON J2534 deep-profile используется только там, где для платформы есть отдельный verified pack.",

            "MERCEDES" =>
                "Mercedes/Smart: OEM deep-profile engine готов. Текущий foundation-пакет не содержит активных ABS/SRS/BCM/Gateway CAN-ID, " +
                "пока они не подтверждены для конкретной платформы/года.",

            "TOYOTA" =>
                "Toyota/Lexus/Daihatsu: OEM deep-profile engine готов. Кузовные ABS/SRS/Body/Smart Key адреса не активируются без verified platform pack.",

            "FORD" =>
                "Ford/Lincoln: OEM deep-profile engine готов. HS-CAN/MS-CAN кузовные ECU не сканируются по догадке; нужен verified profile конкретной платформы.",

            _ =>
                "OEM deep-profile engine работает на Windows/J2534. Если verified pack для текущей платформы отсутствует, AutoDiag не перебирает неизвестные ECU адреса."
        };
    }
}
