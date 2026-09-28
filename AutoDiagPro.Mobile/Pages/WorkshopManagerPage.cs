using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class WorkshopManagerPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly VerticalStackLayout _body = new() { Spacing = 12 };

    public WorkshopManagerPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "СТО";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("WORKSHOP MANAGER"),
                    _body
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
        await RenderAsync();
    }

    private async Task RenderAsync()
    {
        _body.Clear();
        var title = string.IsNullOrWhiteSpace(_state.PendingModuleTitle) ? "Клиенты / CRM" : _state.PendingModuleTitle;
        Title = title;
        _body.Add(Theme.H1(title));

        if (title == "Клиенты / CRM")
            await RenderClientsAsync();
        else if (title == "Запись")
            await RenderAppointmentsAsync();
        else if (title == "Счета / чеки")
            await RenderInvoicesAsync();
        else
            _body.Add(Theme.CardView(Theme.MutedText("Этот модуль управляется из общего Workshop workspace.")));
    }

    private async Task RenderClientsAsync()
    {
        _body.Add(Theme.MutedText("Облачная CRM AutoDiag: контакты клиента доступны на Windows и iPhone."));

        var name = Field("Имя клиента");
        var phone = Field("Телефон", Keyboard.Telephone);
        var email = Field("Email", Keyboard.Email);
        var notes = EditorField("Заметки");

        var add = Theme.PrimaryButton("Добавить клиента");
        add.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                await DisplayAlert("CRM", "Введите имя клиента.", "OK");
                return;
            }

            try
            {
                await _api.CreateClientAsync(
                    name.Text.Trim(),
                    phone.Text?.Trim(),
                    email.Text?.Trim(),
                    notes.Text?.Trim());
                await RenderAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("CRM", ex.Message, "OK");
            }
        };

        _body.Add(Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children = { name, phone, email, notes, add }
        }));

        var clients = await _api.GetClientsAsync();
        _body.Add(Theme.H2($"Клиенты • {clients.Count}"));

        foreach (var client in clients.OrderBy(x => x.FullName))
        {
            _body.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = client.FullName,
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Theme.Text
                    },
                    Theme.MutedText(string.Join(" • ", new[] { client.Phone, client.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))),
                    Theme.MutedText("ID • " + client.Id.ToString("N")[..8].ToUpperInvariant()),
                    string.IsNullOrWhiteSpace(client.Notes) ? Theme.MutedText("Без заметок") : Theme.Body(client.Notes)
                }
            }, new Thickness(13)));
        }
    }

    private async Task RenderAppointmentsAsync()
    {
        _body.Add(Theme.MutedText("Облачная запись клиента на сервис. Клиент увидит её в своём приложении."));

        var client = Field("Клиент");
        var work = Field("Работы / причина визита");
        var date = new DatePicker
        {
            Date = DateTime.Today,
            BackgroundColor = Theme.Surface,
            TextColor = Theme.Text
        };
        var time = new TimePicker
        {
            Time = DateTime.Now.AddHours(1).TimeOfDay,
            BackgroundColor = Theme.Surface,
            TextColor = Theme.Text
        };

        var add = Theme.PrimaryButton("Добавить запись");
        add.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(client.Text) || string.IsNullOrWhiteSpace(work.Text))
            {
                await DisplayAlert("Запись", "Введите клиента и работы.", "OK");
                return;
            }

            try
            {
                var starts = new DateTimeOffset(date.Date.Date + time.Time);
                await _api.CreateAppointmentAsync(
                    _state.SelectedVehicle?.Id,
                    client.Text.Trim(),
                    starts,
                    work.Text.Trim());
                await RenderAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Запись", ex.Message, "OK");
            }
        };

        _body.Add(Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                client, work,
                new Label { Text = "Дата", TextColor = Theme.Muted, FontSize = 11 }, date,
                new Label { Text = "Время", TextColor = Theme.Muted, FontSize = 11 }, time,
                add
            }
        }));

        var list = (await _api.GetAppointmentsAsync())
            .OrderBy(x => x.StartsAt)
            .ToList();

        if (_state.Vehicles.Count == 0)
            _state.Vehicles = await _api.GetVehiclesAsync();

        _body.Add(Theme.H2($"Записи • {list.Count}"));

        foreach (var item in list)
        {
            var done = Theme.CompactButton(
                string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                    ? "Готово ✓"
                    : "Отметить готово");

            done.Clicked += async (_, _) =>
            {
                try
                {
                    await _api.UpdateAppointmentAsync(item.Id, status: "Completed");
                    await RenderAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Запись", ex.Message, "OK");
                }
            };

            var vehicle = _state.Vehicles.FirstOrDefault(x => x.Id == item.VehicleId)?.DisplayName ?? "Без авто";
            _body.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = $"{item.StartsAt.LocalDateTime:dd.MM HH:mm} • {item.ClientName}",
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Theme.Text
                    },
                    Theme.Body(item.Work),
                    Theme.MutedText(vehicle + " • " + item.Status),
                    done
                }
            }, new Thickness(13)));
        }
    }

    private async Task RenderInvoicesAsync()
    {
        _body.Add(Theme.MutedText("Облачные счета и чеки выбранного автомобиля. Клиент видит их в сервисной истории."));

        var number = Field("Номер");
        number.Text = "INV-" + DateTime.Now.ToString("yyMMdd-HHmm");
        var description = Field("Работы / детали");
        var amount = Field("Сумма, €", Keyboard.Numeric);
        var paid = new Switch();
        var paidRow = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                paid,
                new Label
                {
                    Text = "Оплачено",
                    TextColor = Theme.Text,
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        var add = Theme.PrimaryButton("Создать счёт");
        add.Clicked += async (_, _) =>
        {
            if (!decimal.TryParse(
                    (amount.Text ?? "").Replace(',', '.'),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                await DisplayAlert("Счёт", "Введите сумму.", "OK");
                return;
            }

            try
            {
                var order = _state.SelectedVehicle is null
                    ? null
                    : (await _api.GetWorkOrdersAsync())
                        .Where(x => x.VehicleId == _state.SelectedVehicle.Id)
                        .OrderByDescending(x => x.UpdatedAt)
                        .FirstOrDefault();

                await _api.CreateInvoiceAsync(
                    _state.SelectedVehicle?.Id,
                    order?.Id,
                    string.IsNullOrWhiteSpace(number.Text) ? null : number.Text.Trim(),
                    description.Text?.Trim(),
                    value,
                    paid.IsToggled);

                await RenderAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Счёт", ex.Message, "OK");
            }
        };

        _body.Add(Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children = { number, description, amount, paidRow, add }
        }));

        var list = (await _api.GetInvoicesAsync(_state.SelectedVehicle?.Id))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        _body.Add(Theme.H2($"Счета • {list.Count}"));

        foreach (var invoice in list)
        {
            var toggle = Theme.CompactButton(invoice.Paid ? "Оплачено ✓" : "Отметить оплату");
            toggle.Clicked += async (_, _) =>
            {
                try
                {
                    await _api.UpdateInvoiceAsync(invoice.Id, paid: true);
                    await RenderAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Счёт", ex.Message, "OK");
                }
            };

            _body.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label
                            {
                                Text = invoice.Number,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Theme.Text
                            },
                            new Label
                            {
                                Text = $"{invoice.Amount:N2} €",
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Theme.Accent
                            }
                        }
                    },
                    Theme.Body(invoice.Description ?? ""),
                    Theme.MutedText(invoice.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm")),
                    toggle
                }
            }, new Thickness(13)));
        }
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

    private static Editor EditorField(string placeholder) => new()
    {
        Placeholder = placeholder,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 72
    };
}