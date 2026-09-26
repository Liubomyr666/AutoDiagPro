using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class VehiclesPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly Label _status = Theme.MutedText("Загрузка...");
    private readonly Label _selectedTitle = new() { Text = "Автомобиль не выбран", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
    private readonly Label _selectedVin = Theme.MutedText("VIN • —");
    private readonly Label _selectedMileage = Theme.MutedText("Пробег • —");

    public VehiclesPage()
    {
        Title = "Авто";
        BackgroundColor = Theme.Page;

        _list.ItemTemplate = new DataTemplate(BuildVehicleCard);
        _list.SelectionChanged += VehicleSelected;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 92),
                Spacing = 14,
                Children =
                {
                    BuildHeader(),
                    BuildVehicleHero(),
                    Theme.H2("Мои автомобили"),
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View BuildHeader()
    {
        var refresh = Theme.CompactButton("Обновить");
        refresh.Clicked += async (_, _) => await LoadAsync();

        var add = Theme.CompactButton("+ Авто");
        add.Clicked += async (_, _) => await Shell.Current.GoToAsync("addvehicle");

        var actions = new HorizontalStackLayout { Spacing = 7, Children = { add, refresh } };

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children = { Theme.Eyebrow("VEHICLE WORKSPACE"), Theme.H1("Автомобиль"), _status }
        }, 0, 0);
        grid.Add(actions, 1, 0);
        return grid;
    }

    private View BuildVehicleHero()
    {
        var diag = Theme.PrimaryButton("Диагностика");
        diag.Clicked += async (_, _) => await Shell.Current.GoToAsync("//diagnostics");

        var history = Theme.SecondaryButton("История");
        history.Clicked += async (_, _) => await Shell.Current.GoToAsync("history");

        var ai = Theme.SecondaryButton("AI");
        ai.WidthRequest = 72;
        ai.Clicked += async (_, _) => await Shell.Current.GoToAsync("ai");

        var grid = new Grid { HeightRequest = 205 };
        grid.Add(new Image { Source = "hero_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.66 });
        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 7,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("CURRENT VEHICLE"),
                _selectedTitle,
                _selectedVin,
                _selectedMileage,
                new HorizontalStackLayout { Spacing = 8, Children = { diag, history, ai } }
            }
        });

        return new Border
        {
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = grid
        };
    }

    private static View BuildVehicleCard()
    {
        var title = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, FontAutoScalingEnabled = false };
        title.SetBinding(Label.TextProperty, nameof(ServerVehicleRecord.DisplayName));

        var vin = new Label { FontSize = 11, TextColor = Theme.Muted, FontAutoScalingEnabled = false };
        vin.SetBinding(Label.TextProperty, nameof(ServerVehicleRecord.Vin), stringFormat: "VIN • {0}");

        var mileage = new Label { FontSize = 12, TextColor = Theme.Accent, FontAttributes = FontAttributes.Bold, FontAutoScalingEnabled = false };
        mileage.SetBinding(Label.TextProperty, nameof(ServerVehicleRecord.MileageKm), stringFormat: "{0:N0} км");

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        grid.Add(new VerticalStackLayout { Spacing = 5, Children = { title, vin } }, 0, 0);
        grid.Add(mileage, 1, 0);

        return Theme.CardView(grid, new Thickness(14));
    }

    private async Task LoadAsync()
    {
        try
        {
            var vehicles = await _api.GetVehiclesAsync();
            _state.Vehicles = vehicles;

            if (_state.SelectedVehicle is null || vehicles.All(x => x.Id != _state.SelectedVehicle.Id))
                _state.SelectedVehicle = vehicles.FirstOrDefault();

            _list.ItemsSource = vehicles;
            _list.SelectedItem = _state.SelectedVehicle;
            _status.Text = vehicles.Count == 0 ? "К аккаунту пока не привязан автомобиль." : $"AutoDiag Server • {vehicles.Count} авто";
            _status.TextColor = vehicles.Count == 0 ? Theme.Accent : Theme.Green;
            RefreshSelected();
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
        RefreshSelected();
        await DisplayAlert("AutoDiag Pro", $"Активный автомобиль: {vehicle.DisplayName}", "OK");
    }

    private void RefreshSelected()
    {
        var v = _state.SelectedVehicle;
        _selectedTitle.Text = v?.DisplayName ?? "Автомобиль не выбран";
        _selectedVin.Text = "VIN • " + (string.IsNullOrWhiteSpace(v?.Vin) ? "—" : v!.Vin);
        _selectedMileage.Text = v?.MileageKm is null ? "Пробег • —" : $"Пробег • {v.MileageKm:N0} км";
    }
}