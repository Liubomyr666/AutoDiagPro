using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class LoginPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly Entry _email = new() { Placeholder = "Email", Keyboard = Keyboard.Email };
    private readonly Entry _password = new() { Placeholder = "Пароль", IsPassword = true };
    private readonly Switch _remember = new() { IsToggled = true };
    private readonly Button _login = new() { Text = "Войти" };
    private readonly Button _google = new() { Text = "Войти через Google" };
    private readonly Label _status = Theme.MutedText("Проверяю AutoDiag Server...");

    public LoginPage()
    {
        Title = "AutoDiag Pro";
        BackgroundColor = Theme.Page;

        StyleEntry(_email);
        StyleEntry(_password);
        StyleButton(_login);
        StyleSecondaryButton(_google);
        _login.Clicked += LoginClicked;
        _google.Clicked += GoogleClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 60, 24, 30),
                Spacing = 18,
                Children =
                {
                    BuildHeader(),
                    Theme.CardView(BuildForm())
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
            _status.Text = $"Сервер ONLINE • API v{health.Version}";
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

    private View BuildHeader()
    {
        return new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 10,
            Children =
            {
                new Image { Source = "brandmark.svg", WidthRequest = 112, HeightRequest = 112 },
                new Label
                {
                    Text = "AutoDiag PRO",
                    FontSize = 30,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                Theme.MutedText("Диагностика автомобиля прямо с iPhone")
            }
        };
    }
    private View BuildForm()
    {
        var rememberRow = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                _remember,
                new Label
                {
                    Text = "Запомнить вход на этом iPhone",
                    TextColor = Theme.Text,
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        return new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Theme.H1("Вход"),
                _status,
                _email,
                _password,
                rememberRow,
                _login,
                new Label { Text = "или", TextColor = Theme.Muted, HorizontalTextAlignment = TextAlignment.Center },
                _google,
                Theme.MutedText("Данные сессии хранятся в защищённом iOS Keychain.")
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
            {
                await DisplayAlert("AutoDiag", "Сервер требует сменить временный пароль. Это можно сделать в Windows-клиенте или через администратора.", "OK");
            }

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

                if (string.Equals(status.Status, "complete", StringComparison.OrdinalIgnoreCase) &&
                    status.Session is not null)
                {
                    App.OpenMain();
                    return;
                }

                if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status.Status, "expired", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(status.Error ?? "Google-вход не завершён.");
                }
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

    private static void StyleSecondaryButton(Button button)
    {
        button.BackgroundColor = Color.FromArgb("#1B242A");
        button.TextColor = Theme.Text;
        button.CornerRadius = 12;
        button.HeightRequest = 50;
        button.BorderColor = Theme.Line;
        button.BorderWidth = 1;
    }

    private static void StyleEntry(Entry entry)
    {
        entry.BackgroundColor = Color.FromArgb("#12171A");
        entry.TextColor = Theme.Text;
        entry.PlaceholderColor = Theme.Muted;
        entry.HeightRequest = 50;
        entry.Margin = new Thickness(0);
    }

    private static void StyleButton(Button button)
    {
        button.BackgroundColor = Theme.Accent;
        button.TextColor = Color.FromArgb("#111315");
        button.FontAttributes = FontAttributes.Bold;
        button.CornerRadius = 12;
        button.HeightRequest = 50;
    }
}