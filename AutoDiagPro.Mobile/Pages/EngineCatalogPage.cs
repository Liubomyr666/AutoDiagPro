using AutoDiagPro.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;

namespace AutoDiagPro.Mobile.Pages;

public sealed class EngineCatalogPage : ContentPage
{
    private sealed record CatalogItem(
        string Brand,
        string Model,
        string Years,
        string Engines,
        string Badge,
        string Fuels,
        string PhotoQuery,
        string Hint);

    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly HttpClient _photoHttp = CreatePhotoHttp();
    private readonly SemaphoreSlim _photoGate = new(1, 1);
    private readonly Dictionary<string, string> _photoUrls = new(StringComparer.OrdinalIgnoreCase);

    private readonly Entry _search = new()
    {
        Placeholder = "Марка, модель, поколение или двигатель…",
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted,
        HeightRequest = 46,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };

    private readonly HorizontalStackLayout _filterRow = new() { Spacing = 8 };
    private readonly Label _status = Theme.MutedText("Загрузка каталога…");

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
        new("Volkswagen","Golf","Mk7 • 2012–2020","1.2 TSI • 1.4 TSI • 1.6 TDI • 2.0 TDI • GTE","VW","petrol|diesel|hybrid|popular","Volkswagen Golf Mk7 hatchback","Типовые проблемы"),
        new("BMW","3 Series","F30 • 2012–2019","1.6 • 2.0 • 3.0 • B48 • B58 • N47","BMW","petrol|diesel|hybrid|popular","BMW F30 3 Series sedan","Перед покупкой"),
        new("Mercedes-Benz","C-Class","W205 • 2014–2021","1.6 • 2.0 • 2.1 • 3.0 • M274 • OM654","MB","petrol|diesel|hybrid|popular","Mercedes W205 C Class sedan","Что проверить"),
        new("Audi","A4","B9 • 2015–2024","1.4 TFSI • 2.0 TFSI • 2.0 TDI","AU","petrol|diesel|hybrid|popular","Audi A4 B9 sedan","Типовые проблемы"),
        new("Skoda","Octavia","A7 • 2013–2020","1.2 TSI • 1.4 TSI • 1.6 TDI • 2.0 TDI","SK","petrol|diesel|popular","Skoda Octavia III 2017","Перед покупкой"),
        new("Volkswagen","Passat","B8 • 2014–2023","1.4 TSI • 1.5 TSI • 2.0 TSI • 1.6 TDI • 2.0 TDI","VW","petrol|diesel|hybrid|popular","Volkswagen Passat B8 sedan","Типовые проблемы"),
        new("Toyota","Corolla","E210 • 2018–2025","1.2 Turbo • 1.8 Hybrid • 2.0 Hybrid","TY","petrol|hybrid|popular","Toyota Corolla E210 sedan","Что проверить"),
        new("Ford","Focus","Mk3 • 2011–2018","1.0 EcoBoost • 1.5 EcoBoost • 1.6 TDCi • 2.0 TDCi","FD","petrol|diesel|popular","Ford Focus Mk3 hatchback","Типовые проблемы"),
        new("Renault","Megane","IV • 2016–2023","1.2 TCe • 1.3 TCe • 1.5 dCi • 1.6 dCi","RN","petrol|diesel|popular","Renault Megane IV hatchback","Перед покупкой"),
        new("Peugeot","308","T9 • 2013–2021","1.2 PureTech • 1.6 THP • 1.5 BlueHDi","PG","petrol|diesel","Peugeot 308 T9 hatchback","Типовые проблемы"),
        new("Hyundai","Tucson","TL • 2015–2021","1.6 T-GDI • 2.0 • 1.7 CRDi • 2.0 CRDi","HY","petrol|diesel|popular","Hyundai Tucson 2018","Что проверить"),
        new("Kia","Ceed","CD • 2018–2024","1.0 T-GDI • 1.4 T-GDI • 1.6 CRDi","KIA","petrol|diesel|popular","Kia Ceed 2019","Типовые проблемы"),
        new("Opel","Astra","K • 2015–2022","1.0 Turbo • 1.4 Turbo • 1.6 CDTI","OP","petrol|diesel","Opel Astra K hatchback","Перед покупкой"),
        new("Nissan","Qashqai","J11 • 2013–2021","1.2 DIG-T • 1.3 DIG-T • 1.5 dCi • 1.6 dCi","NS","petrol|diesel|popular","Nissan Qashqai J11 SUV","Что проверить"),
        new("Honda","Civic","X • 2016–2022","1.0 VTEC Turbo • 1.5 VTEC Turbo • 2.0","HN","petrol|popular","Honda Civic X hatchback","Типовые проблемы"),
        new("Tesla","Model 3","2017–2024","RWD • Long Range • Performance","TS","electric|popular","Tesla Model 3 sedan","Перед покупкой"),
        new("BMW","5 Series","G30 • 2017–2023","2.0 • 3.0 • B48 • B58 • B47 • B57","BMW","petrol|diesel|hybrid|popular","BMW G30 5 Series sedan","Типовые проблемы"),
        new("Mercedes-Benz","E-Class","W213 • 2016–2023","2.0 • 3.0 • OM654 • OM656","MB","petrol|diesel|hybrid|popular","Mercedes W213 E Class sedan","Перед покупкой"),
        new("Audi","A6","C8 • 2018–2025","2.0 TFSI • 3.0 TFSI • 2.0 TDI • 3.0 TDI","AU","petrol|diesel|hybrid|popular","Audi A6 C8 sedan","Что проверить"),
        new("Skoda","Superb","III • 2015–2024","1.4 TSI • 1.5 TSI • 2.0 TSI • 1.6 TDI • 2.0 TDI","SK","petrol|diesel|hybrid","Skoda Superb III 2018","Перед покупкой")
    };

    public EngineCatalogPage()
    {
        Title = "Автомобили и двигатели";
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
                    _status,
                    _cards,
                    Theme.MutedText(
                        "Каталог — отдельный справочный экран. Диагностика, OBD, DTC, ECU, Live Data, кодирование и сервисные операции работают как раньше.")
                }
            }
        };

        Render();
    }

    private static HttpClient CreatePhotoHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AutoDiagPro-iOS/3.27");
        return client;
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
                Theme.Eyebrow("AUTODIAG CATALOG"),
                Theme.H1("Автомобили и двигатели"),
                Theme.MutedText("Популярные модели, поколения и моторы. Карточки используют реальные фото автомобиля.")
            }
        }, 0, 0);

        var counter = Theme.Pill("20 моделей", Theme.Accent);
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
            ("electric", "Электро"),
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
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var data = _items.Where(x =>
        {
            var filterOk = _filter == "all" ||
                           x.Fuels.Split('|').Contains(_filter, StringComparer.OrdinalIgnoreCase);

            var haystack = $"{x.Brand} {x.Model} {x.Years} {x.Engines}";
            var searchOk = words.Length == 0 ||
                           words.All(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase));

            return filterOk && searchOk;
        }).ToArray();

        _status.Text = $"Показано {data.Length} из {_items.Length} популярных моделей • фото: Wikimedia Commons";

        if (data.Length == 0)
        {
            var empty = Theme.CardView(
                Theme.MutedText("По этому фильтру ничего не найдено. Измени фильтр или строку поиска."),
                new Thickness(16),
                16);
            _cards.Add(empty, 0, 0);
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

        var photo = new Image
        {
            Aspect = Aspect.AspectFill,
            BackgroundColor = Theme.Surface,
            Opacity = 0.96
        };

        _ = LoadPhotoAsync(photo, item.PhotoQuery);

        var card = Theme.CardView(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Border
                {
                    HeightRequest = 122,
                    StrokeThickness = 0,
                    BackgroundColor = Theme.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Content = photo
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

    private async Task LoadPhotoAsync(Image image, string photoQuery)
    {
        await _photoGate.WaitAsync();
        try
        {
            if (!_photoUrls.TryGetValue(photoQuery, out var photoUrl))
            {
                photoUrl = await ResolvePhotoUrlAsync(photoQuery) ?? "";

                if (string.IsNullOrWhiteSpace(photoUrl))
                {
                    var broad = string.Join(" ", photoQuery
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Take(3));
                    photoUrl = await ResolvePhotoUrlAsync(broad) ?? "";
                }

                if (!string.IsNullOrWhiteSpace(photoUrl))
                    _photoUrls[photoQuery] = photoUrl;
            }

            if (!string.IsNullOrWhiteSpace(photoUrl))
            {
                image.Source = new UriImageSource
                {
                    Uri = new Uri(photoUrl),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(30)
                };
            }
        }
        catch
        {
            // Keep the dark placeholder when the photo network is unavailable.
        }
        finally
        {
            await Task.Delay(400);
            _photoGate.Release();
        }
    }

    private async Task<string?> ResolvePhotoUrlAsync(string search)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var api =
                    "https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrnamespace=6&gsrlimit=1" +
                    "&gsrsearch=" + Uri.EscapeDataString(search) +
                    "&prop=imageinfo&iiprop=url&iiurlwidth=900&format=json&origin=*";

                var json = await _photoHttp.GetStringAsync(api);
                using var document = JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty("query", out var query) ||
                    !query.TryGetProperty("pages", out var pages))
                    return null;

                foreach (var page in pages.EnumerateObject())
                {
                    if (!page.Value.TryGetProperty("imageinfo", out var infos) || infos.GetArrayLength() == 0)
                        continue;

                    var info = infos[0];
                    if (info.TryGetProperty("thumburl", out var thumb))
                        return thumb.GetString();
                    if (info.TryGetProperty("url", out var original))
                        return original.GetString();
                }

                return null;
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(650 * (attempt + 1));
            }
        }

        return null;
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
