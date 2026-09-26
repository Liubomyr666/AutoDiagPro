using System.Globalization;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class WearPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly Entry _tf = Field("Протектор перед, мм");
    private readonly Entry _tr = Field("Протектор зад, мм");
    private readonly Entry _pf = Field("Колодки перед, мм");
    private readonly Entry _pr = Field("Колодки зад, мм");
    private readonly Editor _notes = new() { Placeholder = "Заметки", MinimumHeightRequest = 80, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Theme.Surface, TextColor = Theme.Text, PlaceholderColor = Theme.Muted };
    private readonly Label _status = Theme.MutedText("Введите фактические замеры.");
    private readonly VerticalStackLayout _history = new() { Spacing = 9 };

    public WearPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Шины / колодки";

        var save = Theme.PrimaryButton("Сохранить замер");
        save.Clicked += async (_, _) => await SaveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("WEAR CHECK"),
                    Theme.H1("Шины / колодки"),
                    Theme.MutedText("Сохраняйте реальные замеры по автомобилю. AutoDiag отмечает низкие значения как требующие внимания, но окончательное решение остаётся за осмотром и спецификацией автомобиля."),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children = { _tf, _tr, _pf, _pr, _notes, save }
                    }),
                    _status,
                    Theme.H2("История замеров"),
                    _history
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task SaveAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _status.Text = "Сначала выберите автомобиль.";
            _status.TextColor = Theme.Accent;
            return;
        }

        var db = await _store.LoadAsync();
        db.WearChecks.Add(new WearCheckMobile
        {
            VehicleId = v.Id,
            TireFrontMm = Parse(_tf.Text),
            TireRearMm = Parse(_tr.Text),
            PadFrontMm = Parse(_pf.Text),
            PadRearMm = Parse(_pr.Text),
            Notes = _notes.Text?.Trim() ?? ""
        });
        await _store.SaveAsync(db);

        _status.Text = "Замер сохранён.";
        _status.TextColor = Theme.Green;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _history.Clear();
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _history.Add(Theme.CardView(Theme.MutedText("Сначала выберите автомобиль.")));
            return;
        }

        var db = await _store.LoadAsync();
        var list = db.WearChecks.Where(x => x.VehicleId == v.Id).OrderByDescending(x => x.CheckedAt).Take(15).ToList();

        foreach (var x in list)
        {
            var attention = new[]
            {
                x.TireFrontMm is not null && x.TireFrontMm < 2,
                x.TireRearMm is not null && x.TireRearMm < 2,
                x.PadFrontMm is not null && x.PadFrontMm < 3,
                x.PadRearMm is not null && x.PadRearMm < 3
            }.Any(vv => vv);

            _history.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label { Text = x.CheckedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm"), FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                    Theme.MutedText($"Шины: перед {Fmt(x.TireFrontMm)} / зад {Fmt(x.TireRearMm)} мм"),
                    Theme.MutedText($"Колодки: перед {Fmt(x.PadFrontMm)} / зад {Fmt(x.PadRearMm)} мм"),
                    string.IsNullOrWhiteSpace(x.Notes) ? Theme.MutedText("") : Theme.Body(x.Notes),
                    Theme.Pill(attention ? "ПРОВЕРИТЬ" : "OK", attention ? Theme.Accent : Theme.Green)
                }
            }, new Thickness(13)));
        }

        if (list.Count == 0)
            _history.Add(Theme.CardView(Theme.MutedText("Замеров пока нет.")));
    }

    private static double? Parse(string? text)
    {
        var clean = (text ?? "").Replace(',', '.');
        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string Fmt(double? value) => value is null ? "—" : value.Value.ToString("0.0", CultureInfo.InvariantCulture);

    private static Entry Field(string placeholder) => new()
    {
        Placeholder = placeholder,
        Keyboard = Keyboard.Numeric,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48
    };
}