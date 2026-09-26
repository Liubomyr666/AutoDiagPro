using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class WorkshopManagerPage : ContentPage
{
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
        _body.Add(Theme.MutedText("Локальная CRM на iPhone: контакты и заметки клиента. Автомобили хранятся на AutoDiag Server."));

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

            var db = await _store.LoadAsync();
            db.Clients.Add(new MobileClientRecord
            {
                Name = name.Text.Trim(),
                Phone = phone.Text?.Trim() ?? "",
                Email = email.Text?.Trim() ?? "",
                Notes = notes.Text?.Trim() ?? ""
            });
            await _store.SaveAsync(db);
            await RenderAsync();
        };

        _body.Add(Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children = { name, phone, email, notes, add }
        }));

        var data = await _store.LoadAsync();
        _body.Add(Theme.H2($"Клиенты • {data.Clients.Count}"));

        foreach (var client in data.Clients.OrderBy(x => x.Name))
        {
            var delete = Theme.CompactButton("Удалить");
            delete.TextColor = Theme.Red;
            delete.Clicked += async (_, _) =>
            {
                var yes = await DisplayAlert("CRM", $"Удалить {client.Name}?", "Удалить", "Отмена");
                if (!yes) return;
                var db = await _store.LoadAsync();
                db.Clients.RemoveAll(x => x.Id == client.Id);
                await _store.SaveAsync(db);
                await RenderAsync();
            };

            _body.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label { Text = client.Name, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                    Theme.MutedText(string.Join(" • ", new[] { client.Phone, client.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))),
                    string.IsNullOrWhiteSpace(client.Notes) ? Theme.MutedText("Без заметок") : Theme.Body(client.Notes),
                    delete
                }
            }, new Thickness(13)));
        }
    }

    private async Task RenderAppointmentsAsync()
    {
        _body.Add(Theme.MutedText("Запись клиента на сервис с привязкой к текущему автомобилю."));

        var client = Field("Клиент");
        var work = Field("Работы / причина визита");
        var date = new DatePicker { Date = DateTime.Today, BackgroundColor = Theme.Surface, TextColor = Theme.Text };
        var time = new TimePicker { Time = DateTime.Now.AddHours(1).TimeOfDay, BackgroundColor = Theme.Surface, TextColor = Theme.Text };

        var add = Theme.PrimaryButton("Добавить запись");
        add.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(client.Text) || string.IsNullOrWhiteSpace(work.Text))
            {
                await DisplayAlert("Запись", "Введите клиента и работы.", "OK");
                return;
            }

            var starts = date.Date.Date + time.Time;
            var db = await _store.LoadAsync();
            db.Appointments.Add(new MobileAppointmentRecord
            {
                VehicleId = _state.SelectedVehicle?.Id,
                ClientName = client.Text.Trim(),
                StartsAt = new DateTimeOffset(starts),
                Work = work.Text.Trim()
            });
            await _store.SaveAsync(db);
            await RenderAsync();
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

        var data = await _store.LoadAsync();
        var list = data.Appointments.OrderBy(x => x.StartsAt).ToList();
        _body.Add(Theme.H2($"Записи • {list.Count}"));

        foreach (var item in list)
        {
            var done = Theme.CompactButton(item.Status == "Готово" ? "Готово ✓" : "Отметить готово");
            done.Clicked += async (_, _) =>
            {
                var db = await _store.LoadAsync();
                var target = db.Appointments.FirstOrDefault(x => x.Id == item.Id);
                if (target is not null) target.Status = "Готово";
                await _store.SaveAsync(db);
                await RenderAsync();
            };

            var vehicle = _state.Vehicles.FirstOrDefault(x => x.Id == item.VehicleId)?.DisplayName ?? "Без авто";
            _body.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label { Text = $"{item.StartsAt.LocalDateTime:dd.MM HH:mm} • {item.ClientName}", FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                    Theme.Body(item.Work),
                    Theme.MutedText(vehicle + " • " + item.Status),
                    done
                }
            }, new Thickness(13)));
        }
    }

    private async Task RenderInvoicesAsync()
    {
        _body.Add(Theme.MutedText("Локальные счета/чеки для выбранного автомобиля."));

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
                new Label { Text = "Оплачено", TextColor = Theme.Text, VerticalTextAlignment = TextAlignment.Center }
            }
        };

        var add = Theme.PrimaryButton("Создать счёт");
        add.Clicked += async (_, _) =>
        {
            if (!decimal.TryParse((amount.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                await DisplayAlert("Счёт", "Введите сумму.", "OK");
                return;
            }

            var db = await _store.LoadAsync();
            db.Invoices.Add(new MobileInvoiceRecord
            {
                VehicleId = _state.SelectedVehicle?.Id,
                Number = string.IsNullOrWhiteSpace(number.Text) ? "INV" : number.Text.Trim(),
                Description = description.Text?.Trim() ?? "",
                Amount = value,
                Paid = paid.IsToggled
            });
            await _store.SaveAsync(db);
            await RenderAsync();
        };

        _body.Add(Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children = { number, description, amount, paidRow, add }
        }));

        var data = await _store.LoadAsync();
        var list = data.Invoices.OrderByDescending(x => x.CreatedAt).ToList();
        _body.Add(Theme.H2($"Счета • {list.Count}"));

        foreach (var invoice in list)
        {
            var toggle = Theme.CompactButton(invoice.Paid ? "Оплачено ✓" : "Отметить оплату");
            toggle.Clicked += async (_, _) =>
            {
                var db = await _store.LoadAsync();
                var target = db.Invoices.FirstOrDefault(x => x.Id == invoice.Id);
                if (target is not null) target.Paid = true;
                await _store.SaveAsync(db);
                await RenderAsync();
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
                            new Label { Text = invoice.Number, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                            new Label { Text = $"{invoice.Amount:N2} €", FontAttributes = FontAttributes.Bold, TextColor = Theme.Accent }
                        }
                    },
                    Theme.Body(invoice.Description),
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