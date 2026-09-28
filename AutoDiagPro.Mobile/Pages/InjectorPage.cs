using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class InjectorPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly Label _status = Theme.MutedText("Подключите OBD и запустите проверку.");
    private readonly VerticalStackLayout _values = new() { Spacing = 9 };
    private bool _busy;

    public InjectorPage()
    {
        Title = "Форсунки";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        var scan = Theme.PrimaryButton("Проверить топливную систему");
        scan.Clicked += async (_, _) => await RefreshAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("INJECTORS / FUEL"),
                    Theme.H1("Форсунки"),
                    Theme.MutedText("AutoDiag показывает только реально прочитанные значения ECU. Коррекции по цилиндрам не подменяются выдуманными данными."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 7,
                        Children =
                        {
                            Theme.Eyebrow("ВАЖНО"),
                            Theme.Body("Через обычный ELM327 доступны только стандартные OBD-II PID. Марочные коррекции форсунок, rail-specific параметры и кодирование форсунок требуют совместимого диагностического интерфейса.")
                        }
                    }),
                    scan,
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
        if (_obd.IsConnected) await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _values.Clear();

        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Сначала откройте «Диагностика».";
            _status.TextColor = Theme.Accent;
            return;
        }

        _busy = true;
        _status.Text = "Читаю топливные параметры...";
        _status.TextColor = Theme.Accent;

        try
        {
            var data = await _obd.InjectorSnapshotAsync();
            foreach (var item in data)
            {
                _values.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        Theme.Eyebrow(item.Key),
                        new Label
                        {
                            Text = item.Value,
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Text,
                            LineBreakMode = LineBreakMode.WordWrap,
                            FontAutoScalingEnabled = false
                        }
                    }
                }, new Thickness(12)));
            }

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
        }
    }
}
