using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class AdminPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly VerticalStackLayout _users = new() { Spacing = 9 };
    private readonly VerticalStackLayout _tenants = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Загрузка...");
    private readonly Entry _email = Field("Email");
    private readonly Entry _name = Field("Имя");
    private readonly Picker _role = new() { Title = "Роль", ItemsSource = new[] { "Client", "Mechanic", "Admin", "Owner" } };

    public AdminPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Пользователи";

        _role.BackgroundColor = Theme.Surface;
        _role.TextColor = Theme.Text;
        _role.SelectedIndex = 0;

        var create = Theme.PrimaryButton("Создать пользователя");
        create.Clicked += async (_, _) => await CreateUserAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("ACCESS CONTROL"),
                    Theme.H1("Пользователи / роли"),
                    Theme.MutedText("Работает через AutoDiag Server. Сервер дополнительно проверяет права текущего аккаунта."),
                    _status,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            Theme.Eyebrow("НОВЫЙ ПОЛЬЗОВАТЕЛЬ"),
                            _email, _name, _role, create
                        }
                    }),
                    Theme.H2("Пользователи"),
                    _users,
                    Theme.H2("Организации"),
                    _tenants
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireAdminAsync(this)) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _users.Clear();
        _tenants.Clear();
        try
        {
            var list = await _api.GetUsersAsync();
            foreach (var u in list.OrderBy(x => x.DisplayName))
                _users.Add(UserCard(u));

            if (_api.Session?.IsPlatformAdmin == true)
            {
                var tenants = await _api.GetTenantsAsync();
                foreach (var t in tenants.OrderBy(x => x.Name))
                    _tenants.Add(TenantCard(t));
            }
            else
            {
                _tenants.Add(Theme.CardView(Theme.MutedText("Организации доступны только platform admin.")));
            }

            _status.Text = $"Загружено пользователей: {list.Count}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
            _users.Add(Theme.CardView(Theme.MutedText("Раздел недоступен для текущей роли или сервер не отвечает.")));
        }
    }

    private View UserCard(ServerUserRecord user)
    {
        var active = Theme.CompactButton(user.IsActive ? "Отключить" : "Включить");
        active.Clicked += async (_, _) =>
        {
            try
            {
                await _api.UpdateUserAsync(user.Id, new ServerUserUpdate { IsActive = !user.IsActive });
                await LoadAsync();
            }
            catch (Exception ex) { await DisplayAlert("Пользователь", ex.Message, "OK"); }
        };

        var reset = Theme.CompactButton("Сбросить пароль");
        reset.Clicked += async (_, _) =>
        {
            var yes = await DisplayAlert("Сброс пароля", $"Создать временный пароль для {user.Email}?", "Сбросить", "Отмена");
            if (!yes) return;
            try
            {
                var result = await _api.ResetUserPasswordAsync(user.Id);
                await DisplayAlert("Временный пароль", result.TemporaryPassword, "OK");
            }
            catch (Exception ex) { await DisplayAlert("Сброс пароля", ex.Message, "OK"); }
        };

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = user.DisplayName, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText($"{user.Role} • {user.Email}"),
                Theme.Pill(user.IsActive ? "ACTIVE" : "DISABLED", user.IsActive ? Theme.Green : Theme.Red),
                new HorizontalStackLayout { Spacing = 8, Children = { active, reset } }
            }
        }, new Thickness(13));
    }

    private View TenantCard(ServerTenantRecord tenant)
    {
        var toggle = Theme.CompactButton(tenant.IsActive ? "Отключить" : "Включить");
        toggle.Clicked += async (_, _) =>
        {
            try
            {
                await _api.SetTenantActiveAsync(tenant.Id, !tenant.IsActive);
                await LoadAsync();
            }
            catch (Exception ex) { await DisplayAlert("Организация", ex.Message, "OK"); }
        };

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = tenant.Name, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText($"Пользователей: {tenant.UsersCount} • Авто: {tenant.VehiclesCount} • Scans: {tenant.ScansCount}"),
                toggle
            }
        }, new Thickness(13));
    }

    private async Task CreateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_name.Text))
        {
            await DisplayAlert("Пользователь", "Введите email и имя.", "OK");
            return;
        }

        try
        {
            var created = await _api.CreateUserAsync(new ServerUserCreate
            {
                Email = _email.Text.Trim(),
                DisplayName = _name.Text.Trim(),
                Role = _role.SelectedItem?.ToString() ?? "Client"
            });

            await DisplayAlert("Пользователь создан",
                $"Email: {created.Email}\nРоль: {created.Role}\nВременный пароль: {created.TemporaryPassword}",
                "OK");

            _email.Text = "";
            _name.Text = "";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Пользователь", ex.Message, "OK");
        }
    }

    private static Entry Field(string placeholder) => new()
    {
        Placeholder = placeholder,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48
    };
}
