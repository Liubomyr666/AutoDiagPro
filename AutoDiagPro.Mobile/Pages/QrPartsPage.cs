using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class QrPartsPage : ContentPage
{
    private readonly QrScannerService _scanner = AppServices.Get<QrScannerService>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Entry _code = Field("QR / штрихкод");
    private readonly Entry _name = Field("Название детали");
    private readonly Entry _qty = Field("Количество", Keyboard.Numeric);
    private readonly Label _status = Theme.MutedText("Отсканируйте код детали.");
    private readonly VerticalStackLayout _recent = new() { Spacing = 9 };

    public QrPartsPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "QR детали";
        _qty.Text = "1";

        var scan = Theme.PrimaryButton("Сканировать камерой");
        scan.Clicked += async (_, _) => await ScanAsync(false);

        var photo = Theme.SecondaryButton("QR с фотографии");
        photo.Clicked += async (_, _) => await ScanAsync(true);

        var receive = Theme.SecondaryButton("Принять деталь");
        receive.Clicked += async (_, _) => await ReceiveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("PARTS RECEIVING"),
                    Theme.H1("QR-приём запчастей"),
                    Theme.MutedText("Код распознаётся системным Apple Vision и сохраняется локально с привязкой к выбранному автомобилю."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children = { scan, photo, _code, _name, _qty, receive }
                    }),
                    _status,
                    Theme.H2("Последние поступления"),
                    _recent
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRecentAsync();
    }

    private async Task ScanAsync(bool pick)
    {
        _status.Text = "Распознаю код...";
        _status.TextColor = Theme.Accent;

        try
        {
            var value = pick
                ? await _scanner.PickAndDecodeAsync()
                : await _scanner.CaptureAndDecodeAsync();

            if (string.IsNullOrWhiteSpace(value))
            {
                _status.Text = "QR/штрихкод не найден. Попробуйте снять ближе и без бликов.";
                _status.TextColor = Theme.Accent;
                return;
            }

            _code.Text = value;
            _status.Text = "Код распознан.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task ReceiveAsync()
    {
        if (string.IsNullOrWhiteSpace(_code.Text))
        {
            _status.Text = "Сначала отсканируйте или введите код.";
            _status.TextColor = Theme.Accent;
            return;
        }

        var db = await _store.LoadAsync();
        db.ReceivedParts.Add(new ReceivedPartMobile
        {
            VehicleId = _state.SelectedVehicle?.Id,
            Code = _code.Text.Trim(),
            Name = string.IsNullOrWhiteSpace(_name.Text) ? "Деталь" : _name.Text.Trim(),
            Quantity = int.TryParse(_qty.Text, out var qty) && qty > 0 ? qty : 1
        });
        await _store.SaveAsync(db);

        _status.Text = _state.SelectedVehicle is null
            ? "Деталь принята без привязки к авто."
            : $"Деталь привязана: {_state.SelectedVehicle.DisplayName}";
        _status.TextColor = Theme.Green;

        _code.Text = "";
        _name.Text = "";
        _qty.Text = "1";
        await LoadRecentAsync();
    }

    private async Task LoadRecentAsync()
    {
        _recent.Clear();
        var db = await _store.LoadAsync();
        foreach (var part in db.ReceivedParts.OrderByDescending(x => x.ReceivedAt).Take(15))
        {
            var vehicle = _state.Vehicles.FirstOrDefault(x => x.Id == part.VehicleId)?.DisplayName ?? "Без авто";
            _recent.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label { Text = $"{part.Name} ×{part.Quantity}", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                    Theme.MutedText(part.Code),
                    new Label { Text = $"{vehicle} • {part.ReceivedAt.LocalDateTime:dd.MM HH:mm}", FontSize = 11, TextColor = Theme.Accent }
                }
            }, new Thickness(12)));
        }

        if (_recent.Count == 0)
            _recent.Add(Theme.CardView(Theme.MutedText("Поступлений пока нет.")));
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