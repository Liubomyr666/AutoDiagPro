using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();

    private readonly Entry _host = new()
    {
        Text = Preferences.Default.Get("obd_host", "192.168.0.10"),
        Placeholder = "IP OBD"
    };
    private readonly Entry _port = new()
    {
        Text = Preferences.Default.Get("obd_port", 35000).ToString(),
        Placeholder = "Порт",
        Keyboard = Keyboard.Numeric
    };
    private readonly Entry _partsRegion = new()
    {
        Text = Preferences.Default.Get("parts_region", "Германия / ЕС"),
        Placeholder = "Регион поиска запчастей"
    };
    private readonly Label _server = Theme.MutedText("Проверка сервера...");

    public SettingsPage()
    {
        Title = "Настройки";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        StyleEntry(_host);
        StyleEntry(_port);
        StyleEntry(_partsRegion);

        var save = DarkButton("Сохранить OBD настройки");
        save.Clicked += SaveClicked;

        var logout = DarkButton("Выйти из аккаунта");
        logout.TextColor = Theme.Red;
        logout.Clicked += LogoutClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 24, 18, 40),
                Spacing = 14,
                Children =
                {
                    Theme.H1("Настройки"),
                    BuildAccountCard(),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "OBD ПО WI-FI", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                            _host, _port, save
                        }
                    }),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            new Label { Text = "ПОИСК ЗАПЧАСТЕЙ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                            Theme.MutedText("AI использует этот регион при поиске магазинов и цен."),
                            _partsRegion
                        }
                    }),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            new Label { Text = "AUTODIAG SERVER", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                            Theme.MutedText($"AutoDiag Pro iOS v{AppInfo.Current.VersionString} • build {AppInfo.Current.BuildString}"),
                            _server,
                            logout
                        }
                    })
                }
            }
        };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var health = await _api.GetHealthAsync();
            _server.Text = $"ONLINE • API v{health.Version}";
            _server.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _server.Text = ex.Message;
            _server.TextColor = Theme.Red;
        }
    }

    private View BuildAccountCard()
    {
        var s = _api.Session;
        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = "АККАУНТ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                new Label { Text = s?.DisplayName ?? "—", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText(s?.Email ?? "—"),
                new Label { Text = s?.Role ?? "—", FontSize = 12, TextColor = Theme.Accent }
            }
        });
    }

    private async void SaveClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(_port.Text, out var port))
        {
            await DisplayAlert("AutoDiag", "Некорректный порт.", "OK");
            return;
        }

        Preferences.Default.Set("obd_host", _host.Text?.Trim() ?? "192.168.0.10");
        Preferences.Default.Set("obd_port", port);
        Preferences.Default.Set("parts_region",
            string.IsNullOrWhiteSpace(_partsRegion.Text) ? "Германия / ЕС" : _partsRegion.Text.Trim());
        await DisplayAlert("AutoDiag", "Настройки сохранены.", "OK");
    }
    private async void LogoutClicked(object? sender, EventArgs e)
    {
        var yes = await DisplayAlert("AutoDiag", "Выйти из аккаунта?", "Выйти", "Отмена");
        if (!yes) return;

        await _api.LogoutAsync();
        App.OpenLogin();
    }

    private static Button DarkButton(string text) =>
        new()
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#1B242A"),
            TextColor = Theme.Text,
            CornerRadius = 12,
            HeightRequest = 46
        };

    private static void StyleEntry(Entry entry)
    {
        entry.BackgroundColor = Color.FromArgb("#0E1316");
        entry.TextColor = Theme.Text;
        entry.PlaceholderColor = Theme.Muted;
        entry.HeightRequest = 48;
    }
}
