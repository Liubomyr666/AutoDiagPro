using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class MileagePage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly VerticalStackLayout _body = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Готово.");

    public MileagePage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Пробег / износ";

        var read = Theme.PrimaryButton("Прочитать доступные OBD данные");
        read.Clicked += async (_, _) => await ReadAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("MILEAGE CENTER"),
                    Theme.H1("Пробег / износ"),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 7,
                        Children =
                        {
                            Theme.Eyebrow("ВАЖНО"),
                            Theme.Body("Стандартный OBD-II обычно не отдаёт полный одометр автомобиля или пробег коробки. AutoDiag не будет называть «реальным пробегом» значение, которое ECU не предоставил. Марочные ECU/TCU значения появятся только для явно поддержанных протоколов.")
                        }
                    }),
                    read, _status, _body
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RenderVehicle();
    }

    private void RenderVehicle()
    {
        _body.Clear();
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _body.Add(Theme.CardView(Theme.MutedText("Сначала выберите автомобиль.")));
            return;
        }

        _body.Add(Row("Автомобиль", v.DisplayName));
        _body.Add(Row("VIN", v.Vin ?? "—"));
        _body.Add(Row("Пробег в AutoDiag", v.MileageKm is null ? "—" : $"{v.MileageKm:N0} км"));
    }

    private async Task ReadAsync()
    {
        RenderVehicle();
        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Стандартные данные не прочитаны.";
            _status.TextColor = Theme.Accent;
            return;
        }

        try
        {
            var live = await _obd.LiveSnapshotAsync();
            if (live.TryGetValue("Пробег после сброса DTC", out var distance))
                _body.Add(Row("После сброса DTC", distance));
            if (live.TryGetValue("Время работы", out var runtime))
                _body.Add(Row("Время работы двигателя", runtime));
            if (live.TryGetValue("Напряжение ECU", out var voltage))
                _body.Add(Row("Напряжение ECU", voltage));

            _status.Text = "Доступные стандартные данные прочитаны.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View Row(string title, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10
        };
        grid.Add(new Label { Text = title, FontSize = 11, TextColor = Theme.Muted }, 0, 0);
        grid.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, HorizontalOptions = LayoutOptions.End }, 1, 0);
        return Theme.CardView(grid, new Thickness(12));
    }
}