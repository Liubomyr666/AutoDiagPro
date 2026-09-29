using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class VehicleIdentityPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly Entry _vin = new()
    {
        Placeholder = "VIN • 17 символов",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 50,
        CharacterSpacing = 1
    };
    private readonly Label _result = Theme.MutedText("Введите VIN или используйте VIN выбранного автомобиля.");

    public VehicleIdentityPage()
    {
        Title = "Распознавание VIN";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        var decode = Theme.PrimaryButton("Распознать автомобиль");
        decode.Clicked += (_, _) => Decode();

        var selected = Theme.SecondaryButton("VIN выбранного авто");
        selected.Clicked += (_, _) =>
        {
            _vin.Text = _state.SelectedVehicle?.Vin ?? "";
            Decode();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 36),
                Spacing = 13,
                Children =
                {
                    Theme.Eyebrow("VIN / VEHICLE ID"),
                    Theme.H1("Автоопределение автомобиля"),
                    Theme.MutedText("Локально определяет WMI, марку, регион и модельный год. Двигатель/коробка подтверждаются через ECU и заводские данные."),
                    _vin,
                    decode,
                    selected,
                    Theme.CardView(_result, new Thickness(14), 16)
                }
            }
        };

        _vin.Text = _state.SelectedVehicle?.Vin ?? "";
    }

    private void Decode()
    {
        var result = VehicleIdentityService.Decode(_vin.Text);
        _vin.Text = result.Vin;
        _result.Text = result.Summary;
        _result.TextColor = result.IsValid ? Theme.Text : Theme.Red;
    }
}
