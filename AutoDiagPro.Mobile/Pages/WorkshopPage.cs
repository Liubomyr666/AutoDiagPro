using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;
using Microsoft.Maui.Media;

namespace AutoDiagPro.Mobile.Pages;

public sealed class WorkshopPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly VerticalStackLayout _orders = new() { Spacing = 10 };
    private readonly VerticalStackLayout _searchResults = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Загрузка СТО...");
    private readonly Label _searchStatus = Theme.MutedText("Поиск по телефону / ID клиента, VIN или госномеру.");
    private readonly Entry _search = new()
    {
        Placeholder = "Телефон, ID клиента, VIN или госномер",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48,
        ReturnType = ReturnType.Search,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
    private readonly Entry _intakeMileage = Field("Пробег, км", Keyboard.Numeric);
    private readonly Entry _intakeFuel = Field("Топливо, %", Keyboard.Numeric);
    private readonly Editor _intakeComplaint = EditorField("Жалоба клиента / причина обращения");
    private readonly Editor _intakeDamage = EditorField("Повреждения при приёмке");
    private readonly Label _intakeStatus = Theme.MutedText("Выберите автомобиль для приёмки.");

    public WorkshopPage()
    {
        Title = "СТО";
        BackgroundColor = Theme.Page;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    BuildHeader(),
                    BuildHero(),
                    BuildSearch(),
                    BuildIntakeCard(),
                    BuildModules(),
                    Theme.H2("Заказ-наряды"),
                    _orders
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
        await LoadAsync();
    }

    private View BuildHeader()
    {
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Theme.Eyebrow("WORKSHOP PRO"),
                Theme.H1("Управление СТО"),
                Theme.MutedText("Клиенты, заказ-наряды, склад, запись, сотрудники и документы."),
                _status
            }
        };
    }

    private View BuildHero()
    {
        var grid = new Grid { HeightRequest = 170 };
        grid.Add(new Image { Source = "adapter.jpg", Aspect = Aspect.AspectFill });
        grid.Add(new BoxView { Color = Theme.Page, Opacity = 0.72 });
        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 7,
            VerticalOptions = LayoutOptions.End,
            Children =
            {
                Theme.Pill("WORKSHOP CLOUD", Theme.Green),
                new Label { Text = "СТО всегда под рукой", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = "Заказы и выбранный автомобиль синхронизируются с AutoDiag Server.", FontSize = 12, TextColor = Theme.TextSoft }
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

    private View BuildSearch()
    {
        var button = Theme.PrimaryButton("Найти");
        button.WidthRequest = 86;
        button.Clicked += async (_, _) => await SearchAsync();

        _search.Completed += async (_, _) => await SearchAsync();
        _search.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_search.Text)) return;
            _searchResults.Clear();
            _searchStatus.Text = "Поиск по телефону / ID клиента, VIN или госномеру.";
            _searchStatus.TextColor = Theme.Muted;
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        row.Add(_search, 0, 0);
        row.Add(button, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.Eyebrow("БЫСТРЫЙ ПОИСК"),
                Theme.H2("Найти клиента или автомобиль"),
                Theme.MutedText("Ищет сразу по телефону, внутреннему ID клиента, VIN и госномеру автомобиля."),
                row,
                _searchStatus,
                _searchResults
            }
        }, new Thickness(14), 18);
    }

    private async Task SearchAsync()
    {
        var query = _search.Text?.Trim() ?? "";
        _searchResults.Clear();

        if (query.Length < 2)
        {
            _searchStatus.Text = "Введите минимум 2 символа.";
            _searchStatus.TextColor = Theme.Accent;
            return;
        }

        _searchStatus.Text = "Ищу на AutoDiag Server...";
        _searchStatus.TextColor = Theme.Muted;

        try
        {
            var searchTask = _api.SearchWorkshopAsync(query);
            var vehiclesTask = _api.GetVehiclesAsync();
            await Task.WhenAll(searchTask, vehiclesTask);

            var result = await searchTask;
            _state.Vehicles = await vehiclesTask;

            foreach (var client in result.Clients)
                _searchResults.Add(ClientSearchCard(client));

            foreach (var vehicle in result.Vehicles)
                _searchResults.Add(VehicleSearchCard(vehicle));

            var total = result.Clients.Count + result.Vehicles.Count;
            _searchStatus.Text = total == 0
                ? "Ничего не найдено."
                : $"Найдено: клиентов {result.Clients.Count}, автомобилей {result.Vehicles.Count}.";
            _searchStatus.TextColor = total == 0 ? Theme.Accent : Theme.Green;
        }
        catch (Exception ex)
        {
            _searchStatus.Text = "Ошибка поиска: " + ex.Message;
            _searchStatus.TextColor = Theme.Red;
        }
    }

    private View ClientSearchCard(ServerWorkshopClientRecord client)
    {
        var contacts = string.Join(" • ", new[] { client.Phone, client.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return Theme.SoftCard(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Theme.Pill("КЛИЕНТ • CLOUD", Theme.Green),
                new Label
                {
                    Text = client.FullName,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText(string.IsNullOrWhiteSpace(contacts) ? "Контакты не указаны" : contacts),
                Theme.MutedText("ID • " + client.Id.ToString("N")[..8].ToUpperInvariant()),
                string.IsNullOrWhiteSpace(client.Notes) ? Theme.MutedText("Без заметок") : Theme.Body(client.Notes)
            }
        }, new Thickness(12));
    }

    private View VehicleSearchCard(ServerWorkshopVehicleRecord vehicle)
    {
        var open = Theme.CompactButton("Открыть автомобиль");
        open.Clicked += async (_, _) =>
        {
            _state.SelectedVehicle = _state.Vehicles.FirstOrDefault(x => x.Id == vehicle.Id) ?? vehicle;
            _status.Text = vehicle.DisplayName + " • выбран";
            _status.TextColor = Theme.Green;
            _intakeStatus.Text = "Выбран: " + vehicle.DisplayName;
            _intakeStatus.TextColor = Theme.Green;
            await Shell.Current.GoToAsync("//vehicles");
        };

        var vin = string.IsNullOrWhiteSpace(vehicle.Vin) ? "VIN • —" : "VIN • " + vehicle.Vin;
        var plate = string.IsNullOrWhiteSpace(vehicle.Plate) ? "Госномер • —" : "Госномер • " + vehicle.Plate;
        var owner = string.IsNullOrWhiteSpace(vehicle.ClientName)
            ? "Клиент • не привязан"
            : "Клиент • " + vehicle.ClientName +
              (string.IsNullOrWhiteSpace(vehicle.ClientPhone) ? "" : " • " + vehicle.ClientPhone);

        return Theme.SoftCard(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Theme.Pill("АВТО • CLOUD", Theme.Accent),
                new Label
                {
                    Text = vehicle.DisplayName,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText(vin),
                Theme.MutedText(plate),
                Theme.MutedText(owner),
                open
            }
        }, new Thickness(12));
    }

    private View BuildModules()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        var modules = new[]
        {
            ("Клиенты / CRM", "Авто, контакты, история"),
            ("Запись", "Календарь и приём"),
            ("Склад", "Запчасти и остатки"),
            ("Сотрудники", "Механики и роли"),
            ("Счета / чеки", "Документы и суммы"),
            ("Фото", "До / после ремонта"),
            ("QR детали", "Приём запчастей"),
            ("Отчёты", "Работы и история")
        };

        for (var i = 0; i < modules.Length; i++)
        {
            var m = modules[i];
            grid.Add(ModuleCard(m.Item1, m.Item2), i % 2, i / 2);
        }

        return grid;
    }

    private View ModuleCard(string title, string subtitle)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (title == "QR детали")
            {
                await Shell.Current.GoToAsync("qrparts");
                return;
            }
            if (title == "Сотрудники")
            {
                await Shell.Current.GoToAsync("admin");
                return;
            }
            if (title == "Отчёты")
            {
                await Shell.Current.GoToAsync("reports");
                return;
            }
            if (title == "Склад")
            {
                await Shell.Current.GoToAsync("inventory");
                return;
            }
            if (title == "Фото")
            {
                await Shell.Current.GoToAsync("repair");
                return;
            }
            if (title is "Клиенты / CRM" or "Запись" or "Счета / чеки")
            {
                _state.PendingModuleTitle = title;
                await Shell.Current.GoToAsync("workshopmanager");
                return;
            }

            _state.PendingModuleTitle = title;
            _state.PendingModuleSubtitle = "Управление СТО";
            _state.PendingModuleBody = $"{subtitle}. Раздел синхронизирован с общей архитектурой AutoDiag Pro. Функции, для которых серверный API уже доступен, работают напрямую; остальные данные сохраняются в мобильном workspace.";
            await Shell.Current.GoToAsync("module");
        };

        var card = Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                Theme.MutedText(subtitle),
                new Label { Text = "Открыть ›", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Theme.Accent }
            }
        }, new Thickness(13));
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task LoadAsync()
    {
        _orders.Clear();
        try
        {
            var list = await _api.GetWorkOrdersAsync();
            var selected = _state.SelectedVehicle;
            if (selected is not null)
                list = list.Where(x => x.VehicleId == selected.Id).ToList();

            var active = list.OrderByDescending(x => x.UpdatedAt).Take(8).ToList();
            _status.Text = selected is null
                ? $"ONLINE • {active.Count} последних заказов"
                : $"{selected.DisplayName} • {active.Count} заказов";
            _status.TextColor = Theme.Green;

            if (active.Count == 0)
            {
                _orders.Add(Theme.CardView(Theme.MutedText("Заказ-нарядов пока нет.")));
                return;
            }

            foreach (var order in active)
                _orders.Add(OrderCard(order));
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
            _orders.Add(Theme.CardView(Theme.MutedText("Не удалось загрузить заказ-наряды.")));
        }
    }

    private static View OrderCard(ServerWorkOrderRecord order)
    {
        var amount = order.TotalAmount <= 0 ? "Сумма не указана" : $"{order.TotalAmount:N2} €";
        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10
        };
        header.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(order.Number) ? "Заказ-наряд" : order.Number,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text
        }, 0, 0);
        header.Add(new Label
        {
            Text = order.Status,
            TextColor = Theme.Accent,
            FontSize = 11,
            HorizontalOptions = LayoutOptions.End
        }, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                header,
                new Label { Text = string.IsNullOrWhiteSpace(order.Title) ? "Без названия" : order.Title, FontSize = 14, TextColor = Theme.TextSoft },
                new Label { Text = amount + " • " + order.UpdatedAt.LocalDateTime.ToString("dd.MM HH:mm"), FontSize = 11, TextColor = Theme.Muted }
            }
        }, new Thickness(14));
    }
}