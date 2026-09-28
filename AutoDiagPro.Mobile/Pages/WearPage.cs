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
        Placeholder = "Заметки / что проверить",
        MinimumHeightRequest = 80,
        AutoSize = EditorAutoSizeOption.TextChanges,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted
    };
    private readonly Label _status = Theme.MutedText("Загрузка...");
    private readonly VerticalStackLayout _summary = new() { Spacing = 8 };
    private readonly VerticalStackLayout _history = new() { Spacing = 9 };

    public WearPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Шины / колодки";

        var save = Theme.PrimaryButton("Сохранить фактический замер");
        save.Clicked += async (_, _) => await SaveAsync();

        var inputCard = Theme.CardView(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                Theme.Eyebrow("НОВЫЙ ЗАМЕР"),
                Theme.MutedText("Заполняйте только то, что реально измерено."),
                _tf, _tr, _pf, _pr, _notes, save
            }
        });
        inputCard.IsVisible = AccessPolicy.IsStaff;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("WEAR CHECK • AUTODIAG CLOUD"),
                    Theme.H1("Шины / колодки"),
                    Theme.MutedText(AccessPolicy.IsStaff
                        ? "Фактические замеры синхронизируются с Windows и iPhone."
                        : "Здесь отображаются фактические замеры, сделанные сотрудником СТО."),
                    Theme.CardView(_summary),
                    inputCard,
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
        if (!AccessPolicy.IsStaff)
        {
            SetStatus("Добавлять фактические замеры может только сотрудник СТО.", Theme.Accent);
            return;
        }

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

        if (!ValidTire(tf) || !ValidTire(tr) || !ValidPad(pf) || !ValidPad(pr))
        {
            SetStatus("Проверьте значения: протектор 0–30 мм, колодки 0–40 мм.", Theme.Red);
            return;
        }

        try
        {
            SetStatus("Сохраняю в AutoDiag Cloud...", Theme.Accent);
            await _api.CreateWearCheckAsync(v.Id, tf, tr, pf, pr, _notes.Text?.Trim());

            _tf.Text = _tr.Text = _pf.Text = _pr.Text = "";
            _notes.Text = "";
            await LoadAsync();
            SetStatus("Замер сохранён • Windows и iPhone синхронизированы.", Theme.Green);
        }
        catch (Exception ex)
        {
            SetStatus("Не удалось сохранить: " + ex.Message, Theme.Red);
        }
    }

    private async Task LoadAsync()
    {
        _history.Clear();
        _summary.Clear();

        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _summary.Add(Theme.MutedText("Автомобиль не выбран."));
            SetStatus("Сначала выберите автомобиль.", Theme.Accent);
            return;
        }

        try
        {
            var list = (await _api.GetWearChecksAsync(v.Id))
                .OrderByDescending(x => x.CheckedAt)
                .Take(30)
                .ToList();

            RenderSummary(v, list.FirstOrDefault());

            foreach (var x in list)
                _history.Add(Card(x));

            if (list.Count == 0)
                _history.Add(Theme.CardView(Theme.MutedText("Фактических замеров пока нет.")));

            SetStatus($"{v.DisplayName} • замеров в облаке: {list.Count}", Theme.Green);
        }
        catch (Exception ex)
        {
            _summary.Add(Theme.MutedText("Не удалось получить текущий износ."));
            SetStatus("Синхронизация: " + ex.Message, Theme.Red);
            _history.Add(Theme.CardView(Theme.MutedText("Не удалось загрузить историю.")));
        }
    }

    private void RenderSummary(ServerVehicleRecord vehicle, ServerWearCheckRecord? latest)
    {
        _summary.Add(Theme.Eyebrow("ТЕКУЩЕЕ СОСТОЯНИЕ"));
        _summary.Add(new Label
        {
            Text = vehicle.DisplayName,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text,
            FontAutoScalingEnabled = false
        });

        if (latest is null)
        {
            _summary.Add(Theme.MutedText("Нет фактических замеров."));
            return;
        }

        var tireMin = new[] { latest.TireFrontMm, latest.TireRearMm }
            .Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(double.NaN).Min();
        var padMin = new[] { latest.PadFrontMm, latest.PadRearMm }
            .Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(double.NaN).Min();

        var attention = (!double.IsNaN(tireMin) && tireMin < 2.0) ||
                        (!double.IsNaN(padMin) && padMin < 3.0);

        _summary.Add(Theme.Pill(attention ? "ТРЕБУЕТ ПРОВЕРКИ" : "ПОСЛЕДНИЙ ЗАМЕР OK",
            attention ? Theme.Accent : Theme.Green));
        _summary.Add(Theme.MutedText(
            $"Шины: перед {Fmt(latest.TireFrontMm)} / зад {Fmt(latest.TireRearMm)} мм"));
        _summary.Add(Theme.MutedText(
            $"Колодки: перед {Fmt(latest.PadFrontMm)} / зад {Fmt(latest.PadRearMm)} мм"));
        _summary.Add(Theme.MutedText($"Обновлено • {latest.CheckedAt.LocalDateTime:dd.MM.yyyy HH:mm}"));
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
                    TextColor = Theme.Text,
                    FontAutoScalingEnabled = false
                },
                Theme.MutedText($"Шины: перед {Fmt(x.TireFrontMm)} / зад {Fmt(x.TireRearMm)} мм"),
                Theme.MutedText($"Колодки: перед {Fmt(x.PadFrontMm)} / зад {Fmt(x.PadRearMm)} мм"),
                string.IsNullOrWhiteSpace(x.Notes) ? Theme.MutedText("Без заметок") : Theme.Body(x.Notes),
                Theme.Pill(attention ? "ПРОВЕРИТЬ" : "OK", attention ? Theme.Accent : Theme.Green)
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

    private static bool ValidTire(double? value) => value is null || value <= 30;
    private static bool ValidPad(double? value) => value is null || value <= 40;

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
