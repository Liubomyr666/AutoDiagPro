using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class LoginPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly Entry _email = new() { Placeholder = "Email", Keyboard = Keyboard.Email };
    private readonly Entry _password = new() { Placeholder = "Пароль", IsPassword = true };
    private readonly Switch _remember = new() { IsToggled = true };
    private readonly Button _login = Theme.PrimaryButton("Войти");
    private readonly Button _google = Theme.SecondaryButton("Войти через Google");
    private readonly Label _status = Theme.MutedText("Проверяю AutoDiag Server...");

    public LoginPage()
    {
        Title = "AutoDiag Pro";
        BackgroundColor = Theme.Page;

        StyleEntry(_email);
        StyleEntry(_password);
        _login.Clicked += LoginClicked;
        _google.Clicked += GoogleClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    BuildHero(),
                    Theme.CardView(BuildForm(), new Thickness(18))
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
            _status.Text = $"SERVER ONLINE • API v{health.Version}";
            _status.TextColor = Theme.Green;

            if (await _api.TryRestoreAsync())
            {
                App.OpenMain();
                return;
            }
        }
        catch
        {
            _status.Text = "Сервер недоступен. Проверь интернет.";
            _status.TextColor = Theme.Red;
        }
    }

    private View BuildHero()
    {
        var grid = new Grid { HeightRequest = 285 };
        grid.Add(new Image { Source = "login_car.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.60 });

        var logoRow = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Image { Source = "brandmark.png", WidthRequest = 52, HeightRequest = 52 },
                new VerticalStackLayout
                {
                    Spacing = 0,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = "AutoDiag PRO", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                        new Label { Text = "VEHICLE DIAGNOSTICS", FontSize = 9, TextColor = Theme.Muted, CharacterSpacing = 1.2 }
                    }
                }
            }
        };

        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(18),
            Spacing = 12,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                logoRow,
                Theme.Pill("IOS WORKSPACE"),
                new Label
                {
                    Text = "Диагностика автомобиля\nпрямо с iPhone",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White
                },
                new Label
                {
                    Text = "OBD • VIN • DTC • Live Data • AI • СТО",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#D3D9DD")
                }
            }
        });

        return new Border
        {
            Stroke = Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            Content = grid
        };
    }

    private View BuildForm()
    {
        var rememberRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10
        };
        rememberRow.Add(_remember, 0, 0);
        rememberRow.Add(new Label
        {
            Text = "Запомнить вход на этом iPhone",
            TextColor = Theme.TextSoft,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);

        return new VerticalStackLayout
        {
            Spacing = 13,
            Children =
            {
                Theme.Eyebrow("SECURE LOGIN"),
                Theme.H2("Вход в AutoDiag Pro"),
                _status,
                _email,
                _password,
                rememberRow,
                _login,
                new Label { Text = "или", TextColor = Theme.Muted, HorizontalTextAlignment = TextAlignment.Center, FontSize = 11 },
                _google,
                Theme.MutedText("Сессия хранится в защищённом iOS Keychain. Пароль не сохраняется в исходном коде приложения.")
            }
        };
    }

    private async void LoginClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            await DisplayAlert("AutoDiag", "Введите email и пароль.", "OK");
            return;
        }

        _login.IsEnabled = false;
        _login.Text = "Вход...";
        try
        {
            var session = await _api.LoginAsync(_email.Text, _password.Text, _remember.IsToggled);
            if (session.MustChangePassword)
                await DisplayAlert("AutoDiag", "Сервер требует сменить временный пароль.", "OK");

            App.OpenMain();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Не удалось войти", ex.Message, "OK");
        }
        finally
        {
            _login.IsEnabled = true;
            _login.Text = "Войти";
        }
    }

    private async void GoogleClicked(object? sender, EventArgs e)
    {
        _google.IsEnabled = false;
        _google.Text = "Открываю Google...";

        try
        {
            var start = await _api.StartExternalLoginAsync("google");
            await Browser.Default.OpenAsync(start.AuthorizationUrl, BrowserLaunchMode.SystemPreferred);

            _google.Text = "Жду вход через Google...";
            while (DateTimeOffset.UtcNow < start.ExpiresAtUtc)
            {
                await Task.Delay(1500);
                var status = await _api.GetExternalLoginStatusAsync(start.RequestId, _remember.IsToggled);

                if (string.Equals(status.Status, "complete", StringComparison.OrdinalIgnoreCase) && status.Session is not null)
                {
                    App.OpenMain();
                    return;
                }

                if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status.Status, "expired", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(status.Error ?? "Google-вход не завершён.");
            }

            throw new TimeoutException("Время Google-входа истекло. Попробуйте ещё раз.");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Google", ex.Message, "OK");
        }
        finally
        {
            _google.IsEnabled = true;
            _google.Text = "Войти через Google";
        }
    }

    private static void StyleEntry(Entry entry)
    {
        entry.BackgroundColor = Theme.Surface;
        entry.TextColor = Theme.Text;
        entry.PlaceholderColor = Theme.Muted;
        entry.HeightRequest = 50;
        entry.Margin = new Thickness(0);
    }
}