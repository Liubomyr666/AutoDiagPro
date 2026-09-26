using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class LiveDataPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly VerticalStackLayout _values = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Подключите OBD-адаптер.");
    private readonly Button _refresh = Theme.PrimaryButton("Обновить Live Data");
    private IDispatcherTimer? _timer;
    private bool _busy;

    public LiveDataPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Live Data";
        _refresh.Clicked += async (_, _) => await RefreshAsync();

        var auto = new Switch();
        auto.Toggled += (_, e) =>
        {
            if (e.Value) StartTimer();
            else StopTimer();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("LIVE DATA"),
                    Theme.H1("Живые данные"),
                    Theme.MutedText("Параметры читаются напрямую через ELM327. Неподдерживаемые PID автоматически пропускаются."),
                    BuildAutoRefreshCard(auto),
                    _refresh,
                    _status,
                    _values
                }
            }
        };
    }

    private static View BuildAutoRefreshCard(Switch auto)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10
        };
        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children = { Theme.H2("Автообновление"), Theme.MutedText("Раз в 3 секунды") }
        }, 0, 0);
        grid.Add(auto, 1, 0);
        return Theme.CardView(grid);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        StopTimer();
        base.OnDisappearing();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(3);
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Откройте «Диагностика» и подключите адаптер.";
            _status.TextColor = Theme.Accent;
            return;
        }

        _busy = true;
        _refresh.IsEnabled = false;
        _status.Text = "Читаю данные...";
        _status.TextColor = Theme.Accent;
        try
        {
            var data = await _obd.LiveSnapshotAsync();
            _values.Clear();

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 9,
                RowSpacing = 9
            };

            var i = 0;
            foreach (var item in data)
            {
                grid.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        Theme.Eyebrow(item.Key),
                        new Label { Text = item.Value, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text }
                    }
                }, new Thickness(12)), i % 2, i / 2);
                i++;
            }

            _values.Add(grid);
            _status.Text = $"ONLINE • {_obd.TransportName} • {DateTime.Now:HH:mm:ss}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
        finally
        {
            _busy = false;
            _refresh.IsEnabled = true;
        }
    }
}
