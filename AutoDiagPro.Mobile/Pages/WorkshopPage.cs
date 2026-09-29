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
    private readonly Label _statOpen = Value();
    private readonly Label _statRevenue = Value();
    private readonly Label _statAppointments = Value();
    private readonly Label _statClients = Value();
    private readonly Label _statUnpaid = Value();
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
                    BuildStats(),
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

    private View BuildStats()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        grid.Add(Stat("Открытые работы", _statOpen), 0, 0);
        grid.Add(Stat("Оплачено сегодня", _statRevenue), 1, 0);
        grid.Add(Stat("Записи сегодня", _statAppointments), 0, 1);
        grid.Add(Stat("Клиенты", _statClients), 1, 1);
        grid.Add(Stat("Неоплаченные счета", _statUnpaid), 0, 2);
        return new VerticalStackLayout { Spacing = 9, Children = { Theme.H2("Статистика СТО"), grid } };
    }

    private static View Stat(string title, Label value) =>
        Theme.CardView(new VerticalStackLayout { Spacing = 5, Children = { Theme.Eyebrow(title), value } }, new Thickness(13));

    private static Label Value() => new()
    {
        Text = "—",
        FontSize = 17,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text
    };

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

    private View BuildIntakeCard()
    {
        var accept = Theme.PrimaryButton("Принять автомобиль");
        accept.Clicked += async (_, _) => await CreateIntakeAsync();

        var before = Theme.SecondaryButton("Фото ДО");
        before.Clicked += async (_, _) => await CapturePhotoAsync("Before");

        var after = Theme.SecondaryButton("Фото ПОСЛЕ");
        after.Clicked += async (_, _) => await CapturePhotoAsync("After");

        var photoGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        photoGrid.Add(before, 0, 0);
        photoGrid.Add(after, 1, 0);

        var maintenance = Theme.SecondaryButton("Добавить ТО");
        maintenance.Clicked += async (_, _) => await AddMaintenanceAsync();

        var part = Theme.SecondaryButton("Установить деталь");
        part.Clicked += async (_, _) => await AddInstalledPartAsync();

        var serviceGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        serviceGrid.Add(maintenance, 0, 0);
        serviceGrid.Add(part, 1, 0);

        var numbers = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        numbers.Add(_intakeMileage, 0, 0);
        numbers.Add(_intakeFuel, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.Eyebrow("ПРИЁМКА АВТО"),
                Theme.H2("Зафиксировать состояние"),
                Theme.MutedText("Пробег, топливо, жалоба клиента, повреждения и фото ДО/ПОСЛЕ сохраняются в AutoDiag Cloud."),
                numbers,
                _intakeComplaint,
                _intakeDamage,
                accept,
                photoGrid,
                serviceGrid,
                _intakeStatus
            }
        }, new Thickness(14), 18);
    }

    private async Task CreateIntakeAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("Приёмка", "Сначала выберите автомобиль через поиск или раздел «Авто».", "OK");
            return;
        }

        long? mileage = long.TryParse(_intakeMileage.Text, out var mileageValue) ? mileageValue : vehicle.MileageKm;
        int? fuel = int.TryParse(_intakeFuel.Text, out var fuelValue) ? Math.Clamp(fuelValue, 0, 100) : null;
        var complaint = _intakeComplaint.Text?.Trim() ?? "";
        var damage = _intakeDamage.Text?.Trim() ?? "";

        _intakeStatus.Text = "Создаю приёмку...";
        _intakeStatus.TextColor = Theme.Accent;

        try
        {
            var title = string.IsNullOrWhiteSpace(complaint) ? "Приёмка автомобиля" : complaint;
            var workOrderId = await _api.CreateWorkOrderAsync(vehicle.Id, title);
            await _api.CreateIntakeAsync(vehicle.Id, workOrderId, mileage, fuel, complaint, damage);

            _intakeStatus.Text = "Автомобиль принят • заказ-наряд создан.";
            _intakeStatus.TextColor = Theme.Green;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _intakeStatus.Text = "Ошибка приёмки: " + ex.Message;
            _intakeStatus.TextColor = Theme.Red;
        }
    }

    private async Task AddMaintenanceAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("ТО", "Сначала выберите автомобиль.", "OK");
            return;
        }

        var name = await DisplayPromptAsync("План ТО", "Что обслужить? Например: моторное масло", "Далее", "Отмена");
        if (string.IsNullOrWhiteSpace(name)) return;

        var mileageText = await DisplayPromptAsync(
            "План ТО",
            "На каком пробеге выполнить? Можно оставить пустым.",
            "Сохранить",
            "Без пробега",
            keyboard: Keyboard.Numeric);

        long? dueMileage = long.TryParse(mileageText, out var mileage) ? mileage : null;

        try
        {
            await _api.CreateMaintenanceAsync(
                vehicle.Id,
                name.Trim(),
                dueMileage,
                null,
                vehicle.MileageKm,
                DateTimeOffset.Now,
                null);

            _intakeStatus.Text = "Пункт ТО добавлен в облачный план.";
            _intakeStatus.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            await DisplayAlert("ТО", ex.Message, "OK");
        }
    }

    private async Task AddInstalledPartAsync()
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("Деталь", "Сначала выберите автомобиль.", "OK");
            return;
        }

        var name = await DisplayPromptAsync("Установленная деталь", "Название детали", "Далее", "Отмена");
        if (string.IsNullOrWhiteSpace(name)) return;

        var partNumber = await DisplayPromptAsync("Установленная деталь", "OEM / артикул (необязательно)", "Далее", "Пропустить");
        var manufacturer = await DisplayPromptAsync("Установленная деталь", "Производитель (необязательно)", "Сохранить", "Пропустить");

        try
        {
            var order = (await _api.GetWorkOrdersAsync())
                .Where(x => x.VehicleId == vehicle.Id)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            await _api.AddInstalledPartAsync(
                vehicle.Id,
                order?.Id,
                name.Trim(),
                string.IsNullOrWhiteSpace(partNumber) ? null : partNumber.Trim(),
                string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer.Trim(),
                null,
                null,
                vehicle.MileageKm,
                DateTimeOffset.Now,
                null,
                _api.Session?.DisplayName);

            _intakeStatus.Text = "Деталь добавлена в историю автомобиля.";
            _intakeStatus.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Деталь", ex.Message, "OK");
        }
    }

    private async Task CapturePhotoAsync(string kind)
    {
        var vehicle = _state.SelectedVehicle;
        if (vehicle is null)
        {
            await DisplayAlert("Фото", "Сначала выберите автомобиль.", "OK");
            return;
        }

        try
        {
            FileResult? file;
            try
            {
                file = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = kind == "Before" ? "Фото до ремонта" : "Фото после ремонта"
                });
            }
            catch
            {
                file = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = kind == "Before" ? "Фото до ремонта" : "Фото после ремонта"
                });
            }

            if (file is null) return;

            await using var stream = await file.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            if (bytes.Length > 15 * 1024 * 1024)
            {
                await DisplayAlert("Фото", "Фото больше 15 МБ. Выберите снимок меньшего размера.", "OK");
                return;
            }

            var uploadBytes = bytes;
            var marked = false;
            var action = await DisplayActionSheet(
                "Фото повреждений",
                "Отмена",
                null,
                "Разметить повреждения",
                "Загрузить без разметки");

            if (string.IsNullOrWhiteSpace(action) || action == "Отмена")
                return;

            if (action == "Разметить повреждения")
            {
                var markup = new PhotoMarkupPage(bytes);
                await Navigation.PushModalAsync(new NavigationPage(markup));
                var edited = await markup.Completion;
                if (edited is null) return;
                uploadBytes = edited;
                marked = true;
            }

            var order = (await _api.GetWorkOrdersAsync())
                .Where(x => x.VehicleId == vehicle.Id)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var mime = marked
                ? "image/jpeg"
                : ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

            var note = kind == "Before" ? "Фото до ремонта" : "Фото после ремонта";
            if (marked) note += " • разметка повреждений";

            await _api.UploadPhotoAsync(
                vehicle.Id,
                order?.Id,
                kind,
                note,
                mime,
                uploadBytes);

            _intakeStatus.Text = kind == "Before" ? "Фото ДО загружено." : "Фото ПОСЛЕ загружено.";
            _intakeStatus.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _intakeStatus.Text = "Фото: " + ex.Message;
            _intakeStatus.TextColor = Theme.Red;
        }
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
                _state.PendingModuleTitle = "Сотрудники";
                await Shell.Current.GoToAsync("workshopmanager");
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

            await DisplayAlert("СТО", "Раздел недоступен в текущей мобильной сборке.", "OK");
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
            var listTask = _api.GetWorkOrdersAsync();
            var dashboardTask = _api.GetDashboardAsync();
            var appointmentsTask = _api.GetAppointmentsAsync();
            await Task.WhenAll(listTask, dashboardTask, appointmentsTask);
            var list = await listTask;
            var dashboard = await dashboardTask;
            var appointments = await appointmentsTask;
            _statOpen.Text = dashboard.ActiveOrders.ToString();
            _statRevenue.Text = dashboard.PaidToday.ToString("N2") + " €";
            _statAppointments.Text = appointments.Count(x =>
                x.StartsAt.LocalDateTime.Date == DateTime.Today &&
                !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Status, "Отменено", StringComparison.OrdinalIgnoreCase)).ToString();
            _statClients.Text = dashboard.Clients.ToString();
            _statUnpaid.Text = dashboard.UnpaidInvoices.ToString();

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

    private View OrderCard(ServerWorkOrderRecord order)
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
        header.Add(Theme.Pill(order.Status, Theme.Accent), 1, 0);

        var next = Theme.CompactButton("Следующий статус");
        next.Clicked += async (_, _) =>
        {
            var nextStatus = NextStatus(order.Status);
            try
            {
                await _api.UpdateWorkOrderAsync(order.Id, status: nextStatus);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("СТО", ex.Message, "OK");
            }
        };

        var estimate = Theme.CompactButton("Смета клиенту");
        estimate.Clicked += async (_, _) =>
        {
            var value = await DisplayPromptAsync(
                "Смета",
                "Введите итоговую сумму для согласования клиентом:",
                "Отправить",
                "Отмена",
                initialValue: order.TotalAmount > 0 ? order.TotalAmount.ToString("0.00") : "",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(value)) return;
            if (!decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var total))
            {
                await DisplayAlert("Смета", "Некорректная сумма.", "OK");
                return;
            }

            try
            {
                await _api.UpdateWorkOrderAsync(order.Id, totalAmount: total, estimateStatus: "WaitingClient");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Смета", ex.Message, "OK");
            }
        };

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        actions.Add(next, 0, 0);
        actions.Add(estimate, 1, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                header,
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(order.Title) ? "Без названия" : order.Title,
                    FontSize = 14,
                    TextColor = Theme.TextSoft
                },
                Theme.MutedText(amount + " • " + order.UpdatedAt.LocalDateTime.ToString("dd.MM HH:mm")),
                Theme.Pill("СМЕТА • " + FriendlyEstimate(order.EstimateStatus),
                    string.Equals(order.EstimateStatus, "Approved", StringComparison.OrdinalIgnoreCase) ? Theme.Green :
                    string.Equals(order.EstimateStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ? Theme.Red : Theme.Accent),
                actions
            }
        }, new Thickness(14));
    }

    private static string NextStatus(string? current)
    {
        var value = (current ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            "open" or "accepted" or "принято" => "Diagnostics",
            "diagnostics" or "диагностика" => "WaitingParts",
            "waitingparts" or "ожидание деталей" => "Repairing",
            "repairing" or "в ремонте" => "QualityCheck",
            "qualitycheck" or "проверка" => "Ready",
            "ready" or "готово" => "Completed",
            _ => "Diagnostics"
        };
    }

    private static string FriendlyEstimate(string? status)
    {
        var value = (status ?? "").Trim();
        if (value.Equals("WaitingClient", StringComparison.OrdinalIgnoreCase)) return "ЖДЁТ КЛИЕНТА";
        if (value.Equals("Approved", StringComparison.OrdinalIgnoreCase)) return "СОГЛАСОВАНА";
        if (value.Equals("Rejected", StringComparison.OrdinalIgnoreCase)) return "ОТКЛОНЕНА";
        return "НЕ ОТПРАВЛЕНА";
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

    private static Editor EditorField(string placeholder) => new()
    {
        Placeholder = placeholder,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 78,
        FontAutoScalingEnabled = false
    };

}