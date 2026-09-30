using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoDiagPro.SharedCoding;

// Discovery catalogue only: a listed idea is NEVER evidence that the car supports a coding write.
public sealed record SharedCodingIdea(string Category, string Name, string Module, string Condition, string Source);

public static class SharedCodingCatalog
{
    private static readonly Dictionary<string, string> MakeGroups = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<SharedCodingIdea>> Ideas = new(StringComparer.OrdinalIgnoreCase);

    static SharedCodingCatalog()
    {
        Assign("VAG", "Volkswagen,Audi,Skoda,Škoda,SEAT,CUPRA");
        Assign("PORSCHE", "Porsche");
        Assign("BMW", "BMW,MINI");
        Assign("LUXURY", "Rolls-Royce,Maserati,Bentley,Lamborghini,Ferrari,Aston Martin,McLaren,Lotus");
        Assign("MERCEDES", "Mercedes-Benz,Smart");
        Assign("TOYOTA", "Toyota,Lexus,Daihatsu");
        Assign("FORD", "Ford,Lincoln");
        Assign("HYUNDAI", "Hyundai,Kia,Genesis");
        Assign("STELLANTIS", "Opel,Vauxhall,Peugeot,Citroën,Citroen,DS Automobiles,Fiat,Alfa Romeo,Jeep,Chrysler,Dodge,RAM,Abarth,Lancia");
        Assign("RENAULT", "Renault,Dacia");
        Assign("NISSAN", "Nissan,Infiniti");
        Assign("HONDA", "Honda,Acura");
        Assign("MAZDA", "Mazda");
        Assign("MITSUBISHI", "Mitsubishi");
        Assign("SUBARU", "Subaru");
        Assign("VOLVO", "Volvo,Polestar");
        Assign("JLR", "Jaguar,Land Rover");
        Assign("GM", "Chevrolet,Cadillac,Buick,GMC,Saab");
        Assign("SUZUKI", "Suzuki");
        Assign("ISUZU", "Isuzu");
        Assign("KGM", "KGM / SsangYong");
        Assign("EV_OEM", "Tesla,Rivian,Lucid,NIO,XPeng");
        Assign("CN_OEM", "MG,BYD,Geely,Lynk & Co,Zeekr,Chery,Exeed,Omoda,Jaecoo,GWM,Haval,Ora,Wey");

        Add("COMMON", "Комфорт|Поведение центрального замка|BCM / Body ECU|Только если конкретная машина публикует пользовательский параметр",
            "Освещение|Задержка наружного освещения|Body / Lighting ECU|Требуется датчик/подходящая прошивка и поддержка комплектации",
            "Комфорт|Дистанционные окна и зеркала|Door / Body ECU|Нужны предусмотренные изготовителем моторы и разрешённый параметр",
            "Приборка|Пользовательские экраны и единицы измерения|Cluster / IPC|Проверять только опубликованные настройки приборной панели",
            "Парковка|Отображение/звук парковочной системы|Parking / Head Unit|Штатная парковочная система и совместимая мультимедиа",
            "Сервис|Интервалы обслуживания|Instrument cluster / Service ECU|Проверять только поддерживаемую сервисную адаптацию");
        Add("VAG", "Освещение|Coming Home / Leaving Home|09 BCM|Кодировка зависит от платформы, датчиков и прошивки",
            "Комфорт|Окна с пульта|09 / 42 / 46 / 52|Наличие совместимых дверных блоков и адаптации",
            "Приборка|Тест стрелок Needle Sweep|17 Instruments|Должен поддерживаться версией комбинации приборов",
            "Мультимедиа|Экран парктроника OPS|10 / 5F|Требуется штатная система и совместимая мультимедиа",
            "Комфорт|Опускание пассажирского зеркала назад|52 Door / 09 BCM|Нужен совместимый механизм зеркала и дверной блок");
        Add("PORSCHE", "Комфорт|Автоскладывание зеркал|Body / Door ECU|Только если штатная опция доступна данной платформе",
            "Освещение|Welcome lights|Body / Lighting ECU|Комплектация и OEM-конфигурация Porsche",
            "Приборка|Пользовательские режимы дисплея|Instrument cluster|Проверить версию PCM и приборки");
        Add("BMW", "Комфорт|Складывание зеркал при закрытии|BDC / FEM / FRM|Нужны штатные электроскладываемые зеркала",
            "Приборка|Цифровая скорость|KOMBI|Должна поддерживаться конкретной панелью",
            "Освещение|ДХО и Welcome Lights|FRM / BDC|Проверять поколения кузова и установленную оптику",
            "Мультимедиа|Параметры iDrive / Head Unit|HU / NBT / MGU|Требуется совпадение аппаратной платформы и ПО",
            "Комфорт|Закрытие окон с пульта|CAS / FEM / BDC|Только при наличии заводского параметра");
        Add("LUXURY", "Комфорт|Персонализация зеркал|OEM Body / Door|Только поддерживаемая заводом опция",
            "Освещение|Welcome-подсветка|OEM Lighting|Требуется OEM-документация конкретного шасси",
            "Приборка|Темы дисплея|OEM cluster|Прямое подтверждение прошивкой и комплектацией");
        Add("MERCEDES", "Комфорт|Складывание зеркал|SAM / Door|Электроскладываемые зеркала и доступный параметр",
            "Комфорт|Автозапирание|SAM / EZS|Подтверждённый OEM-параметр кузова",
            "Освещение|Coming Home / свет при открытии|Front / Rear SAM|Штатные датчики и совместимая оптика",
            "Приборка|Настраиваемые экраны|IC / MBUX|Зависит от конкретной версии комбинации приборов");
        Add("TOYOTA", "Комфорт|Настройка Auto Lock / Unlock|Main Body ECU|Опция в Customization для этой комплектации",
            "Комфорт|Окна с пульта|Main Body ECU|Не во всех поколениях и рынках",
            "Освещение|Задержка выключения фар|Main Body ECU|Только если параметр опубликован",
            "Звук|Звуковое подтверждение дверей|Body / Buzzer|Требуется штатный зуммер и поддержка ECU");
        Add("FORD", "Комфорт|Global Open / Close|BCM|Нужны подходящие двери/BCM и рынок",
            "Освещение|ДХО / конфигурация света|BCM|Только валидный профиль по модели и году",
            "Приборка|Настройки IPC|IPC|Сначала проверить поддерживаемые параметры конфигурации",
            "Мультимедиа|Опции штатного экрана|APIM / SYNC|Зависит от установленной версии SYNC");
        Add("HYUNDAI", "Комфорт|Автозапирание|BCM|Поддержка зависит от комплектации",
            "Освещение|Escort / Welcome Lights|BCM|Нужна штатная оптика и параметр",
            "Комфорт|Приветствие зеркал|BCM / Door|Штатные зеркала с приводом",
            "Приборка|Меню приборки|Cluster|Доступные OEM пользовательские параметры");
        Add("STELLANTIS", "Комфорт|Настройки центрального замка|BSI / BCM|Поколение и защищённый gateway могут ограничить доступ",
            "Освещение|ДХО / задержка света|BSI / BCM|Только если доступна заводская telecoding/конфигурация",
            "Комфорт|Поведение окон|Door / BSI|Нужны совместимые дверные блоки",
            "Приборка|Опции дисплея|Cluster|Зависят от комплектации и CAN-платформы");
        Add("RENAULT", "Комфорт|Автозапирание дверей|UCH / BCM|Проверить OEM-разрешённую конфигурацию",
            "Освещение|Welcome / Follow Me Home|UCH / Lighting|Совместимая оптика и UCH",
            "Приборка|Меню приборки|Cluster|Поддерживаемые OEM-параметры",
            "Комфорт|Окна / зеркала|Door / UCH|Нужны штатные опции");
        Add("NISSAN", "Комфорт|Автозапирание|BCM|Только опубликованные OEM пользовательские параметры",
            "Освещение|Welcome / задержка фар|BCM / IPDM|Зависит от поколения автомобиля",
            "Приборка|Параметры дисплея|Meter / Cluster|Совместимая версия ПО");
        Add("HONDA", "Комфорт|Автоматический замок|MICU / BCM|Должен присутствовать параметр",
            "Освещение|Свет после закрытия|MICU|Зависит от датчиков и оптики",
            "Приборка|Меню панели|Gauge Control|Только реально опубликованные функции");
        Add("MAZDA", "Комфорт|Автозапирание|BCM|Не все поколения поддерживают изменение",
            "Освещение|Follow Me Home|BCM / Lighting|Нужны штатные датчики и параметры",
            "Приборка|Настройки комбинации приборов|IC|По аппаратной и программной ревизии");
        Add("MITSUBISHI", "Комфорт|Автозапирание|ETACS|Только документированные customization-параметры",
            "Освещение|Coming Home|ETACS|Совместимая штатная оптика",
            "Звук|Подтверждение закрытия|ETACS / Buzzer|Наличие штатного звукового блока");
        Add("SUBARU", "Комфорт|Параметры блокировки|BIU / BCM|Заводские опции BIU данной платформы",
            "Освещение|ДХО и приветствие|BIU|Только документированные адаптации",
            "Приборка|Настройки Meter|Combination Meter|Конкретная версия приборки");
        Add("VOLVO", "Комфорт|Автоматическая блокировка|CEM|Нужен подходящий CEM и профиль платформы",
            "Освещение|Welcome / подходящая задержка|CEM|Поддержка зависит от светового оборудования",
            "Приборка|Настройки дисплея|DIM|Доступный штатный параметр");
        Add("JLR", "Комфорт|Складывание зеркал|BCM / Door|Только при поддержке моторчиков и OEM профиля",
            "Освещение|Подсветка подхода|BCM|Комплектация и версия кузовного блока",
            "Приборка|Персонализация панели|IPC|Доступная штатная опция");
        Add("GM", "Комфорт|Global unlock / lock|BCM|Разрешённые OEM опции конкретной платформы",
            "Освещение|ДХО / Exit Lighting|BCM|Не для каждого рынка и комплектации",
            "Приборка|Настройки IPC|IPC|Только реально опубликованные параметры");
        Add("SUZUKI", "Комфорт|Автоматическая блокировка|BCM|Если поддерживается штатной конфигурацией",
            "Освещение|Follow Me Home|BCM|Совместимая оптика и прошивка");
        Add("ISUZU", "Комфорт|Настройки замков|Body ECU|При наличии заводской customization",
            "Приборка|Сервисные напоминания|Cluster|Проверять только штатные функции");
        Add("KGM", "Комфорт|Настройки замков|BCM|Если опубликовано заводом",
            "Освещение|Свет при открытии|BCM|Зависит от модели и комплектации");
        Add("CN_OEM", "Комфорт|Штатные настройки доступа|OEM body controller|Возможны ограничения OEM аккаунта и gateway",
            "Освещение|Welcome-подсветка|OEM lighting|Обычно требуется штатный интерфейс автомобиля",
            "Приборка|Режимы цифрового дисплея|OEM digital cluster|Только если такая тема есть в OEM ПО");
        Add("EV_OEM", "Комфорт|Настройки замков в штатном интерфейсе|OEM UI / Body|Не означает, что функция кодируется через OBD",
            "Освещение|Welcome и пользовательский свет|OEM UI / Lighting|При наличии штатной настройки и разрешения производителя",
            "Приборка|Настройки дисплея|OEM UI|Функция зависит от платформы и пакета ПО");
        Add("UNKNOWN", "Профиль|Проверить заводские пользовательские функции|OEM документация|Марка пока без подтверждённой карты блоков");
    }

    public static IReadOnlyCollection<string> KnownMakes => MakeGroups.Keys.ToArray();
    public static string GroupForMake(string? make) =>
        MakeGroups.TryGetValue((make ?? "").Trim(), out var group) ? group : "UNKNOWN";

    public static IReadOnlyList<SharedCodingIdea> ForVehicle(string? make, string? model, int? year)
    {
        var group = GroupForMake(make);
        var result = new List<SharedCodingIdea>();
        // OEM-protected EV systems must not inherit unrelated generic OBD coding promises.
        if (group is not ("EV_OEM" or "CN_OEM" or "LUXURY"))
            result.AddRange(Ideas["COMMON"]);
        if (Ideas.TryGetValue(group, out var specific))
            result.AddRange(specific);
        var m = (model ?? "").Trim();
        var modern = year == null || year >= 2013;
        if (group == "VAG")
        {
            if (modern && !string.IsNullOrWhiteSpace(m))
                result.Add(new("Освещение", "Бегущие поворотники / Dynamic Indicators",
                    "09 BCM / OEM lighting", "Обязательно совместимые LED-секции и поддержка блоком; кодировка не создаёт оборудование", "модель"));
            if (ContainsAny(m, "Octavia", "Superb", "Kodiaq", "Golf", "Passat", "Leon", "A3"))
                result.Add(new("Приборка", "Спортивная тема / vRS / GTI / R",
                    "17 Instruments / Digital Cluster", "Тема должна реально существовать в прошивке именно этой панели", "модель"));
        }
        if (group == "BMW" && modern && ContainsAny(m, "3 Series", "4 Series", "5 Series", "M3", "M4", "X3", "X5"))
            result.Add(new("Приборка", "Спортивная графика M",
                "KOMBI", "Только приборка с поддерживаемой темой и подтверждённым HW/SW", "модель"));
        if (group == "MERCEDES" && modern && ContainsAny(m, "C-Class", "E-Class", "S-Class", "GLC", "GLE"))
            result.Add(new("Приборка", "Спортивные экраны / AMG-тема",
                "IC / MBUX", "Нужны совместимая цифровая приборка и программная тема", "модель"));
        if (group == "FORD" && modern && ContainsAny(m, "Mustang", "Focus", "Explorer"))
            result.Add(new("Приборка", "Спортивные экраны",
                "IPC / SYNC", "Только если предусмотрены данной версией IPC", "модель"));
        return result.GroupBy(x => x.Category + "|" + x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First()).ToArray();
    }

    private static bool ContainsAny(string value, params string[] items) =>
        items.Any(x => value.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static void Assign(string group, string makes)
    {
        foreach (var make in makes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            MakeGroups[make] = group;
    }

    private static void Add(string group, params string[] features)
    {
        var list = Ideas.TryGetValue(group, out var current) ? current : new List<SharedCodingIdea>();
        foreach (var row in features)
        {
            var cells = row.Split('|');
            if (cells.Length != 4) throw new InvalidOperationException("Invalid coding data: " + row);
            list.Add(new(cells[0], cells[1], cells[2], cells[3],
                group == "COMMON" ? "типовая категория" : "семейство марки"));
        }
        Ideas[group] = list;
    }
}
