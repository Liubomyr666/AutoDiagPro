using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class MorePage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();

    public MorePage()
    {
        Title = "Ещё";
        BackgroundColor = Theme.Page;

        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 18, 16, 34),
            Spacing = 14,
            Children =
            {
                Theme.Eyebrow("ALL MODULES"),
                Theme.H1("Все функции"),
                Theme.MutedText("Разделы ПК-версии AutoDiag Pro в мобильной структуре."),
                BuildGroup("ДИАГНОСТИКА", new[]
                {
                    ("Подключение OBD", "Bluetooth LE / Wi-Fi Adapter Manager"),
                    ("Быстрое сканирование", "VIN, DTC и базовые параметры"),
                    ("Блоки управления ECU", "ECU и доступные модули"),
                    ("Ошибки DTC", "Коды неисправностей и AI-разбор"),
                    ("Живые данные", "Live Data / PID"),
                    ("Пробег / износ", "Сравнение доступных данных")
                }),
                BuildGroup("РЕМОНТ И AI", new[]
                {
                    ("Repair Brain / До-После", "DTC → проверка → ремонт → контроль"),
                    ("AI Помощник", "Неисправность, детали, цены и магазины"),
                    ("Сервис / ТО", "Интервалы и напоминания"),
                    ("Шины / колодки", "Износ и рекомендации"),
                    ("Детали по VIN", "Подбор оригинала и аналогов")
                }),
                BuildGroup("ПРОГРАММИРОВАНИЕ И СЕРВИС", new[]
                {
                    ("Программирование ECU", "Прошивки и software info"),
                    ("Tuning / Stage", "Stock / Stage / TCU"),
                    ("Кодирование / Адаптации", "Конфигурация блоков"),
                    ("Ключи и иммобилайзер", "Подготовка и сервис ключей"),
                    ("Сервисные функции ECU", "Reset / EPB / DPF и сервис"),
                    ("АКБ / Battery Coding", "Регистрация и кодирование АКБ")
                }),
                BuildGroup("ДАННЫЕ И СИСТЕМА", new[]
                {
                    ("История диагностики", "Scans и отчёты"),
                    ("Отчёты", "Диагностика и работы"),
                    ("Пользователи / роли", "Аккаунты и права"),
                    ("Администрирование", "Состояние системы"),
                    ("Настройки", "OBD, регион деталей, аккаунт")
                })
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private View BuildGroup(string title, IEnumerable<(string Title, string Subtitle)> items)
    {
        var list = new VerticalStackLayout { Spacing = 9 };
        list.Add(Theme.Eyebrow(title));
        foreach (var item in items)
            list.Add(ModuleRow(item.Title, item.Subtitle));
        return list;
    }

    private View ModuleRow(string title, string subtitle)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10
        };
        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText(subtitle)
            }
        }, 0, 0);
        grid.Add(new Label
        {
            Text = "›",
            FontSize = 28,
            TextColor = Theme.Accent,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.End
        }, 1, 0);

        var card = Theme.CardView(grid, new Thickness(14));

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenAsync(title, subtitle);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task OpenAsync(string title, string subtitle)
    {
        if (title is "Подключение OBD" or "Быстрое сканирование" or "Ошибки DTC")
        {
            await Shell.Current.GoToAsync("//diagnostics");
            return;
        }
        if (title == "Живые данные")
        {
            await Shell.Current.GoToAsync("live");
            return;
        }
        if (title == "Блоки управления ECU")
        {
            await Shell.Current.GoToAsync("ecu");
            return;
        }
        if (title == "Пробег / износ")
        {
            await Shell.Current.GoToAsync("mileage");
            return;
        }
        if (title == "Шины / колодки")
        {
            await Shell.Current.GoToAsync("wear");
            return;
        }
        if (title == "Repair Brain / До-После")
        {
            await Shell.Current.GoToAsync("repair");
            return;
        }
        if (title == "AI Помощник")
        {
            await Shell.Current.GoToAsync("ai");
            return;
        }
        if (title == "Сервис / ТО")
        {
            await Shell.Current.GoToAsync("service");
            return;
        }
        if (title == "Детали по VIN")
        {
            await Shell.Current.GoToAsync("parts");
            return;
        }
        if (title is "История диагностики")
        {
            await Shell.Current.GoToAsync("history");
            return;
        }
        if (title == "Отчёты")
        {
            await Shell.Current.GoToAsync("reports");
            return;
        }
        if (title is "Пользователи / роли" or "Администрирование")
        {
            await Shell.Current.GoToAsync("admin");
            return;
        }
        if (title == "Настройки")
        {
            await Shell.Current.GoToAsync("settings");
            return;
        }
        if (title is "Программирование ECU" or "Tuning / Stage" or "Кодирование / Адаптации" or "Ключи и иммобилайзер" or "Сервисные функции ECU" or "АКБ / Battery Coding")
        {
            _state.PendingModuleTitle = title;
            _state.PendingModuleSubtitle = subtitle;
            await Shell.Current.GoToAsync("programming");
            return;
        }

        _state.PendingModuleTitle = title;
        _state.PendingModuleSubtitle = subtitle;
        _state.PendingModuleBody = title switch
        {
            "Repair Brain / До-После" => "Используй последнюю диагностику как исходную точку: ошибка → вероятная причина → измерения → подтверждённый ремонт → контрольный scan. AI помогает объяснить результат и подобрать детали.",
            "Программирование ECU" => "Мобильный интерфейс показывает раздел и подготовку операции. Реальная запись блока разрешается только с совместимым интерфейсом, стабильным питанием и поддержанным автомобилем.",
            "Tuning / Stage" => "Раздел предназначен для профилей Stock/Stage и информации по ECU/TCU. Прошивка не запускается автоматически и требует совместимого оборудования.",
            "Кодирование / Адаптации" => "Подготовка кодирования и адаптаций по автомобилю. Перед записью приложение должно проверить VIN, блок, напряжение и поддержку процедуры.",
            "Ключи и иммобилайзер" => "Инструменты ключей и иммобилайзера отображаются в общей структуре. Реальные операции доступны только для поддержанных автомобилей и авторизованного сервисного сценария.",
            "АКБ / Battery Coding" => "Регистрация и кодирование аккумулятора для поддержанных автомобилей: технология, ёмкость, производитель и подтверждение замены.",
            "Сервисные функции ECU" => "Сервисные процедуры: сбросы, EPB, DPF и другие функции в зависимости от поддерживаемого блока.",
            _ => $"{subtitle}. Раздел перенесён из ПК-версии в мобильную архитектуру AutoDiag Pro."
        };
        await Shell.Current.GoToAsync("module");
    }
}