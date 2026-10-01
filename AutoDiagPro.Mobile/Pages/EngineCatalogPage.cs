using Microsoft.Maui.Controls.Shapes;

namespace AutoDiagPro.Mobile.Pages;

public sealed class EngineCatalogPage : ContentPage
{
    private readonly Entry _search = new()
    {
        Placeholder = "Поиск по марке, модели или двигателю…",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 46,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };

    private readonly Grid _cards = new()
    {
        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
        ColumnSpacing = 10,
        RowSpacing = 10
    };

    private readonly (string Brand, string Model, string Years, string Engines, string Badge, string Image, string Tag)[] _items =
    {
        ("Volkswagen","Golf","2012–2020 • Mk7","1.2 TSI • 1.4 TSI • 1.6 TDI • 2.0 TDI","VW","catalog_vw_golf.svg","Типовые проблемы"),
        ("BMW","3 Series","2012–2019 • F30","1.6 • 2.0 • 3.0 • B48/B58/N47","BMW","catalog_bmw_3.svg","Перед покупкой"),
        ("Mercedes-Benz","C-Class","2014–2021 • W205","1.6 • 2.0 • 2.1 • 3.0","MB","catalog_mercedes_c.svg","Что проверить"),
        ("Renault","Megane","2012–2020 • III/IV","1.2 TCe • 1.5 dCi • 1.6 • 1.8 TCe","R","catalog_renault_megane.svg","Типовые проблемы"),
        ("Ford","Focus","2011–2018 • III","1.0 EcoBoost • 1.5 EcoBoost • 1.6 TDCi","F","catalog_ford_focus.svg","Что проверить"),
        ("Peugeot","308","2013–2021 • T9","1.2 PureTech • 1.5 BlueHDi • 1.6 THP","P","catalog_peugeot_308.svg","Перед покупкой")
    };

    public EngineCatalogPage()
    {
        Title = "Каталог двигателей";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        _search.TextChanged += (_, _) => Render();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 16, 16, 40),
                Spacing = 14,
                Children =
                {
                    Header(),
                    _search,
                    Filters(),
                    _cards,
                    Theme.MutedText("Справочный каталог добавлен поверх прошлой версии. Диагностика, OBD, AI, кодирование и сервисные функции не изменялись.")
                }
            }
        };
        Render();
    }

    private View Header()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        grid.Add(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Theme.Eyebrow("AUTODIAG ENGINE LIBRARY"),
                Theme.H1("Каталог двигателей"),
                Theme.MutedText("Модели, поколения, моторы и быстрые подсказки в одном стиле.")
            }
        }, 0, 0);
        grid.Add(Theme.Pill("310+ двигателей", Theme.Accent), 1, 0);
        return grid;
    }

    private View Filters()
    {
        var row = new HorizontalStackLayout { Spacing = 8 };
        foreach (var item in new[] { ("Все", true), ("Бензин", false), ("Дизель", false), ("Гибрид", false), ("Популярные", false) })
            row.Add(Chip(item.Item1, item.Item2));
        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = row
        };
    }

    private static Border Chip(string text, bool selected) => new()
    {
        BackgroundColor = selected ? Theme.Accent : Theme.Card2,
        Stroke = selected ? Theme.Accent : Theme.Line,
        StrokeThickness = 1,
        Padding = new Thickness(12, 7),
        StrokeShape = new RoundRectangle { CornerRadius = 14 },
        Content = new Label
        {
            Text = text,
            TextColor = selected ? Colors.White : Theme.TextSoft,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            FontAutoScalingEnabled = false
        }
    };

    private void Render()
    {
        _cards.Clear();
        var q = (_search.Text ?? "").Trim();
        var data = _items.Where(x =>
            q.Length == 0 ||
            $"{x.Brand} {x.Model} {x.Years} {x.Engines}".Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();

        for (var i = 0; i < data.Length; i++)
            _cards.Add(Card(data[i]), i % 2, i / 2);
    }

    private static View Card((string Brand, string Model, string Years, string Engines, string Badge, string Image, string Tag) x)
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
                Text = x.Badge,
                FontSize = x.Badge.Length > 2 ? 8 : 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Theme.Accent,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontAutoScalingEnabled = false
            }
        };

        var title = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        title.Add(badge, 0, 0);
        title.Add(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                new Label { Text = x.Brand, FontSize = 9, TextColor = Theme.Muted, MaxLines = 1, FontAutoScalingEnabled = false },
                new Label { Text = x.Model, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, MaxLines = 1, FontAutoScalingEnabled = false }
            }
        }, 1, 0);

        return Theme.CardView(new VerticalStackLayout
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
                    Content = new Image { Source = x.Image, Aspect = Aspect.AspectFill }
                },
                title,
                new Label { Text = x.Years, FontSize = 9.5, TextColor = Theme.TextSoft, FontAutoScalingEnabled = false },
                new Label { Text = x.Engines, FontSize = 9.5, TextColor = Theme.Muted, MaxLines = 3, LineBreakMode = LineBreakMode.WordWrap, FontAutoScalingEnabled = false },
                Theme.Pill(x.Tag, x.Tag.Contains("проблем", StringComparison.OrdinalIgnoreCase) ? Theme.Red : Theme.Accent)
            }
        }, new Thickness(10), 18);
    }
}
