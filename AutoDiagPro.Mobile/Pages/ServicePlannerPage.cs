using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ServicePlannerPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();

    private readonly Label _vehicle = Theme.MutedText("Автомобиль не выбран");
    private readonly Entry _current = new() { Placeholder = "Текущий пробег, км", Keyboard = Keyboard.Numeric };
    private readonly Entry _nextMileage = new() { Placeholder = "Следующее ТО, км", Keyboard = Keyboard.Numeric };
    private readonly DatePicker _nextDate = new() { Format = "dd.MM.yyyy" };
    private readonly Entry _oil = new() { Placeholder = "Масло / спецификация" };
    private readonly Editor _notes = new() { Placeholder = "Что делать на следующем ТО", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 90 };
    private readonly Label _status = Theme.MutedText("Готово.");
    private ServicePlanMobile? _plan;

    public ServicePlannerPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Сервис / ТО";

        foreach (var e in new[] { _current, _nextMileage, _oil })
        {
            e.BackgroundColor = Theme.Surface;
            e.TextColor = Theme.Text;
            e.PlaceholderColor = Theme.Muted;
            e.HeightRequest = 48;
        }
        _nextDate.BackgroundColor = Theme.Surface;
        _nextDate.TextColor = Theme.Text;
        _notes.BackgroundColor = Theme.Surface;
        _notes.TextColor = Theme.Text;
        _notes.PlaceholderColor = Theme.Muted;

        var quick10 = Theme.SecondaryButton("+10 000 км");
        quick10.Clicked += (_, _) => AddInterval(10000);
        var quick15 = Theme.SecondaryButton("+15 000 км");
        quick15.Clicked += (_, _) => AddInterval(15000);

        var save = Theme.PrimaryButton("Сохранить сервисный план");
        save.Clicked += async (_, _) => await SaveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("MAINTENANCE"),
                    Theme.H1("Сервис / ТО"),
                    _vehicle,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            Theme.Eyebrow("ИНТЕРВАЛ"),
                            _current,
                            new HorizontalStackLayout { Spacing = 8, Children = { quick10, quick15 } },
                            _nextMileage,
                            new Label { Text = "Дата следующего ТО", TextColor = Theme.Muted, FontSize = 11 },
                            _nextDate
                        }
                    }),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { Theme.Eyebrow("РАБОТЫ"), _oil, _notes }
                    }),
                    save,
                    _status
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _vehicle.Text = "Сначала выберите автомобиль.";
            _vehicle.TextColor = Theme.Accent;
            return;
        }

        _vehicle.Text = $"{v.DisplayName} • {v.MileageKm:N0} км";
        _vehicle.TextColor = Theme.TextSoft;
        _plan = await _store.GetOrCreateServicePlanAsync(v.Id);

        var current = _plan.CurrentMileageKm ?? v.MileageKm;
        _current.Text = current?.ToString() ?? "";
        _nextMileage.Text = _plan.NextServiceMileageKm?.ToString() ?? "";
        _oil.Text = _plan.OilSpec;
        _notes.Text = _plan.Notes;
        _nextDate.Date = (_plan.NextServiceDate ?? DateTimeOffset.Now.AddMonths(12)).LocalDateTime;

        UpdateStatus(current);
    }

    private void AddInterval(long interval)
    {
        if (!long.TryParse(_current.Text, out var current)) return;
        _nextMileage.Text = (current + interval).ToString();
    }

    private async Task SaveAsync()
    {
        if (_plan is null) return;
        _plan.CurrentMileageKm = long.TryParse(_current.Text, out var current) ? current : null;
        _plan.NextServiceMileageKm = long.TryParse(_nextMileage.Text, out var next) ? next : null;
        _plan.NextServiceDate = new DateTimeOffset(_nextDate.Date);
        _plan.OilSpec = _oil.Text?.Trim() ?? "";
        _plan.Notes = _notes.Text?.Trim() ?? "";

        await _store.SaveAsync(await _store.LoadAsync());
        UpdateStatus(_plan.CurrentMileageKm);
    }

    private void UpdateStatus(long? current)
    {
        if (_plan is null) return;

        var dueMileage = _plan.NextServiceMileageKm is not null && current is not null
            ? _plan.NextServiceMileageKm - current
            : null;
        var dueDays = _plan.NextServiceDate is not null
            ? (int)Math.Ceiling((_plan.NextServiceDate.Value - DateTimeOffset.Now).TotalDays)
            : (int?)null;

        if ((dueMileage is not null && dueMileage <= 0) || (dueDays is not null && dueDays <= 0))
        {
            _status.Text = "ТО уже требуется.";
            _status.TextColor = Theme.Red;
        }
        else
        {
            var parts = new List<string>();
            if (dueMileage is not null) parts.Add($"через {dueMileage:N0} км");
            if (dueDays is not null) parts.Add($"через {dueDays} дн.");
            _status.Text = parts.Count == 0 ? "Сервисный план сохранён." : "Следующее ТО: " + string.Join(" • ", parts);
            _status.TextColor = Theme.Green;
        }
    }
}
