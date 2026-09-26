using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class HistoryPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly Label _status = Theme.MutedText("Загрузка истории...");

    public HistoryPage()
    {
        Title = "История";
        BackgroundColor = Theme.Page;
        _list.ItemTemplate = new DataTemplate(BuildScanCard);

        var root = new Grid
        {
            Padding = new Thickness(18, 24, 18, 20),
            RowSpacing = 14,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children = { Theme.H1("История диагностики"), _status }
        }, 0, 0);
        root.Add(_list, 0, 1);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }
    private async Task LoadAsync()
    {
        try
        {
            var scans = await _api.GetScansAsync();
            var selected = _state.SelectedVehicle;

            var filtered = selected is null
                ? scans
                : scans.Where(x =>
                    x.VehicleId == selected.Id ||
                    (!string.IsNullOrWhiteSpace(selected.Vin) &&
                     string.Equals(x.Vin, selected.Vin, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            filtered = filtered.OrderByDescending(x => x.ScannedAt).ToList();
            _list.ItemsSource = filtered;
            _status.Text = selected is null
                ? $"Все доступные scan • {filtered.Count}"
                : $"{selected.DisplayName} • {filtered.Count} scan";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View BuildScanCard()
    {
        var date = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text };
        date.SetBinding(Label.TextProperty, nameof(ServerScanRecord.ScannedAt), stringFormat: "{0:dd.MM.yyyy • HH:mm}");
        var dtc = new Label { FontSize = 12, TextColor = Theme.Accent };
        dtc.SetBinding(Label.TextProperty, nameof(ServerScanRecord.DtcCount), stringFormat: "DTC • {0}");

        var protocol = new Label { FontSize = 11, TextColor = Theme.Muted };
        protocol.SetBinding(Label.TextProperty, nameof(ServerScanRecord.Protocol), stringFormat: "Протокол • {0}");

        var summary = new Label
        {
            FontSize = 11,
            TextColor = Theme.Muted,
            MaxLines = 4,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        summary.SetBinding(Label.TextProperty, nameof(ServerScanRecord.Summary));

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children = { date, dtc, protocol, summary }
        }, new Thickness(15));
    }
}