using AutoDiagPro.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AutoDiagPro.Mobile.Pages;

public sealed class EngineCatalogPage : ContentPage
{
    private sealed record CatalogItem(
        string Brand,
        string Model,
        string Years,
        string Engines,
        string Badge,
        string Image,
        string Fuels,
        string Hint);

    private readonly MobileState _state = AppServices.Get<MobileState>();

    private readonly Entry _search = new()
    {
        Placeholder = "Поиск по марке, модели или двигателю…",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 46,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };

    private readonly HorizontalStackLayout _filterRow = new() { Spacing = 8 };

    private readonly Grid _cards = new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        },
        ColumnSpacing = 10,
        RowSpacing = 10
    };

    private string _filter = "all";

    private readonly CatalogItem[] _items =
    {
        new("Volkswagen","Golf","2012–2020 • Mk7","1.2 TSI • 1.4 TSI • 1.6 TDI • 2.0 TDI • GTE","VW","catalog_vw_golf.svg","petrol|diesel|hybrid|popular","Типовые проблемы"),
        new("BMW","3 Series","2012–2019 • F30","1.6 • 2.0 • 3.0 • B48 / B58 / N47","BMW","catalog_bmw_3.svg","petrol|diesel|popular","Перед покупкой"),
        new("Mercedes-Benz","C-Class","2014–2021 • W205","1.6 • 2.0 • 2.1 • 3.0 • M274 / OM654","MB","catalog_mercedes_c.svg","petrol|diesel|popular","Что проверить"),
        new("Renault","Megane","2012–2020 • III / IV","1.2 TCe • 1.5 dCi • 1.6 • 1.8 TCe","RN","catalog_renault_megane.svg","petrol|diesel","Типовые проблемы"),
        new("Ford","Focus","2011–2018 • III","1.0 EcoBoost • 1.5 EcoBoost • 1.6 TDCi","FD","catalog_ford_focus.svg","petrol|diesel|popular","Что проверить"),
        new("Peugeot","308","2013–2021 • T9","1.2 PureTech • 1.5 BlueHDi • 1.6 THP","PG","catalog_peugeot_308.svg","petrol|diesel","Перед покупкой")
    };

    public EngineCatalogPage()
    {
        Title = "Каталог двигателей";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        _search.TextChanged += (_, _) => Render();
        RefreshFilterChips();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 16, 16, 42),
                Spacing = 14,
                Children =
                {
                    Header(),
                    SearchCard(),
                    new ScrollView
                    {
                        Orientation = ScrollOrientation.Horizontal,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                        Content = _filterRow
                    },
                    _cards,
                    Theme.MutedText(
                        "Каталог — отдельный справочный экран. Диагностика, OBD, DTC, ECU, Live Data, кодирование и сервисные операции работают как раньше.")
                }
            }
        };

        Render();
    }

    private View Header()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        grid.Add(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Theme.Eyebrow("AUTODIAG ENGINE LIBRARY"),
                Theme.H1("Каталог двигателей"),
                Theme.MutedText("Модель → поколение → мотор → быстрый переход к проверке.")
            }
        }, 0, 0);

        var counter = Theme.Pill("310+ двигателей", Theme.Accent);
        counter.VerticalOptions = LayoutOptions.Start;
        grid.Add(counter, 1, 0);
        return grid;
    }

    private View SearchCard()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };

        grid.Add(new Label
        {
            Text = "⌕",
            FontSize = 22,
            TextColor = Theme.Accent,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            WidthRequest = 26,
            FontAutoScalingEnabled = false
        }, 0, 0);

        grid.Add(_search, 1, 0);
        return Theme.CardView(grid, new Thickness(10), 16);
    }

    private void RefreshFilterChips()
    {
        _filterRow.Clear();
        foreach (var filter in new[]
        {
            ("all", "Все"),
            ("petrol", "Бензин"),
            ("diesel", "Дизель"),
            ("hybrid", "Гибрид"),
            ("popular", "Популярные")
        })
        {
            var selected = string.Equals(_filter, filter.Item1, StringComparison.OrdinalIgnoreCase);
            var chip = new Border
            {
                BackgroundColor = selected ? Theme.Accent : Theme.Card2,
                Stroke = selected ? Theme.Accent : Theme.Line,
                StrokeThickness = 1,
                Padding = new Thickness(12, 7),
                StrokeShape = new RoundRectangle { CornerRadius = 15 },
                Content = new Label
                {
                    Text = filter.Item2,
                    TextColor = selected ? Colors.White : Theme.TextSoft,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    FontAutoScalingEnabled = false
                }
            };

            var key = filter.Item1;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _filter = key;
                RefreshFilterChips();
                Render();
            };
            chip.GestureRecognizers.Add(tap);
            _filterRow.Add(chip);
        }
    }

    private void Render()
    {
        _cards.Clear();

        var query = (_search.Text ?? "").Trim();
        var words = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var data = _items.Where(x =>
        {
            var filterOk = _filter == "all" ||
                           x.Fuels.Split('|').Contains(_filter, StringComparer.OrdinalIgnoreCase);

            var haystack = $"{x.Brand} {x.Model} {x.Years} {x.Engines}";
            var searchOk = words.Length == 0 ||
                           words.All(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase));

            return filterOk && searchOk;
        }).ToArray();

        if (data.Length == 0)
        {
            var empty = Theme.CardView(
                Theme.MutedText("По этому фильтру ничего не найдено. Измени фильтр или строку поиска."),
                new Thickness(16),
                16);
            _cards.Add(empty, 0, 0);
            _cards.SetColumnSpan(empty, 2);
            return;
        }

        for (var i = 0; i < data.Length; i++)
            _cards.Add(Card(data[i]), i % 2, i / 2);
    }

    private View Card(CatalogItem item)
    {
        var badge = new Border
        {
            WidthRequest = 34,
            HeightRequest = 34,
            BackgroundColor = Theme.AccentSoft,
            Stroke = Color.FromArgb("#315F9D"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 17 },
            Content = new Label
            {
                Text = item.Badge,
                FontSize = item.Badge.Length > 2 ? 8 : 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Theme.Accent,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontAutoScalingEnabled = false
            }
        };

        var title = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        title.Add(badge, 0, 0);
        title.Add(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                new Label
                {
                    Text = item.Brand,
                    FontSize = 9,
                    TextColor = Theme.Muted,
                    MaxLines = 1,
                    FontAutoScalingEnabled = false
                },
                new Label
                {
                    Text = item.Model,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text,
                    MaxLines = 1,
                    FontAutoScalingEnabled = false
                }
            }
        }, 1, 0);

        title.Add(new Label
        {
            Text = "›",
            FontSize = 24,
            TextColor = Theme.Accent,
            VerticalTextAlignment = TextAlignment.Center,
            FontAutoScalingEnabled = false
        }, 2, 0);

        var card = Theme.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Border
                {
                    HeightRequest = 112,
                    StrokeThickness = 0,
                    BackgroundColor = Theme.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Content = new Image { Source = item.Image, Aspect = Aspect.AspectFill }
                },
                title,
                new Label
                {
                    Text = item.Years,
                    FontSize = 9.5,
                    TextColor = Theme.TextSoft,
                    FontAutoScalingEnabled = false
                },
                new Label
                {
                    Text = item.Engines,
                    FontSize = 9.5,
                    TextColor = Theme.Muted,
                    MaxLines = 3,
                    LineBreakMode = LineBreakMode.WordWrap,
                    FontAutoScalingEnabled = false
                },
                Theme.Pill(
                    item.Hint,
                    item.Hint.Contains("проблем", StringComparison.OrdinalIgnoreCase) ? Theme.Red : Theme.Accent)
            }
        }, new Thickness(10), 18);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenItemAsync(item);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task OpenItemAsync(CatalogItem item)
    {
        var action = await DisplayActionSheet(
            $"{item.Brand} {item.Model}",
            "Отмена",
            null,
            "Типовые проблемы",
            "Что проверить",
            "Перед покупкой",
            "Обзор модели и двигателей");

        if (string.IsNullOrWhiteSpace(action) || action == "Отмена")
            return;

        var model = $"{item.Brand} {item.Model} {item.Years}";
        _state.PendingAiQuestion = action switch
        {
            "Типовые проблемы" =>
                $"Покажи типовые проблемы {model}. Разделяй известные слабые места, симптомы и то, что нужно подтвердить диагностикой. Учитывай конкретный двигатель перед выводом.",
            "Что проверить" =>
                $"Составь практичный список, что проверить у {model}: двигатель, коробка, электроника, подвеска и кузов. Укажи, что можно проверить визуально и что через OBD.",
            "Перед покупкой" =>
                $"Сделай чек-лист проверки {model} перед покупкой: осмотр, диагностика, документы, история обслуживания, тест-драйв и красные флаги. Не делай вывод без фактов.",
            _ =>
                $"Дай обзор {model}: доступные двигатели ({item.Engines}), различия поколений и какие данные нужны для точной проверки конкретного автомобиля."
        };

        await Shell.Current.GoToAsync("ai");
    }
}
