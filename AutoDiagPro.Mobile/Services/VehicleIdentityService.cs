namespace AutoDiagPro.Mobile.Services;

public sealed record VehicleIdentityResult(
    string Vin,
    bool IsValid,
    string Make,
    string Country,
    int? ModelYear,
    string Wmi,
    string Summary);

public static class VehicleIdentityService
{
    private static readonly Dictionary<string, string> WmiMakes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WVW"] = "Volkswagen", ["WVG"] = "Volkswagen", ["WV1"] = "Volkswagen",
        ["WAU"] = "Audi", ["WA1"] = "Audi", ["TRU"] = "Audi",
        ["WBA"] = "BMW", ["WBS"] = "BMW", ["WBY"] = "BMW",
        ["WDD"] = "Mercedes-Benz", ["WDB"] = "Mercedes-Benz", ["WDC"] = "Mercedes-Benz",
        ["W1K"] = "Mercedes-Benz", ["W1N"] = "Mercedes-Benz",
        ["TMB"] = "Škoda", ["TMK"] = "Škoda", ["VSS"] = "SEAT/CUPRA",
        ["WF0"] = "Ford", ["VF1"] = "Renault", ["VF3"] = "Peugeot", ["VF7"] = "Citroën",
        ["ZFA"] = "Fiat", ["ZAR"] = "Alfa Romeo", ["YS3"] = "Saab",
        ["JHM"] = "Honda", ["JTD"] = "Toyota", ["JT2"] = "Toyota", ["JTK"] = "Toyota",
        ["JN1"] = "Nissan", ["JNK"] = "Infiniti", ["JM1"] = "Mazda",
        ["KMH"] = "Hyundai", ["KNA"] = "Kia", ["KND"] = "Kia",
        ["1G1"] = "Chevrolet", ["1GC"] = "Chevrolet", ["1FA"] = "Ford", ["1FT"] = "Ford",
        ["1C4"] = "Jeep", ["1C6"] = "RAM", ["1HG"] = "Honda", ["1N4"] = "Nissan",
        ["5YJ"] = "Tesla", ["7SA"] = "Tesla", ["YV1"] = "Volvo", ["YV4"] = "Volvo"
    };

    public static VehicleIdentityResult Decode(string? value)
    {
        var vin = Normalize(value);
        if (vin.Length != 17)
            return new(vin, false, "Не определено", "Не определено", null,
                vin.Length >= 3 ? vin[..3] : vin,
                "VIN должен содержать 17 символов.");

        var wmi = vin[..3];
        var make = WmiMakes.TryGetValue(wmi, out var known) ? known : "Не определено";
        var country = Country(vin[0]);
        var year = DecodeYear(vin[9]);

        var summary = $"VIN: {vin}\nМарка по WMI: {make}\nСтрана/регион: {country}\n" +
                      $"Модельный год: {(year?.ToString() ?? "не определён")}\nWMI: {wmi}\n" +
                      "Точная модель, двигатель и коробка подтверждаются данными ECU/заводским каталогом.";

        return new(vin, true, make, country, year, wmi, summary);
    }

    public static string InferModel(string? value)
    {
        var vin = Normalize(value);
        if (vin.Length != 17) return "";

        var wmi = vin[..3];
        if (wmi is "WDD" or "WDB" or "WDC" or "W1K" or "W1N")
        {
            var series = vin.Substring(3, 3);
            return series switch
            {
                "117" or "118" => "CLA",
                "176" or "177" => "A-Class",
                "204" or "205" or "206" => "C-Class",
                "207" or "211" or "212" or "213" or "214" => "E-Class",
                "221" or "222" or "223" => "S-Class",
                "253" or "254" => "GLC",
                "164" or "166" or "167" => "GLE",
                "156" => "GLA",
                "447" => "V-Class",
                "906" or "907" => "Sprinter",
                _ => ""
            };
        }

        return "";
    }

    public static string Normalize(string? value) =>
        new((value ?? "").Trim().ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c) && c is not 'I' and not 'O' and not 'Q')
            .ToArray());

    private static int? DecodeYear(char code)
    {
        const string codes = "ABCDEFGHJKLMNPRSTVWXY123456789";
        var index = codes.IndexOf(code);
        if (index < 0) return null;

        var now = DateTime.UtcNow.Year;
        var candidates = new List<int>();
        for (var baseYear = 1980 + index; baseYear <= now + 1; baseYear += 30)
            candidates.Add(baseYear);

        return candidates.Count == 0 ? null : candidates.Max();
    }

    private static string Country(char first) => first switch
    {
        '1' or '4' or '5' => "США",
        '2' => "Канада",
        '3' => "Мексика",
        'J' => "Япония",
        'K' => "Южная Корея",
        'S' => "Великобритания",
        'V' => "Франция / Испания",
        'W' => "Германия",
        'T' => "Чехия / Венгрия",
        'Y' => "Швеция / Финляндия",
        'Z' => "Италия",
        _ => "Регион не определён"
    };
}
