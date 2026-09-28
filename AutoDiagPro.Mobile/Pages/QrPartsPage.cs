using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class QrPartsPage : ContentPage
{
    private readonly QrScannerService _scanner = AppServices.Get<QrScannerService>();
    private readonly ApiService _api = AppServices.Get<ApiService>();
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

        var receive = Theme.PrimaryButton("Принять в облачный склад");
        receive.Clicked += async (_, _) => await ReceiveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("PARTS RECEIVING • CLOUD"),
                    Theme.H1("QR-приём запчастей"),
                    Theme.MutedText("Код распознаётся Apple Vision и сразу сохраняется в общем складе AutoDiag Pro."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children = { scan, photo, _code, _name, _qty, receive }
                    }),
                    _status,
                    Theme.H2("Облачный склад"),
                    _recent
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
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
                _status.Text = "QR/штрихкод не найден. Снимите ближе и без бликов.";
                _status.TextColor = Theme.Accent;
                return;
            }

            _code.Text = value.Trim();
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
        var code = _code.Text?.Trim() ?? "";
        var name = _name.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code))
        {
            _status.Text = "Сначала отсканируйте или введите код.";
            _status.TextColor = Theme.Accent;
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            _status.Text = "Введите название детали.";
            _status.TextColor = Theme.Accent;
            return;
        }

        var quantity = int.TryParse(_qty.Text, out var qty) && qty > 0 ? qty : 1;

        try
        {
            _status.Text = "Принимаю в AutoDiag Cloud...";
            _status.TextColor = Theme.Accent;

            await _api.ReceiveInventoryAsync(
                _state.SelectedVehicle?.Id,
                code,
                name,
                quantity);

            _status.Text = _state.SelectedVehicle is null
                ? $"Принято: {name} ×{quantity} • общий склад"
                : $"Принято: {name} ×{quantity} • {_state.SelectedVehicle.DisplayName}";
            _status.TextColor = Theme.Green;

            _code.Text = "";
            _name.Text = "";
            _qty.Text = "1";
            await LoadRecentAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Приёмка: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task LoadRecentAsync()
    {
        _recent.Clear();
        try
        {
            var items = (await _api.GetInventoryAsync())
                .OrderByDescending(x => x.LastReceivedAt ?? x.UpdatedAt)
                .Take(15)
                .ToList();

            foreach (var part in items)
            {
                var vehicle = part.VehicleId is Guid id
                    ? _state.Vehicles.FirstOrDefault(x => x.Id == id)?.DisplayName ?? "Привязано к авто"
                    : "Общий склад";

                _recent.Add(Theme.CardView(new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label
                        {
                            Text = $"{part.Name} ×{part.Quantity}",
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Theme.Text
                        },
                        Theme.MutedText(part.Code),
                        new Label
                        {
                            Text = vehicle +
                                (part.LastReceivedAt is null ? "" : $" • {part.LastReceivedAt.Value.LocalDateTime:dd.MM HH:mm}"),
                            FontSize = 11,
                            TextColor = Theme.Accent
                        }
                    }
                }, new Thickness(12)));
            }

            if (_recent.Count == 0)
                _recent.Add(Theme.CardView(Theme.MutedText("Облачный склад пока пуст.")));
        }
        catch (Exception ex)
        {
            _recent.Add(Theme.CardView(Theme.MutedText("Не удалось загрузить склад: " + ex.Message)));
        }
    }

    private static Entry Field(string placeholder, Keyboard? keyboard = null) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48,
        FontAutoScalingEnabled = false
    };
}
