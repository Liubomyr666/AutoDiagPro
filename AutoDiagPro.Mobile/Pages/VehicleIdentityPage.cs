using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class VehicleIdentityPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly ApiService _api = AppServices.Get<ApiService>();
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
        decode.Clicked += async (_, _) => await DecodeAsync();

        var selected = Theme.SecondaryButton("VIN выбранного авто");
        selected.Clicked += (_, _) =>
        {
            _vin.Text = _state.SelectedVehicle?.Vin ?? "";
            _ = DecodeAsync();
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

    private async Task DecodeAsync()
    {
        var result = VehicleIdentityService.Decode(_vin.Text);
        _vin.Text = result.Vin;
        if (!result.IsValid)
        {
            _result.Text = result.Summary;
            _result.TextColor = Theme.Red;
            return;
        }

        _result.Text = result.Summary + "\n\nПроверяю заводские данные...";
        _result.TextColor = Theme.Accent;

        ServerVinDecodeRecord? deep = null;
        try
        {
            if (_api.IsLoggedIn)
                deep = await _api.DecodeVinAsync(result.Vin);
        }
        catch { }

        if (deep is null)
        {
            _result.Text = result.Summary + "\n\nГлубокие данные сейчас недоступны. Модель/двигатель/коробка не будут выдуманы.";
            _result.TextColor = Theme.Text;
            return;
        }

        static string V(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        var volume = string.IsNullOrWhiteSpace(deep.DisplacementL) ? "" : $" • {deep.DisplacementL} л";

        _result.Text =
            $"VIN: {result.Vin}\n" +
            $"Марка: {V(deep.Make)}\n" +
            $"Модель: {V(deep.Model)}\n" +
            $"Модельный год: {deep.ParsedYear?.ToString() ?? result.ModelYear?.ToString() ?? "—"}\n" +
            $"Двигатель: {V(deep.Engine)}{volume}\n" +
            $"Топливо: {V(deep.FuelType)}\n" +
            $"Коробка: {V(deep.Transmission)}\n" +
            $"Привод: {V(deep.DriveType)}\n" +
            $"Кузов: {V(deep.BodyClass)}\n" +
            $"Регион: {result.Country}\nWMI: {result.Wmi}\n" +
            $"Источник: {V(deep.Source)}";

        _result.TextColor = Theme.Text;
    }
}
