using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class AddVehiclePage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Entry _vin = Field("VIN");
    private readonly Entry _make = Field("Марка");
    private readonly Entry _model = Field("Модель");
    private readonly Entry _year = Field("Год", Keyboard.Numeric);
    private readonly Entry _plate = Field("Госномер");
    private readonly Entry _mileage = Field("Пробег, км", Keyboard.Numeric);
    private readonly Label _status = Theme.MutedText("Заполните известные данные.");

    public AddVehiclePage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Добавить авто";

        var save = Theme.PrimaryButton("Добавить автомобиль");
        save.Clicked += async (_, _) => await SaveAsync();

        var currentVin = Theme.SecondaryButton("Подставить VIN из последней диагностики");
        currentVin.Clicked += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_state.LastVin))
                _vin.Text = _state.LastVin;
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("VEHICLE DATABASE"),
                    Theme.H1("Добавить автомобиль"),
                    Theme.MutedText("Автомобиль будет сохранён на AutoDiag Server и появится и на ПК, и на iPhone."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { _vin, currentVin, _make, _model, _year, _plate, _mileage }
                    }),
                    save,
                    _status
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AccessPolicy.RequireStaffAsync(this);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_vin.Text) && string.IsNullOrWhiteSpace(_make.Text) && string.IsNullOrWhiteSpace(_model.Text))
        {
            _status.Text = "Укажите хотя бы VIN или марку/модель.";
            _status.TextColor = Theme.Accent;
            return;
        }

        try
        {
            _status.Text = "Сохраняю...";
            _status.TextColor = Theme.Accent;

            var id = await _api.CreateVehicleAsync(new ServerVehicleCreate
            {
                Vin = string.IsNullOrWhiteSpace(_vin.Text) ? null : _vin.Text.Trim().ToUpperInvariant(),
                Make = string.IsNullOrWhiteSpace(_make.Text) ? null : _make.Text.Trim(),
                Model = string.IsNullOrWhiteSpace(_model.Text) ? null : _model.Text.Trim(),
                Year = int.TryParse(_year.Text, out var year) ? year : null,
                Plate = string.IsNullOrWhiteSpace(_plate.Text) ? null : _plate.Text.Trim().ToUpperInvariant(),
                MileageKm = long.TryParse(_mileage.Text, out var mileage) ? mileage : null
            });

            var vehicles = await _api.GetVehiclesAsync();
            _state.Vehicles = vehicles;
            _state.SelectedVehicle = vehicles.FirstOrDefault(x => x.Id == id) ?? vehicles.LastOrDefault();

            _status.Text = "Автомобиль добавлен.";
            _status.TextColor = Theme.Green;
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static Entry Field(string placeholder, Keyboard? keyboard = null) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48
    };
}
