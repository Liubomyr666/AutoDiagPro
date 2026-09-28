using System.Globalization;
using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class WearPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Entry _tf = Field("Протектор перед, мм");
    private readonly Entry _tr = Field("Протектор зад, мм");
    private readonly Entry _pf = Field("Колодки перед, мм");
    private readonly Entry _pr = Field("Колодки зад, мм");
    private readonly Editor _notes = new()
    {
        Placeholder = "Заметки",
        MinimumHeightRequest = 80,
        AutoSize = EditorAutoSizeOption.TextChanges,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted
    };
    private readonly Label _status = Theme.MutedText("Введите фактические замеры.");
    private readonly VerticalStackLayout _history = new() { Spacing = 9 };

    public WearPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Шины / колодки";

        var save = Theme.PrimaryButton("Сохранить замер в облако");
        save.Clicked += async (_, _) => await SaveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("WEAR CHECK • CLOUD"),
                    Theme.H1("Шины / колодки"),
                    Theme.MutedText("Замеры синхронизируются через AutoDiag Cloud и доступны на других устройствах."),
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
            SetStatus("Сначала выберите автомобиль.", Theme.Accent);
            return;
        }

        var tf = Parse(_tf.Text);
        var tr = Parse(_tr.Text);
        var pf = Parse(_pf.Text);
        var pr = Parse(_pr.Text);
        if (tf is null && tr is null && pf is null && pr is null)
        {
            SetStatus("Введите хотя бы один фактический замер.", Theme.Accent);
            return;
        }

        try
        {
            SetStatus("Сохраняю в AutoDiag Cloud...", Theme.Accent);
            await _api.CreateWearCheckAsync(v.Id, tf, tr, pf, pr, _notes.Text?.Trim());

            _tf.Text = _tr.Text = _pf.Text = _pr.Text = "";
            _notes.Text = "";
            SetStatus("Замер сохранён и синхронизирован.", Theme.Green);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Не удалось сохранить: " + ex.Message, Theme.Red);
        }
    }

    private async Task LoadAsync()
    {
        _history.Clear();
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            SetStatus("Сначала выберите автомобиль.", Theme.Accent);
            _history.Add(Theme.CardView(Theme.MutedText("Автомобиль не выбран.")));
            return;
        }

        try
        {
            var list = (await _api.GetWearChecksAsync(v.Id))
                .OrderByDescending(x => x.CheckedAt)
                .Take(30)
                .ToList();

            foreach (var x in list)
                _history.Add(Card(x));

            if (list.Count == 0)
                _history.Add(Theme.CardView(Theme.MutedText("Замеров пока нет.")));

            SetStatus($"{v.DisplayName} • облачных замеров: {list.Count}", Theme.Green);
        }
        catch (Exception ex)
        {
            SetStatus("Синхронизация: " + ex.Message, Theme.Red);
            _history.Add(Theme.CardView(Theme.MutedText("Не удалось загрузить историю.")));
        }
    }

    private static View Card(ServerWearCheckRecord x)
    {
        var attention = new[]
        {
            x.TireFrontMm is not null && x.TireFrontMm < 2,
            x.TireRearMm is not null && x.TireRearMm < 2,
            x.PadFrontMm is not null && x.PadFrontMm < 3,
            x.PadRearMm is not null && x.PadRearMm < 3
        }.Any(v => v);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label
                {
                    Text = x.CheckedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text
                },
                Theme.MutedText($"Шины: перед {Fmt(x.TireFrontMm)} / зад {Fmt(x.TireRearMm)} мм"),
                Theme.MutedText($"Колодки: перед {Fmt(x.PadFrontMm)} / зад {Fmt(x.PadRearMm)} мм"),
                string.IsNullOrWhiteSpace(x.Notes) ? Theme.MutedText("Без заметок") : Theme.Body(x.Notes),
                Theme.Pill(attention ? "ТРЕБУЕТ ПРОВЕРКИ" : "OK", attention ? Theme.Accent : Theme.Green)
            }
        }, new Thickness(13));
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.TextColor = color;
    }

    private static double? Parse(string? text)
    {
        var clean = (text ?? "").Replace(',', '.');
        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : null;
    }

    private static string Fmt(double? value) =>
        value is null ? "—" : value.Value.ToString("0.0", CultureInfo.InvariantCulture);

    private static Entry Field(string placeholder) => new()
    {
        Placeholder = placeholder,
        Keyboard = Keyboard.Numeric,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 48,
        FontAutoScalingEnabled = false
    };
}
