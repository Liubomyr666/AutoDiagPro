using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class ServicePlannerPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Label _vehicle = Theme.MutedText("Автомобиль не выбран");
    private readonly Entry _current = Field("Текущий пробег, км", Keyboard.Numeric, readOnly: true);
    private readonly Entry _nextMileage = Field("Следующее ТО, км", Keyboard.Numeric);
    private readonly DatePicker _nextDate = new()
    {
        Format = "dd.MM.yyyy",
        MinimumDate = DateTime.Today,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text
    };
    private readonly Entry _oil = Field("Масло / спецификация");
    private readonly Editor _notes = new()
    {
        Placeholder = "Что делать на следующем ТО",
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 90,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted
    };
    private readonly Label _status = Theme.MutedText("Загрузка...");
    private ServerMaintenanceRecord? _plan;

    public ServicePlannerPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Сервис / ТО";

        var quick10 = Theme.SecondaryButton("+10 000 км");
        quick10.Clicked += (_, _) => AddInterval(10000);
        var quick15 = Theme.SecondaryButton("+15 000 км");
        quick15.Clicked += (_, _) => AddInterval(15000);

        var save = Theme.PrimaryButton("Сохранить в AutoDiag Cloud");
        save.Clicked += async (_, _) => await SaveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("MAINTENANCE • CLOUD"),
                    Theme.H1("Сервис / ТО"),
                    Theme.MutedText("Один план для Windows и iPhone. Изменения сохраняются на сервере AutoDiag Pro."),
                    _vehicle,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            Theme.Eyebrow("ИНТЕРВАЛ"),
                            _current,
                            new Grid
                            {
                                ColumnDefinitions =
                                {
                                    new ColumnDefinition(GridLength.Star),
                                    new ColumnDefinition(GridLength.Star)
                                },
                                ColumnSpacing = 8,
                                Children =
                                {
                                    { quick10, 0, 0 },
                                    { quick15, 1, 0 }
                                }
                            },
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
            _status.Text = "Автомобиль не выбран.";
            _status.TextColor = Theme.Accent;
            return;
        }

        _vehicle.Text = $"{v.DisplayName} • {(v.MileageKm is null ? "пробег —" : $"{v.MileageKm:N0} км")}";
        _vehicle.TextColor = Theme.TextSoft;
        _current.Text = v.MileageKm?.ToString() ?? "";

        try
        {
            var items = await _api.GetMaintenanceAsync(v.Id);
            _plan = items
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault(x => x.Name.Equals("Плановое ТО", StringComparison.OrdinalIgnoreCase))
                ?? items.OrderByDescending(x => x.UpdatedAt).FirstOrDefault();

            if (_plan is null)
            {
                _nextMileage.Text = v.MileageKm is long km ? (km + 10000).ToString() : "";
                _nextDate.Date = DateTime.Today.AddMonths(12);
                _oil.Text = "";
                _notes.Text = "";
                _status.Text = "Облачный план ещё не создан.";
                _status.TextColor = Theme.Accent;
                return;
            }

            _nextMileage.Text = _plan.DueMileage?.ToString() ?? "";
            _nextDate.Date = _plan.DueDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today.AddMonths(12);
            (_oil.Text, _notes.Text) = ParseNotes(_plan.Notes);
            UpdateStatus(v.MileageKm);
        }
        catch (Exception ex)
        {
            _status.Text = "Не удалось загрузить план: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private void AddInterval(long interval)
    {
        var current = _state.SelectedVehicle?.MileageKm;
        if (current is null && !long.TryParse(_current.Text, out var parsed)) return;
        _nextMileage.Text = ((current ?? parsed) + interval).ToString();
    }

    private async Task SaveAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null) return;

        var nextMileage = long.TryParse(_nextMileage.Text, out var next) ? next : (long?)null;
        var dueDate = DateOnly.FromDateTime(_nextDate.Date);
        var notes = BuildNotes();

        _status.Text = "Сохраняю в облако...";
        _status.TextColor = Theme.Accent;

        try
        {
            if (_plan is null)
            {
                await _api.CreateMaintenanceAsync(
                    v.Id,
                    "Плановое ТО",
                    nextMileage,
                    dueDate,
                    v.MileageKm,
                    DateTimeOffset.Now,
                    notes);
            }
            else
            {
                await _api.UpdateMaintenanceAsync(
                    _plan.Id,
                    name: "Плановое ТО",
                    dueMileage: nextMileage,
                    dueDate: dueDate,
                    lastDoneMileage: v.MileageKm,
                    lastDoneAt: DateTimeOffset.Now,
                    notes: notes);
            }

            _status.Text = "Сохранено • Windows и iPhone увидят один план ТО.";
            _status.TextColor = Theme.Green;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка сохранения: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private string BuildNotes()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_oil.Text))
            parts.Add("Масло: " + _oil.Text.Trim());
        if (!string.IsNullOrWhiteSpace(_notes.Text))
            parts.Add(_notes.Text.Trim());
        return string.Join(Environment.NewLine, parts);
    }

    private static (string Oil, string Notes) ParseNotes(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return ("", "");
        var lines = source.Replace("\r", "").Split('\n').ToList();
        var oil = "";
        if (lines.Count > 0 && lines[0].StartsWith("Масло:", StringComparison.OrdinalIgnoreCase))
        {
            oil = lines[0]["Масло:".Length..].Trim();
            lines.RemoveAt(0);
        }
        return (oil, string.Join(Environment.NewLine, lines).Trim());
    }

    private void UpdateStatus(long? current)
    {
        if (_plan is null) return;

        var dueMileage = _plan.DueMileage is not null && current is not null
            ? _plan.DueMileage - current
            : null;
        var dueDays = _plan.DueDate is not null
            ? _plan.DueDate.Value.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber
            : (int?)null;

        if ((dueMileage is not null && dueMileage <= 0) || (dueDays is not null && dueDays <= 0))
        {
            _status.Text = "ТО уже требуется.";
            _status.TextColor = Theme.Red;
            return;
        }

        var parts = new List<string>();
        if (dueMileage is not null) parts.Add($"через {dueMileage:N0} км");
        if (dueDays is not null) parts.Add($"через {dueDays} дн.");
        _status.Text = parts.Count == 0 ? "План синхронизирован." : "Следующее ТО: " + string.Join(" • ", parts);
        _status.TextColor = Theme.Green;
    }

    private static Entry Field(string placeholder, Keyboard? keyboard = null, bool readOnly = false) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48,
        IsReadOnly = readOnly,
        FontAutoScalingEnabled = false
    };
}
