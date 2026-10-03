namespace AutoDiagPro.Mobile.Services;

public sealed record MobileEcuModuleProfile(
    string Address,
    string Name,
    string Purpose,
    string Requirement);

public static class MobileEcuPlatformCatalogService
{
    private static MobileEcuModuleProfile M(string a, string n, string p, string r) => new(a, n, p, r);

    private static readonly MobileEcuModuleProfile[] Generic =
    {
        M("OBD", "Engine / Emissions", "Двигатель", "Стандартный OBD-II read-only доступ."),
        M("TCM", "Transmission", "Коробка", "Марочный CAN/UDS профиль."),
        M("ABS", "ABS / ESC", "Тормоза", "Марочный CAN/UDS профиль."),
        M("SRS", "Airbag", "Подушки", "Марочный ECU / OEM transport."),
        M("BCM", "Body Control", "Кузовная электроника", "Марочный ECU / OEM transport."),
        M("IPC", "Instrument Cluster", "Приборная панель", "Марочный ECU / OEM transport."),
        M("GW", "Gateway", "Gateway", "Марочный gateway/OEM transport."),
        M("EPS", "Power Steering", "Рулевое управление", "Марочный CAN/UDS профиль."),
        M("HVAC", "Climate", "Климат", "Марочный ECU."),
        M("ACCESS", "Access / Immobilizer", "Доступ", "OEM/secure access profile."),
        M("EPB", "Parking Brake", "Электроручник", "Марочная сервисная процедура."),
        M("ADAS", "Camera / Radar", "ADAS", "OEM UDS/DoIP профиль.")
    };

    private static readonly MobileEcuModuleProfile[] Vag =
    {
        M("01", "Engine", "Двигатель", "BLE/Wi-Fi ELM может подтвердить UDS read-only."),
        M("02", "Transmission / DSG", "Коробка", "BLE/Wi-Fi ELM может подтвердить UDS read-only."),
        M("03", "ABS / ESP", "Тормоза", "Для глубокого доступа предпочтителен J2534/VAG transport."),
        M("15", "Airbag / SRS", "Подушки", "Для глубокого доступа предпочтителен J2534/VAG transport."),
        M("17", "Instruments", "Приборная панель", "VAG-specific transport."),
        M("19", "CAN Gateway", "Gateway", "VAG gateway scan / J2534."),
        M("09", "Central Electrics", "BCM / свет", "VAG-specific transport."),
        M("44", "Steering Assist", "Рулевое управление", "VAG-specific transport."),
        M("53", "Parking Brake", "EPB", "VAG-specific transport."),
        M("TPMS", "Tyre Pressure", "TPMS", "VAG-specific profile where fitted."),
        M("5F", "Information Electronics", "Мультимедиа", "VAG-specific UDS/DoIP.")
    };

    private static readonly MobileEcuModuleProfile[] Bmw =
    {
        M("DME/DDE", "Engine", "Двигатель", "Windows EDIABAS/K+DCAN/ENET/DoIP read-only IDENT."),
        M("EGS", "Transmission", "Коробка", "BMW-specific transport."),
        M("DSC", "Dynamic Stability Control", "ABS / DSC", "BMW-specific transport."),
        M("MRS/ACSM", "Airbag", "SRS / Airbag", "BMW-specific transport."),
        M("CAS/EWS", "Car Access / Immobilizer", "Доступ", "BMW-specific transport."),
        M("KOMBI", "Instrument Cluster", "Приборная панель", "BMW-specific transport."),
        M("JBBF/FEM/BDC", "Body Gateway", "Кузов / gateway", "BMW-specific transport."),
        M("FRM", "Footwell Module", "Свет / кузов", "BMW-specific transport."),
        M("IHKA", "Climate", "Климат", "BMW-specific transport."),
        M("KAFAS/ACC", "ADAS", "Камеры / радары", "BMW-specific/DoIP.")
    };

    private static readonly MobileEcuModuleProfile[] Mercedes =
    {
        M("ME/CDI/MR", "Engine", "Двигатель", "Mercedes UDS/J2534/DoIP/OEM profile."),
        M("VGS/EGS", "Transmission", "Коробка", "Mercedes OEM profile."),
        M("ESP", "ESP / ABS", "Тормоза", "Mercedes OEM profile."),
        M("SRS", "Airbag", "Подушки", "Mercedes OEM profile."),
        M("CGW", "Central Gateway", "Gateway", "Mercedes gateway/OEM profile."),
        M("SAM", "Signal Acquisition Module", "Кузовная электроника", "Mercedes OEM profile."),
        M("IC", "Instrument Cluster", "Приборная панель", "Mercedes OEM profile."),
        M("EIS/EZS", "Ignition / Access", "Доступ", "Mercedes OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "Mercedes OEM profile."),
        M("EPB", "Parking Brake", "EPB", "Mercedes OEM service profile."),
        M("TPMS", "Tyre Pressure", "TPMS", "Mercedes OEM profile where fitted."),
        M("ADAS", "Radar / Camera", "ADAS", "UDS/DoIP/OEM.")
    };

    private static readonly MobileEcuModuleProfile[] Toyota =
    {
        M("ECM", "Engine Control", "Двигатель", "Toyota CAN/UDS/J2534 profile."),
        M("ECT/TCM", "Transmission", "Коробка", "Toyota OEM profile."),
        M("ABS/VSC", "Skid Control", "ABS / VSC", "Toyota OEM profile."),
        M("SRS", "Airbag", "Подушки", "Toyota OEM profile."),
        M("MAIN BODY", "Main Body ECU", "Кузов", "Toyota OEM profile."),
        M("METER", "Combination Meter", "Приборная панель", "Toyota OEM profile."),
        M("GATEWAY", "Network Gateway", "Gateway", "Toyota OEM profile."),
        M("SMART KEY", "Smart Key", "Доступ", "Toyota OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "Toyota OEM profile."),
        M("PCS/RADAR", "Pre-Collision / Radar", "ADAS", "Toyota OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] Ford =
    {
        M("PCM", "Powertrain Control", "Двигатель", "Ford CAN/UDS/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "Ford OEM profile."),
        M("ABS", "ABS Module", "Тормоза", "Ford OEM profile."),
        M("RCM", "Restraints Control", "Подушки", "Ford OEM profile."),
        M("BCM", "Body Control", "Кузов", "Ford OEM profile."),
        M("IPC", "Instrument Cluster", "Приборная панель", "Ford OEM profile."),
        M("GWM", "Gateway Module", "Gateway", "Ford OEM profile."),
        M("PSCM", "Power Steering", "Рулевое управление", "Ford OEM profile."),
        M("APIM", "SYNC / Infotainment", "Мультимедиа", "Ford OEM profile."),
        M("IPMA/CCM", "ADAS", "Камеры / радары", "Ford OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] Gm =
    {
        M("ECM", "Engine Control", "Двигатель", "GM CAN/UDS/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "GM OEM profile."),
        M("EBCM", "Brake Control", "ABS / ESC", "GM OEM profile."),
        M("SDM", "Airbag / Restraints", "Подушки", "GM OEM profile."),
        M("BCM", "Body Control", "Кузов", "GM OEM profile."),
        M("IPC", "Instrument Cluster", "Приборная панель", "GM OEM profile."),
        M("GATEWAY", "Serial Data Gateway", "Gateway", "GM OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "GM OEM profile."),
        M("HVAC", "Climate", "Климат", "GM OEM profile."),
        M("ADAS", "Camera / Radar", "ADAS", "GM OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] Stellantis =
    {
        M("ECM/PCM", "Engine Control", "Двигатель", "CAN/UDS/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "OEM profile."),
        M("ABS/ESP", "Brake Control", "ABS / ESP", "OEM profile."),
        M("ORC/SRS", "Restraints", "Подушки", "OEM profile."),
        M("BCM/BSI", "Body Control / BSI", "Кузов", "OEM profile."),
        M("IPC", "Instrument Cluster", "Приборная панель", "OEM profile."),
        M("SGW/GW", "Secure / Central Gateway", "Gateway", "На части новых машин нужен authorized Secure Gateway access."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("EPB", "Parking Brake", "EPB", "OEM service profile."),
        M("ADAS", "Camera / Radar", "ADAS", "OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] Renault =
    {
        M("INJECTION", "Injection / Engine", "Двигатель", "Renault/Dacia CAN/UDS/J2534 profile."),
        M("GEARBOX", "Automatic Gearbox", "Коробка", "OEM profile."),
        M("ABS/ESC", "Brake Control", "ABS / ESC", "OEM profile."),
        M("AIRBAG", "Airbag", "Подушки", "OEM profile."),
        M("UCH", "UCH / Body Control", "Кузов", "OEM profile."),
        M("CLUSTER", "Instrument Panel", "Приборная панель", "OEM profile."),
        M("GATEWAY", "Gateway", "Gateway", "OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("EPB", "Parking Brake", "EPB", "OEM profile."),
        M("ADAS", "Camera / Radar", "ADAS", "OEM/DoIP profile.")
    };

    private static readonly MobileEcuModuleProfile[] HyundaiKia =
    {
        M("ECM", "Engine Control", "Двигатель", "Hyundai/Kia CAN/UDS/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "OEM profile."),
        M("ABS/ESC", "Brake / Stability", "ABS / ESC", "OEM profile."),
        M("SRS", "Airbag", "Подушки", "OEM profile."),
        M("BCM", "Body Control", "Кузов", "OEM profile."),
        M("CLU", "Cluster", "Приборная панель", "OEM profile."),
        M("GW", "Gateway", "Gateway", "OEM profile."),
        M("SMK", "Smart Key", "Доступ", "OEM profile."),
        M("MDPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("SCC/FCA/LKA", "ADAS", "Камеры / радары", "OEM/DoIP profile.")
    };

    private static readonly MobileEcuModuleProfile[] Nissan =
    {
        M("ECM", "Engine Control", "Двигатель", "Nissan/Infiniti CAN/UDS/J2534 profile."),
        M("TCM/CVT", "Transmission", "Коробка / CVT", "OEM profile."),
        M("ABS/VDC", "Brake / VDC", "ABS / ESC", "OEM profile."),
        M("AIRBAG", "Airbag", "Подушки", "OEM profile."),
        M("BCM", "Body Control", "Кузов", "OEM profile."),
        M("METER", "Combination Meter", "Приборная панель", "OEM profile."),
        M("IPDM", "Power Distribution", "Электропитание", "OEM profile."),
        M("I-KEY", "Intelligent Key", "Доступ", "OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("ADAS", "AVM / Radar / Camera", "ADAS", "OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] Honda =
    {
        M("PCM/ECM", "Powertrain Control", "Двигатель", "Honda/Acura CAN/UDS/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "OEM profile."),
        M("VSA/ABS", "Vehicle Stability Assist", "ABS / ESC", "OEM profile."),
        M("SRS", "Airbag", "Подушки", "OEM profile."),
        M("MICU/BCM", "Body Control", "Кузов", "OEM profile."),
        M("GAUGE", "Gauge Control", "Приборная панель", "OEM profile."),
        M("GATEWAY", "Gateway", "Gateway", "OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("SMART ENTRY", "Smart Entry", "Доступ", "OEM profile."),
        M("ADAS", "Honda Sensing", "ADAS", "OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] JapaneseGeneric =
    {
        M("ECM/PCM", "Engine Control", "Двигатель", "CAN/UDS/J2534 OEM profile."),
        M("TCM/CVT", "Transmission", "Коробка", "OEM profile."),
        M("ABS/ESC", "Brake / Stability", "ABS / ESC", "OEM profile."),
        M("SRS", "Airbag", "Подушки", "OEM profile."),
        M("BCM/BIU/ETACS", "Body Control", "Кузов", "OEM profile."),
        M("METER", "Instrument Cluster", "Приборная панель", "OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("HVAC", "Climate", "Климат", "OEM profile."),
        M("KEYLESS", "Keyless / Immobilizer", "Доступ", "OEM profile."),
        M("ADAS", "Camera / Radar", "ADAS", "OEM profile.")
    };

    private static readonly MobileEcuModuleProfile[] Volvo =
    {
        M("ECM", "Engine Control", "Двигатель", "Volvo/Polestar CAN/UDS/DoIP/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "OEM profile."),
        M("BCM", "Brake Control", "ABS / ESC", "OEM profile."),
        M("SRS", "Restraints", "Подушки", "OEM profile."),
        M("CEM", "Central Electronic Module", "Gateway / кузов", "Volvo OEM profile."),
        M("DIM", "Driver Information Module", "Приборная панель", "OEM profile."),
        M("VGM", "Vehicle Gateway", "Gateway", "SPA/CMA OEM/DoIP profile."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("CCM", "Climate", "Климат", "OEM profile."),
        M("ADAS", "Camera / Radar", "ADAS", "OEM/DoIP profile.")
    };

    private static readonly MobileEcuModuleProfile[] Jlr =
    {
        M("PCM", "Powertrain Control", "Двигатель", "JLR CAN/UDS/DoIP/J2534 profile."),
        M("TCM", "Transmission", "Коробка", "OEM profile."),
        M("ABS", "Anti-Lock Brakes", "ABS / DSC", "OEM profile."),
        M("RCM", "Restraints Control", "Подушки", "OEM profile."),
        M("BCM", "Body Control", "Кузов", "OEM profile."),
        M("IPC", "Instrument Cluster", "Приборная панель", "OEM profile."),
        M("GWM", "Gateway Module", "Gateway", "OEM/DoIP profile."),
        M("KVM", "Keyless Vehicle", "Доступ", "OEM profile."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM profile."),
        M("ADAS", "Camera / Radar", "ADAS", "OEM/DoIP profile.")
    };

    private static readonly MobileEcuModuleProfile[] Tesla =
    {
        M("VCSEC", "Vehicle Controller / Security", "Доступ / кузов", "OEM CAN/Ethernet."),
        M("GW", "Gateway", "Gateway", "OEM Ethernet/CAN."),
        M("BMS", "Battery Management", "Высоковольтная батарея", "OEM CAN/Ethernet."),
        M("INV", "Drive Inverter", "Силовая электроника", "OEM CAN/Ethernet."),
        M("ABS", "Brake / Stability", "ABS / stability", "OEM CAN."),
        M("EPS", "Power Steering", "Рулевое управление", "OEM CAN."),
        M("SRS", "Restraints", "Подушки", "OEM CAN."),
        M("ADAS", "Autopilot / ADAS", "Камеры / ADAS", "OEM Ethernet/CAN.")
    };

    public static IReadOnlyList<MobileEcuModuleProfile> GetModules(string? brand) =>
        Group(brand) switch
        {
            "VAG" => Vag,
            "BMW" => Bmw,
            "MERCEDES" => Mercedes,
            "TOYOTA" => Toyota,
            "FORD" => Ford,
            "GM" => Gm,
            "STELLANTIS" => Stellantis,
            "RENAULT" => Renault,
            "HYUNDAI_KIA" => HyundaiKia,
            "NISSAN" => Nissan,
            "HONDA" => Honda,
            "JAPANESE" => JapaneseGeneric,
            "VOLVO" => Volvo,
            "JLR" => Jlr,
            "TESLA" => Tesla,
            _ => Generic
        };

    public static string Group(string? brand)
    {
        var b = (brand ?? "").Trim();
        if (b is "Volkswagen" or "Audi" or "Škoda" or "Skoda" or "SEAT" or "CUPRA" or "Porsche") return "VAG";
        if (b is "BMW" or "MINI") return "BMW";
        if (b is "Mercedes-Benz" or "Smart") return "MERCEDES";
        if (b is "Toyota" or "Lexus" or "Daihatsu") return "TOYOTA";
        if (b is "Ford" or "Lincoln") return "FORD";
        if (b is "Chevrolet" or "Cadillac" or "Buick" or "GMC") return "GM";
        if (b is "Opel" or "Vauxhall" or "Peugeot" or "Citroën" or "Citroen" or "DS Automobiles" or "Fiat" or "Alfa Romeo" or "Jeep" or "Chrysler" or "Dodge" or "RAM" or "Abarth" or "Lancia") return "STELLANTIS";
        if (b is "Renault" or "Dacia") return "RENAULT";
        if (b is "Hyundai" or "Kia" or "Genesis") return "HYUNDAI_KIA";
        if (b is "Nissan" or "Infiniti") return "NISSAN";
        if (b is "Honda" or "Acura") return "HONDA";
        if (b is "Mazda" or "Mitsubishi" or "Subaru" or "Suzuki") return "JAPANESE";
        if (b is "Volvo" or "Polestar") return "VOLVO";
        if (b is "Jaguar" or "Land Rover") return "JLR";
        if (b == "Tesla") return "TESLA";
        return "GENERIC";
    }
}