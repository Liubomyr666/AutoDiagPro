using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly MobileReleaseService _releases = new();
    private readonly Label _release = Theme.MutedText("Проверка версии...");

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

        var backup = DarkButton("Создать резервную копию");
        backup.Clicked += BackupClicked;

        var restore = DarkButton("Восстановить из копии");
        restore.Clicked += RestoreClicked;

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
                    BuildUpdateCard(),
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
                            new Label { Text = "РЕЗЕРВНАЯ КОПИЯ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                            Theme.MutedText("CRM, записи, счета, Repair Brain, ТО, локальные уведомления и offline-очередь."),
                            backup, restore
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
        await CheckReleaseAsync(showDialog: false);
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

    private View BuildUpdateCard()
    {
        var check = DarkButton("Проверить обновление");
        check.Clicked += async (_, _) => await CheckReleaseAsync(showDialog: true);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                new Label { Text = "ОБНОВЛЕНИЯ", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText($"Установлено: iOS {AppInfo.Current.VersionString} • build {AppInfo.Current.BuildString}"),
                _release,
                check,
                Theme.MutedText("На iPhone установка новой версии идёт через TestFlight/App Store после включения Apple Developer distribution.")
            }
        });
    }

    private async Task CheckReleaseAsync(bool showDialog)
    {
        _release.Text = "Проверяю актуальную iOS-версию...";
        _release.TextColor = Theme.Muted;

        try
        {
            var check = await _releases.CheckAsync();
            if (check.UpdateAvailable)
            {
                _release.Text =
                    $"Доступна iOS {check.LatestVersion} • build {check.LatestBuild}\n{check.Notes}";
                _release.TextColor = Theme.Accent;
                if (showDialog)
                    await DisplayAlert("Обновление AutoDiag Pro",
                        $"Доступна iOS {check.LatestVersion} build {check.LatestBuild}.\n\n{check.Distribution}",
                        "OK");
            }
            else
            {
                _release.Text =
                    $"Версия актуальна • iOS {check.CurrentVersion} build {check.CurrentBuild} • Server {check.ServerVersion}";
                _release.TextColor = Theme.Green;
                if (showDialog)
                    await DisplayAlert("AutoDiag Pro", "Установлена актуальная iOS-версия.", "OK");
            }
        }
        catch (Exception ex)
        {
            _release.Text = "Проверка версии недоступна: " + ex.Message;
            _release.TextColor = Theme.Red;
            if (showDialog)
                await DisplayAlert("Обновление", ex.Message, "OK");
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
    private async void BackupClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = await _store.ExportBackupAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "AutoDiag Pro Backup",
                File = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Резервная копия", ex.Message, "OK");
        }
    }

    private async void RestoreClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Выберите AutoDiag backup JSON"
            });
            if (file is null) return;
            await _store.ImportBackupAsync(file.FullPath);
            await DisplayAlert("Резервная копия", "Данные восстановлены.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Резервная копия", ex.Message, "OK");
        }
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
