using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class MorePage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();

    public MorePage()
    {
        Title = "Ещё";
        BackgroundColor = Theme.Page;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 38),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("AUTODIAG PRO • MOBILE WORKSPACE"),
                    Theme.H1("Все функции"),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 7,
                        Children =
                        {
                            Theme.Pill(AccessPolicy.FriendlyRole, Theme.Green),
                            Theme.Body("Структура синхронизирована с AutoDiag Pro Windows v5.4.0. Открываются только реальные мобильные экраны — без пустых заглушек.")
                        }
                    }),
                    BuildGroup("ДИАГНОСТИКА", new[]
                    {
                        Item("Подключение OBD", "Bluetooth LE / Wi-Fi • Adapter Manager", "//diagnostics"),
                        Item("Полная диагностика", "VIN • ECU • DTC • Readiness • Live Data", "//diagnostics"),
                        Item("Быстрое сканирование", "VIN, DTC и базовые параметры", "//diagnostics"),
                        Item("Блоки управления ECU", "ECU / calibration identification", "ecu"),
                        Item("Ошибки DTC", "Коды неисправностей и AI-разбор", "//diagnostics"),
                        Item("Живые данные", "Live Data / PID", "live"),
                        Item("Форсунки", "Fuel pressure • trims • MAF • injector live", "injectors"),
                        Item("Дизель / топливная система", "Fuel pressure • trims • MAF • diesel live", "diesel"),
                        Item("Пробег", "Доступные mileage-данные", "mileage"),
                        Item("Распознавание VIN", "Марка • регион • модельный год", "vehicleidentity")
                    }),
                    BuildGroup("РЕМОНТ И СЕРВИС", new[]
                    {
                        Item("Каталог двигателей", "Модели • поколения • моторы • типовые проблемы", "enginecatalog"),
                        Item("Repair Brain / До-После", "Диагностика → ремонт → контроль", "repair"),
                        Item("AI помощник", "Неисправность • проверки • детали", "ai"),
                        Item("Сервис / ТО", "Интервалы и напоминания", "service"),
                        Item("Шины / колодки", "Замеры износа и история", "wear"),
                        Item("Детали по VIN", "Оригинал, аналоги и совместимость", "parts"),
                        Item("История автомобиля", "Диагностика • ремонт • ТО • детали • счета", "timeline"),
                        Item("История scan", "Сохранённые диагностические проверки", "history"),
                        Item("Отчёты / PDF", "Сервисный отчёт и сравнение", "reports"),
                        Item("Уведомления", "ТО • DTC • записи • счета • синхронизация", "notifications")
                    }),
                    BuildGroup("ПРОГРАММИРОВАНИЕ", new[]
                    {
                        Program("Программирование ECU", "Software / calibration • capability check"),
                        Program("Tuning / Stage", "Stock • Stage • ECU/TCU preparation"),
                        Program("Кодирование / Адаптации", "Coding • adaptations • configuration"),
                        Program("Ключи и иммобилайзер", "Key / immobilizer capability check"),
                        Program("Сервисные функции ECU", "Reset • EPB • DPF • service"),
                        Program("АКБ / Battery Coding", "Battery registration / coding profile")
                    }),
                    BuildGroup("СТО", new[]
                    {
                        Item("Клиенты / CRM", "Клиенты, контакты и история", "workshopmanager", "Клиенты / CRM"),
                        Item("Запись клиентов", "Календарь и визиты", "workshopmanager", "Запись"),
                        Item("Счета / чеки", "Суммы и статус оплаты", "workshopmanager", "Счета / чеки"),
                        Item("Сотрудники", "Механики, должности и навыки", "workshopmanager", "Сотрудники"),
                        Item("Склад", "Остатки и поступления", "inventory"),
                        Item("QR-приёмка деталей", "Приём и привязка к авто", "qrparts"),
                        Item("Управление СТО", "Заказ-наряды • статусы • сметы", "//workshop")
                    }),
                    BuildGroup("СИСТЕМА", new[]
                    {
                        Item("Пользователи / роли", "Аккаунты сотрудников и клиентов", "admin"),
                        Item("Администрирование", "Состояние системы и доступы", "admin"),
                        Item("Настройки", "OBD • регион • аккаунт", "settings")
                    }),
                    Theme.MutedText($"AutoDiag Pro iOS • {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})")
                }
            }
        };
    }

    private static MenuItem Item(string title, string subtitle, string route, string? module = null) =>
        new(title, subtitle, route, module, false);

    private static MenuItem Program(string title, string subtitle) =>
        new(title, subtitle, "programming", title, true);

    private View BuildGroup(string title, IEnumerable<MenuItem> items)
    {
        var list = new VerticalStackLayout { Spacing = 9 };
        list.Add(Theme.Eyebrow(title));
        foreach (var item in items)
            list.Add(ModuleRow(item));
        return list;
    }

    private View ModuleRow(MenuItem item)
    {
        var text = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label
                {
                    Text = item.Title,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText(item.Subtitle)
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        grid.Add(text, 0, 0);
        grid.Add(new Label
        {
            Text = "›",
            FontSize = 27,
            TextColor = Theme.Accent,
            VerticalTextAlignment = TextAlignment.Center,
            FontAutoScalingEnabled = false
        }, 1, 0);

        var card = Theme.CardView(grid, new Thickness(14));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenAsync(item);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task OpenAsync(MenuItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Module))
        {
            _state.PendingModuleTitle = item.Module;
            _state.PendingModuleSubtitle = item.Subtitle;
        }

        await Shell.Current.GoToAsync(item.Route);
    }

    private sealed record MenuItem(
        string Title,
        string Subtitle,
        string Route,
        string? Module,
        bool Programming);
}

[executed on device: Rubakha (3db26fc9-a901-43ec-82ae-bbafd3175a39)]