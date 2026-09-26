using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class VehiclesPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly Label _status = Theme.MutedText("Загрузка...");

    public VehiclesPage()
    {
        Title = "Автомобили";
        BackgroundColor = Theme.Page;

        _list.ItemTemplate = new DataTemplate(BuildVehicleCard);
        _list.SelectionChanged += VehicleSelected;

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Padding = new Thickness(18, 24, 18, 20),
            RowSpacing = 14
        };
        root.Add(BuildHeader(), 0, 0);
        root.Add(_list, 0, 1);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }
    private View BuildHeader()
    {
        var refresh = new Button
        {
            Text = "Обновить",
            BackgroundColor = Color.FromArgb("#1B242A"),
            TextColor = Theme.Text,
            CornerRadius = 10
        };
        refresh.Clicked += async (_, _) => await LoadAsync();

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Theme.H1("Мои автомобили"),
                _status
            }
        }, 0, 0);

        grid.Add(refresh, 1, 0);
        return grid;
    }

    private static View BuildVehicleCard()
    {
        var title = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
        title.SetBinding(Label.TextProperty, nameof(ServerVehicleRecord.DisplayName));
        var vin = new Label { FontSize = 11, TextColor = Theme.Muted };
        vin.SetBinding(Label.TextProperty, nameof(ServerVehicleRecord.Vin), stringFormat: "VIN • {0}");

        var mileage = new Label { FontSize = 12, TextColor = Theme.Accent };
        mileage.SetBinding(Label.TextProperty, nameof(ServerVehicleRecord.MileageKm), stringFormat: "Пробег • {0:N0} км");

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children = { title, vin, mileage }
        }, new Thickness(16));
    }

    private async Task LoadAsync()
    {
        try
        {
            var vehicles = await _api.GetVehiclesAsync();
            _state.Vehicles = vehicles;
            _state.SelectedVehicle ??= vehicles.FirstOrDefault();

            _list.ItemsSource = vehicles;
            _list.SelectedItem = _state.SelectedVehicle;
            _status.Text = vehicles.Count == 0
                ? "К аккаунту пока не привязан автомобиль."
                : $"С сервера • {vehicles.Count} авто";
            _status.TextColor = vehicles.Count == 0 ? Theme.Accent : Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async void VehicleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ServerVehicleRecord vehicle) return;
        _state.SelectedVehicle = vehicle;
        await DisplayAlert("AutoDiag", $"Выбран: {vehicle.DisplayName}", "OK");
    }
}