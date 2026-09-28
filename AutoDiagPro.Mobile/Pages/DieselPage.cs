using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class DieselPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly Label _status = Theme.MutedText("Подключите OBD и запустите чтение.");
    private readonly VerticalStackLayout _values = new() { Spacing = 9 };

    public DieselPage()
    {
        Title = "Форсунки / дизель";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        var read = Theme.PrimaryButton("Считать топливные параметры");
        read.Clicked += async (_, _) => await LoadAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 38),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("DIESEL / FUEL LIVE"),
                    Theme.H1("Форсунки / дизель"),
                    Theme.MutedText("Читаются только реальные PID, которые автомобиль отдаёт через стандартный OBD-II. Коррекции форсунок по цилиндрам обычно требуют марочного протокола и поэтому не подменяются выдуманными значениями."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            Theme.Eyebrow("ДОСТУПНЫЕ ДАННЫЕ"),
                            Theme.Body("Давление топлива, STFT/LTFT, MAF, MAP, нагрузка, обороты, расход топлива и другие поддержанные PID.")
                        }
                    }),
                    read,
                    _status,
                    _values
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
    }

    private async Task LoadAsync()
    {
        _values.Clear();

        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Откройте «Диагностика» и подключите адаптер.";
            _status.TextColor = Theme.Accent;
            return;
        }

        try
        {
            _status.Text = "Читаю PID...";
            _status.TextColor = Theme.Accent;

            var live = await _obd.LiveSnapshotAsync();
            var preferred = new[]
            {
                "Давление топлива", "STFT Bank 1", "LTFT Bank 1", "MAF", "MAP",
                "RPM", "Нагрузка", "Расход топлива", "Уровень топлива",
                "Температура впуска", "Температура масла", "Напряжение ECU"
            };

            var shown = 0;
            foreach (var key in preferred)
            {
                if (!live.TryGetValue(key, out var value)) continue;
                _values.Add(ValueCard(key, value));
                shown++;
            }

            if (shown == 0)
            {
                _values.Add(Theme.CardView(Theme.Body(
                    "Автомобиль не вернул топливные PID через стандартный OBD-II. Для коррекций форсунок/rail-specific данных нужен марочный протокол или совместимый диагностический интерфейс.")));
                _status.Text = "Стандартные diesel/fuel PID недоступны.";
                _status.TextColor = Theme.Accent;
            }
            else
            {
                _status.Text = $"Получено параметров: {shown}";
                _status.TextColor = Theme.Green;
            }
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View ValueCard(string name, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        grid.Add(new Label
        {
            Text = name,
            TextColor = Theme.TextSoft,
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        grid.Add(new Label
        {
            Text = value,
            TextColor = Theme.Text,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.End
        }, 1, 0);
        return Theme.CardView(grid, new Thickness(13));
    }
}
