using System.Globalization;
using System.Text;
using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Graphics;

namespace AutoDiagPro.Mobile.Pages;

public sealed class LiveDataPage : ContentPage
{
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();
    private readonly Label _status = Theme.MutedText("Подключите OBD-адаптер.");
    private readonly Label _sessionStatus = Theme.MutedText("Сессия не записывается.");
    private readonly Label _current = MetricValue("—");
    private readonly Label _min = MetricValue("—");
    private readonly Label _max = MetricValue("—");
    private readonly Label _before = MetricValue("—");
    private readonly Label _after = MetricValue("—");
    private readonly Label _delta = MetricValue("—");
    private readonly Label _compareStatus = Theme.MutedText("Запишите две сессии ДО / ПОСЛЕ.");
    private readonly VerticalStackLayout _values = new() { Spacing = 9 };
    private readonly Picker _pid = new() { Title = "Параметр на графике" };
    private readonly GraphicsView _chart = new() { HeightRequest = 300 };
    private readonly LiveChartDrawable _chartDrawable = new();
    private readonly Button _record = Theme.PrimaryButton("● Запись поездки");
    private readonly Button _refresh = Theme.SecondaryButton("Обновить");
    private readonly Button _beforeButton = Theme.SecondaryButton("Сохранить как ДО");
    private readonly Button _afterButton = Theme.SecondaryButton("Сохранить как ПОСЛЕ");
    private readonly Button _compareButton = Theme.SecondaryButton("Сравнить ДО / ПОСЛЕ");
    private readonly Button _exportButton = Theme.SecondaryButton("Экспорт CSV");
    private readonly Button _clearButton = Theme.SecondaryButton("Очистить график");
    private readonly Switch _auto = new();

    private IDispatcherTimer? _timer;
    private bool _busy;
    private bool _recording;
    private DateTime _startedAt;
    private readonly List<LiveFrame> _currentFrames = new();
    private readonly List<List<LiveFrame>> _sessions = new();
    private List<LiveFrame>? _beforeSession;
    private List<LiveFrame>? _afterSession;
    private readonly Dictionary<string, List<double>> _series =
        new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _lastSnapshot =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record LiveFrame(DateTime Timestamp, Dictionary<string, string> Values);
    public LiveDataPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Live Data 2.0";

        _pid.TextColor = Theme.Text;
        _pid.BackgroundColor = Theme.Surface;
        _pid.SelectedIndexChanged += (_, _) =>
        {
            RenderChart();
            UpdateComparison();
        };

        _chart.Drawable = _chartDrawable;
        _refresh.Clicked += async (_, _) => await RefreshAsync();
        _record.Clicked += async (_, _) => await ToggleRecordingAsync();
        _beforeButton.Clicked += (_, _) => SaveLastAsBefore();
        _afterButton.Clicked += (_, _) => SaveLastAsAfter();
        _compareButton.Clicked += async (_, _) => await CompareAsync();
        _exportButton.Clicked += async (_, _) => await ExportCsvAsync();
        _clearButton.Clicked += (_, _) => ClearChart();

        _auto.Toggled += (_, e) =>
        {
            if (e.Value || _recording) StartTimer();
            else StopTimer();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 40),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("LIVE DATA 2.0"),
                    Theme.H1("Живые данные"),
                    Theme.MutedText("Графики в реальном времени, запись поездки, Min / Max / Current, сравнение ДО / ПОСЛЕ и CSV."),
                    BuildToolbar(),
                    BuildAutoRefreshCard(),
                    BuildChartCard(),
                    BuildCompareCard(),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            Theme.Eyebrow("ВСЕ ДОСТУПНЫЕ ПАРАМЕТРЫ ECU"),
                            Theme.MutedText("Показываются только PID, которые реально ответил автомобиль."),
                            _values
                        }
                    })
                }
            }
        };
    }
    private View BuildToolbar()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        grid.Add(_record, 0, 0);
        grid.Add(_refresh, 1, 0);
        grid.Add(_beforeButton, 0, 1);
        grid.Add(_afterButton, 1, 1);
        grid.Add(_compareButton, 0, 2);
        Grid.SetColumnSpan(_compareButton, 2);
        grid.Add(_exportButton, 0, 3);
        grid.Add(_clearButton, 1, 3);
        return grid;
    }

    private View BuildAutoRefreshCard()
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
            Spacing = 4,
            Children =
            {
                Theme.H2("Автообновление"),
                Theme.MutedText("Раз в 1 секунду во время диагностики или записи")
            }
        }, 0, 0);
        grid.Add(_auto, 1, 0);
        return Theme.CardView(grid);
    }
    private View BuildChartCard()
    {
        var stats = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        stats.Add(MetricCard("CURRENT", _current), 0, 0);
        stats.Add(MetricCard("MIN", _min), 1, 0);
        stats.Add(MetricCard("MAX", _max), 2, 0);

        var chartBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#08131B"),
            Stroke = Theme.Line,
            StrokeThickness = 1,
            Padding = new Thickness(8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Content = _chart
        };

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.Eyebrow("ГРАФИК В РЕАЛЬНОМ ВРЕМЕНИ"),
                _sessionStatus,
                _pid,
                stats,
                chartBorder,
                _status
            }
        });
    }

    private View BuildCompareCard()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        grid.Add(MetricCard("ДО • СРЕДНЕЕ", _before), 0, 0);
        grid.Add(MetricCard("ПОСЛЕ • СРЕДНЕЕ", _after), 1, 0);
        grid.Add(MetricCard("Δ", _delta), 2, 0);

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Theme.Eyebrow("СРАВНЕНИЕ ДО / ПОСЛЕ"),
                grid,
                _compareStatus
            }
        });
    }
    private static View MetricCard(string title, Label value) =>
        Theme.SoftCard(new VerticalStackLayout
        {
            Spacing = 5,
            Children = { Theme.Eyebrow(title), value }
        }, new Thickness(10));

    private static Label MetricValue(string text) => new()
    {
        Text = text,
        FontSize = 17,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text,
        HorizontalTextAlignment = TextAlignment.Center,
        FontAutoScalingEnabled = false
    };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        if (!_recording) StopTimer();
        base.OnDisappearing();
    }

    private void StartTimer()
    {
        if (_timer is not null) return;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (_, _) => await RefreshAsync(silent: true);
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
    }
    private async Task RefreshAsync(bool silent = false)
    {
        if (_busy) return;
        if (!_obd.IsConnected)
        {
            _status.Text = "OBD не подключён. Откройте «Диагностика» и подключите адаптер.";
            _status.TextColor = Theme.Accent;
            return;
        }

        _busy = true;
        _refresh.IsEnabled = false;
        if (!silent)
        {
            _status.Text = "Читаю Live Data...";
            _status.TextColor = Theme.Accent;
        }

        try
        {
            var data = await _obd.LiveSnapshotAsync();
            var now = DateTime.Now;
            _lastSnapshot = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);

            foreach (var item in data)
            {
                if (!TryParseNumber(item.Value, out var number)) continue;
                if (!_series.TryGetValue(item.Key, out var points))
                {
                    points = new List<double>();
                    _series[item.Key] = points;
                }
                points.Add(number);
                if (points.Count > 600)
                    points.RemoveRange(0, points.Count - 600);
            }

            if (_recording)
            {
                _currentFrames.Add(new LiveFrame(
                    now,
                    new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase)));
                var elapsed = now - _startedAt;
                _sessionStatus.Text =
                    $"● REC • {_currentFrames.Count} кадров • {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
                _sessionStatus.TextColor = Theme.Accent;
            }

            RefreshPidPicker(data.Keys);
            RenderValues(data);
            RenderChart();
            _status.Text = $"ONLINE • {_obd.TransportName} • {now:HH:mm:ss}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
        finally
        {
            _busy = false;
            _refresh.IsEnabled = true;
        }
    }
    private void RenderValues(IReadOnlyDictionary<string, string> data)
    {
        _values.Clear();
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 9,
            RowSpacing = 9
        };

        var i = 0;
        foreach (var item in data)
        {
            grid.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    Theme.Eyebrow(item.Key),
                    new Label
                    {
                        Text = item.Value,
                        FontSize = 17,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Theme.Text,
                        FontAutoScalingEnabled = false
                    }
                }
            }, new Thickness(12)), i % 2, i / 2);
            i++;
        }

        _values.Add(grid);
    }

    private void RefreshPidPicker(IEnumerable<string> keys)
    {
        var selected = _pid.SelectedItem?.ToString();
        var available = keys
            .Where(k => _series.TryGetValue(k, out var values) && values.Count > 0)
            .OrderBy(PidRank)
            .ThenBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _pid.ItemsSource = available;
        if (available.Count == 0) return;
        _pid.SelectedItem = selected is not null && available.Contains(selected)
            ? selected
            : available[0];
    }
    private void RenderChart()
    {
        var key = _pid.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(key) ||
            !_series.TryGetValue(key, out var raw) ||
            raw.Count == 0)
        {
            _current.Text = _min.Text = _max.Text = "—";
            _chartDrawable.Values = Array.Empty<double>();
            _chart.Invalidate();
            return;
        }

        var values = raw.TakeLast(300).ToArray();
        _current.Text = values[^1].ToString("0.##", CultureInfo.InvariantCulture);
        _min.Text = values.Min().ToString("0.##", CultureInfo.InvariantCulture);
        _max.Text = values.Max().ToString("0.##", CultureInfo.InvariantCulture);
        _chartDrawable.Values = values;
        _chart.Invalidate();
    }

    private async Task ToggleRecordingAsync()
    {
        if (!_obd.IsConnected)
        {
            await DisplayAlert("Live Data 2.0", "Сначала подключите OBD-адаптер.", "OK");
            return;
        }

        if (!_recording)
        {
            _currentFrames.Clear();
            _series.Clear();
            _recording = true;
            _startedAt = DateTime.Now;
            _record.Text = "■ Остановить";
            _sessionStatus.Text = "● REC • запуск записи поездки...";
            _sessionStatus.TextColor = Theme.Accent;
            _auto.IsToggled = true;
            StartTimer();
            await RefreshAsync(silent: true);
            return;
        }

        StopRecording();
    }

    private void StopRecording()
    {
        _recording = false;
        _record.Text = "● Запись поездки";

        if (_currentFrames.Count > 0)
        {
            var completed = CloneSession(_currentFrames);
            _sessions.Add(completed);
            while (_sessions.Count > 10) _sessions.RemoveAt(0);
            if (_beforeSession is null) _beforeSession = CloneSession(completed);
            else if (_afterSession is null) _afterSession = CloneSession(completed);
        }

        var duration = DateTime.Now - _startedAt;
        _sessionStatus.Text =
            $"Запись завершена • {_currentFrames.Count} кадров • {(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
        _sessionStatus.TextColor = Theme.Green;
        UpdateComparison();
        if (!_auto.IsToggled) StopTimer();
    }
    private static List<LiveFrame> CloneSession(IEnumerable<LiveFrame> source) =>
        source.Select(f => new LiveFrame(
            f.Timestamp,
            new Dictionary<string, string>(f.Values, StringComparer.OrdinalIgnoreCase))).ToList();

    private List<LiveFrame>? LastSession()
    {
        if (_recording) return null;
        return _sessions.LastOrDefault();
    }

    private void SaveLastAsBefore()
    {
        var session = LastSession();
        if (session is null)
        {
            _compareStatus.Text = "Сначала завершите запись Live Data.";
            return;
        }
        _beforeSession = CloneSession(session);
        _compareStatus.Text = "Последняя сессия сохранена как ДО.";
        UpdateComparison();
    }

    private void SaveLastAsAfter()
    {
        var session = LastSession();
        if (session is null)
        {
            _compareStatus.Text = "Сначала завершите запись Live Data.";
            return;
        }
        _afterSession = CloneSession(session);
        _compareStatus.Text = "Последняя сессия сохранена как ПОСЛЕ.";
        UpdateComparison();
    }

    private void UpdateComparison()
    {
        var key = _pid.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            _before.Text = _after.Text = _delta.Text = "—";
            return;
        }

        var a = _beforeSession is null ? null : Average(_beforeSession, key);
        var b = _afterSession is null ? null : Average(_afterSession, key);
        _before.Text = a?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";
        _after.Text = b?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";

        if (a is null || b is null)
        {
            _delta.Text = "—";
            return;
        }

        var d = b.Value - a.Value;
        _delta.Text = d.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
    }
    private async Task CompareAsync()
    {
        if (_beforeSession is null || _afterSession is null)
        {
            if (_sessions.Count < 2)
            {
                await DisplayAlert("Live Data 2.0",
                    "Нужно записать минимум две сессии.", "OK");
                return;
            }
            _beforeSession = CloneSession(_sessions[^2]);
            _afterSession = CloneSession(_sessions[^1]);
        }

        UpdateComparison();
        var keys = _beforeSession.SelectMany(x => x.Values.Keys)
            .Intersect(_afterSession.SelectMany(x => x.Values.Keys), StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(PidRank)
            .ThenBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            "LIVE DATA 2.0 • ДО / ПОСЛЕ",
            $"ДО: {_beforeSession.Count} кадров",
            $"ПОСЛЕ: {_afterSession.Count} кадров",
            ""
        };

        foreach (var key in keys)
        {
            var a = Average(_beforeSession, key);
            var b = Average(_afterSession, key);
            if (a is null || b is null) continue;
            var d = b.Value - a.Value;
            lines.Add($"{key}: {a:0.##} → {b:0.##}  Δ {d:+0.##;-0.##;0}");
        }

        _compareStatus.Text =
            $"Сравнено PID: {Math.Max(0, lines.Count - 4)} • ДО {_beforeSession.Count} / ПОСЛЕ {_afterSession.Count} кадров";
        await DisplayAlert("Live Data 2.0 • ДО / ПОСЛЕ",
            string.Join(Environment.NewLine, lines.Take(30)), "OK");
    }

    private async Task ExportCsvAsync()
    {
        var frames = _recording
            ? _currentFrames
            : _sessions.LastOrDefault() ?? _currentFrames;

        if (frames.Count == 0)
        {
            await DisplayAlert("Экспорт CSV", "Сначала запишите Live Data-сессию.", "OK");
            return;
        }

        var keys = frames.SelectMany(x => x.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(PidRank)
            .ThenBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("Timestamp");
        foreach (var key in keys) sb.Append(';').Append(Csv(key));
        sb.AppendLine();

        foreach (var frame in frames)
        {
            sb.Append(frame.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            foreach (var key in keys)
            {
                frame.Values.TryGetValue(key, out var value);
                sb.Append(';').Append(Csv(value ?? ""));
            }
            sb.AppendLine();
        }
        var path = Path.Combine(
            FileSystem.CacheDirectory,
            $"AutoDiagPro_LiveData_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true));
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "AutoDiag Pro • Live Data CSV",
            File = new ShareFile(path)
        });
    }

    private void ClearChart()
    {
        _series.Clear();
        _chartDrawable.Values = Array.Empty<double>();
        _chart.Invalidate();
        _current.Text = _min.Text = _max.Text = "—";
        _sessionStatus.Text = _recording
            ? "● REC • график очищен, запись продолжается"
            : "График очищен. ДО / ПОСЛЕ сохранены.";
    }

    private static double? Average(IEnumerable<LiveFrame> frames, string key)
    {
        var values = new List<double>();
        foreach (var frame in frames)
            if (frame.Values.TryGetValue(key, out var text) &&
                TryParseNumber(text, out var number))
                values.Add(number);
        return values.Count == 0 ? null : values.Average();
    }

    private static bool TryParseNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(',', '.');
        var start = normalized.TakeWhile(c =>
            char.IsDigit(c) || c is '-' or '+' or '.').ToArray();

        var token = new string(start);
        if (token.Length == 0)
        {
            token = new string(normalized
                .SkipWhile(c => !char.IsDigit(c) && c is not '-' and not '+')
                .TakeWhile(c => char.IsDigit(c) || c is '-' or '+' or '.')
                .ToArray());
        }

        return double.TryParse(token, NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);
    }

    private static int PidRank(string key)
    {
        var k = key.ToLowerInvariant();
        if (k.Contains("rpm")) return 0;
        if (k.Contains("скор")) return 1;
        if (k.Contains("температура ож")) return 2;
        if (k.Contains("нагруз")) return 3;
        if (k.Contains("давлен")) return 4;
        if (k.Contains("maf") || k.Contains("map")) return 5;
        if (k.Contains("напряж")) return 6;
        return 20;
    }
    private static string Csv(string value)
    {
        if (value.Contains(';') || value.Contains('"') ||
            value.Contains('\n') || value.Contains('\r'))
            return string.Concat("\"", value.Replace("\"", "\"\""), "\"");
        return value;
    }

    private sealed class LiveChartDrawable : IDrawable
    {
        public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var width = dirtyRect.Width;
            var height = dirtyRect.Height;
            const float pad = 14f;

            canvas.StrokeColor = Color.FromArgb("#19303D");
            canvas.StrokeSize = 1;
            for (var i = 1; i < 4; i++)
            {
                var y = pad + (height - pad * 2) * i / 4f;
                canvas.DrawLine(pad, y, width - pad, y);
            }

            if (Values.Count == 0) return;

            var min = Values.Min();
            var max = Values.Max();
            var range = Math.Max(0.0001, max - min);

            canvas.StrokeColor = Theme.Accent;
            canvas.StrokeSize = 2.5f;

            PointF? previous = null;
            for (var i = 0; i < Values.Count; i++)
            {
                var x = Values.Count == 1
                    ? pad
                    : pad + (width - pad * 2) * i / (Values.Count - 1f);
                var norm = (Values[i] - min) / range;
                var y = height - pad - (float)norm * (height - pad * 2);
                var current = new PointF(x, y);
                if (previous is PointF p)
                    canvas.DrawLine(p.X, p.Y, current.X, current.Y);
                previous = current;
            }
        }
    }
}