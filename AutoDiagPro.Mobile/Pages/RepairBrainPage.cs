using AutoDiagPro.Mobile.Services;
using AutoDiagPro.Mobile.Services.Obd;

namespace AutoDiagPro.Mobile.Pages;

public sealed class RepairBrainPage : ContentPage
{
    private readonly MobileState _state = AppServices.Get<MobileState>();
    private readonly MobileWorkspaceStore _store = AppServices.Get<MobileWorkspaceStore>();
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly Elm327Service _obd = AppServices.Get<Elm327Service>();

    private readonly Label _vehicle = Theme.MutedText("Автомобиль не выбран");
    private readonly Label _status = Theme.MutedText("Готово.");
    private readonly VerticalStackLayout _dtcCards = new() { Spacing = 9 };
    private readonly Editor _complaint = Field("Жалоба / симптомы");
    private readonly Editor _cause = Field("Подтверждённая причина");
    private readonly Editor _repair = Field("Что сделано");
    private readonly Editor _notes = Field("Заметки / измерения");
    private readonly Label _proof = Theme.MutedText("Скан ДО/ПОСЛЕ ещё не сравнивался.");
    private readonly Label _beforeDtcMetric = MetricValue();
    private readonly Label _resolvedDtcMetric = MetricValue();
    private readonly Label _remainingDtcMetric = MetricValue();
    private readonly Label _newDtcMetric = MetricValue();
    private readonly Label _comparisonVerdict = Theme.MutedText("Запишите scan ДО и контрольный scan ПОСЛЕ.");
    private readonly Image _beforePhoto = new() { HeightRequest = 150, Aspect = Aspect.AspectFill, IsVisible = false };
    private readonly Image _afterPhoto = new() { HeightRequest = 150, Aspect = Aspect.AspectFill, IsVisible = false };
    private RepairCaseMobile? _case;
    private RepairScanComparisonMobile? _comparison;

    public RepairBrainPage()
    {
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);
        Title = "Repair Brain";

        var save = Theme.PrimaryButton("Сохранить кейс");
        save.Clicked += async (_, _) => await SaveAsync();

        var before = Theme.SecondaryButton("Записать scan ДО");
        before.Clicked += async (_, _) => await CaptureBeforeAsync();

        var after = Theme.SecondaryButton("Контрольный scan ПОСЛЕ");
        after.Clicked += async (_, _) => await CaptureAfterAsync();

        var shareReport = Theme.PrimaryButton("Поделиться отчётом ДО / ПОСЛЕ");
        shareReport.Clicked += async (_, _) => await ShareComparisonReportAsync();

        var ai = Theme.SecondaryButton("AI: план ремонта");
        ai.Clicked += async (_, _) => await AskAiAsync();

        var photoBefore = Theme.SecondaryButton("Фото ДО");
        photoBefore.Clicked += async (_, _) => await CapturePhotoAsync(true);

        var photoAfter = Theme.SecondaryButton("Фото ПОСЛЕ");
        photoAfter.Clicked += async (_, _) => await CapturePhotoAsync(false);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("REPAIR BRAIN"),
                    Theme.H1("Диагностика → ремонт → контроль"),
                    _vehicle,
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            Theme.Eyebrow("КЕЙС РЕМОНТА"),
                            _complaint, _cause, _repair, _notes,
                            save
                        }
                    }),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            Theme.Eyebrow("ДО / ПОСЛЕ"),
                            before,
                            after,
                            BuildComparisonMetrics(),
                            _comparisonVerdict,
                            _proof,
                            shareReport
                        }
                    }),
                    Theme.CardView(new VerticalStackLayout
                    {
                        Spacing = 9,
                        Children =
                        {
                            Theme.Eyebrow("ФОТО РЕМОНТА"),
                            BuildPhotoGrid(),
                            new HorizontalStackLayout { Spacing = 8, Children = { photoBefore, photoAfter } }
                        }
                    }),
                    ai,
                    _status,
                    Theme.H2("Ошибки и проверки"),
                    _dtcCards
                }
            }
        };
    }

    private View BuildComparisonMetrics()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        grid.Add(MetricCard("DTC ДО", _beforeDtcMetric), 0, 0);
        grid.Add(MetricCard("ИСПРАВЛЕНО", _resolvedDtcMetric), 1, 0);
        grid.Add(MetricCard("ОСТАЛОСЬ", _remainingDtcMetric), 0, 1);
        grid.Add(MetricCard("НОВЫЕ", _newDtcMetric), 1, 1);
        return grid;
    }

    private static View MetricCard(string title, Label value) =>
        Theme.CardView(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Theme.Eyebrow(title),
                value
            }
        }, new Thickness(12));

    private static Label MetricValue() => new()
    {
        Text = "—",
        FontSize = 22,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Text
    };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireStaffAsync(this)) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null)
        {
            _vehicle.Text = "Сначала выберите автомобиль.";
            _vehicle.TextColor = Theme.Accent;
            _dtcCards.Clear();
            return;
        }

        _vehicle.Text = $"{v.DisplayName} • VIN {v.Vin ?? "—"}";
        _vehicle.TextColor = Theme.TextSoft;
        _case = await _store.GetOrCreateRepairCaseAsync(v.Id);

        _complaint.Text = _case.Complaint;
        _cause.Text = _case.ConfirmedCause;
        _repair.Text = _case.RepairDone;
        _notes.Text = _case.Notes;
        RefreshProof();
        RefreshPhotos();
        RenderDtcCards();
    }

    private void RenderDtcCards()
    {
        _dtcCards.Clear();
        var codes = _state.LastDtcCodes.Count > 0
            ? _state.LastDtcCodes
            : (_case?.DtcCodes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        if (codes.Count == 0)
        {
            _dtcCards.Add(Theme.CardView(Theme.MutedText("Нет DTC в текущем кейсе. Запустите диагностику или scan ДО.")));
            return;
        }

        foreach (var code in codes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var s = DtcRepairAdvisor.Analyze(code);
            var text =
                "Что проверить:\n• " + string.Join("\n• ", s.Checks) +
                "\n\nДетали только после подтверждения:\n• " + string.Join("\n• ", s.PossibleParts);

            var ask = Theme.CompactButton("AI по этой ошибке");
            ask.Clicked += async (_, _) =>
            {
                _state.PendingAiQuestion =
                    $"Разбери DTC {code} для выбранного автомобиля. Сначала объясни причину и проверки, затем ремонт. " +
                    "Не предлагай менять деталь без подтверждения. Если нужна деталь — объясни точный подбор по VIN/OEM.";
                await Shell.Current.GoToAsync("ai");
            };

            _dtcCards.Add(Theme.CardView(new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    new Label { Text = s.Code, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Theme.Accent },
                    new Label { Text = s.Summary, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text },
                    Theme.Body(text),
                    ask
                }
            }, new Thickness(14)));
        }
    }

    private async Task SaveAsync()
    {
        if (_case is null) return;
        _case.Complaint = _complaint.Text?.Trim() ?? "";
        _case.ConfirmedCause = _cause.Text?.Trim() ?? "";
        _case.RepairDone = _repair.Text?.Trim() ?? "";
        _case.Notes = _notes.Text?.Trim() ?? "";
        _case.DtcCodes = string.Join(", ", _state.LastDtcCodes);
        _case.UpdatedAt = DateTimeOffset.Now;
        _case.Status = string.IsNullOrWhiteSpace(_case.ConfirmedCause) ? "Диагностика" :
            string.IsNullOrWhiteSpace(_case.RepairDone) ? "Причина подтверждена" : "Ремонт";

        var db = await _store.LoadAsync();
        await _store.SaveAsync(db);
        _status.Text = "Кейс сохранён.";
        _status.TextColor = Theme.Green;
    }

    private async Task CaptureBeforeAsync()
    {
        if (_case is null) return;
        if (!_obd.IsConnected)
        {
            _status.Text = "Подключите OBD в разделе «Диагностика».";
            _status.TextColor = Theme.Accent;
            return;
        }

        try
        {
            _status.Text = "Записываю полный scan ДО...";
            _status.TextColor = Theme.Accent;

            var snapshot = await CaptureRepairSnapshotAsync();
            _case.BeforeSnapshot = snapshot;
            _case.BeforeScan = RepairScanComparisonService.BuildSnapshotText(snapshot);
            _case.DtcCodes = string.Join(", ", snapshot.AllDtc);
            _case.UpdatedAt = DateTimeOffset.Now;

            _state.LastDtcCodes = snapshot.AllDtc.ToList();
            _state.LastDiagnosticSummary = _case.BeforeScan;
            _state.LastDiagnosticAtUtc = DateTimeOffset.Now;

            await _store.SaveAsync(await _store.LoadAsync());
            RenderDtcCards();
            RefreshProof();

            _status.Text = $"Scan ДО сохранён • DTC {snapshot.AllDtc.Count} • Live {snapshot.Live.Count}.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task CaptureAfterAsync()
    {
        if (_case is null) return;
        if (!_obd.IsConnected)
        {
            _status.Text = "Подключите OBD в разделе «Диагностика».";
            _status.TextColor = Theme.Accent;
            return;
        }

        try
        {
            _status.Text = "Записываю контрольный scan ПОСЛЕ...";
            _status.TextColor = Theme.Accent;

            var snapshot = await CaptureRepairSnapshotAsync();
            _case.AfterSnapshot = snapshot;
            _case.AfterScan = RepairScanComparisonService.BuildSnapshotText(snapshot);
            _case.UpdatedAt = DateTimeOffset.Now;

            _state.LastDtcCodes = snapshot.AllDtc.ToList();
            _state.LastDiagnosticSummary = _case.AfterScan;
            _state.LastDiagnosticAtUtc = DateTimeOffset.Now;

            if (snapshot.AllDtc.Count == 0 && !string.IsNullOrWhiteSpace(_case.RepairDone))
                _case.Status = "Закрыт";

            await _store.SaveAsync(await _store.LoadAsync());
            RenderDtcCards();
            RefreshProof();

            _status.Text = snapshot.AllDtc.Count == 0
                ? "Контрольный scan: стандартных DTC нет."
                : $"После ремонта DTC: {string.Join(", ", snapshot.AllDtc)}";
            _status.TextColor = snapshot.AllDtc.Count == 0 ? Theme.Green : Theme.Accent;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private async Task<RepairScanSnapshotMobile> CaptureRepairSnapshotAsync()
    {
        var confirmed = await _obd.DtcAsync();
        var pending = await _obd.PendingDtcAsync();
        var permanent = await _obd.PermanentDtcAsync();
        var live = await _obd.LiveSnapshotAsync();
        var vin = await _obd.VinAsync();
        var protocol = await _obd.ProtocolAsync();
        var voltage = await _obd.VoltageAsync();

        var expectedVin = (_state.SelectedVehicle?.Vin ?? "").Trim().ToUpperInvariant();
        var actualVin = (vin ?? "").Trim().ToUpperInvariant();
        if (expectedVin.Length == 17 && actualVin.Length == 17 &&
            !string.Equals(expectedVin, actualVin, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"VIN не совпадает: выбран {expectedVin}, ECU {actualVin}. Scan не сохранён.");

        return new RepairScanSnapshotMobile
        {
            CapturedAt = DateTimeOffset.Now,
            Vin = actualVin,
            Protocol = protocol ?? "",
            Voltage = voltage ?? "",
            ConfirmedDtc = confirmed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PendingDtc = pending.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PermanentDtc = permanent.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Live = new Dictionary<string, string>(live, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void RefreshProof()
    {
        if (_case is null) return;

        _comparison = null;
        _beforeDtcMetric.Text = "—";
        _resolvedDtcMetric.Text = "—";
        _remainingDtcMetric.Text = "—";
        _newDtcMetric.Text = "—";

        if (_case.BeforeSnapshot is not null && _case.AfterSnapshot is not null)
        {
            _comparison = RepairScanComparisonService.Compare(
                _case.BeforeSnapshot,
                _case.AfterSnapshot);

            _beforeDtcMetric.Text = _comparison.BeforeCount.ToString();
            _resolvedDtcMetric.Text = _comparison.Resolved.Count.ToString();
            _remainingDtcMetric.Text = _comparison.Remaining.Count.ToString();
            _newDtcMetric.Text = _comparison.Added.Count.ToString();
            _comparisonVerdict.Text = RepairScanComparisonService.Verdict(_comparison);
            _comparisonVerdict.TextColor =
                _comparison.Added.Count == 0 && _comparison.Remaining.Count == 0
                    ? Theme.Green
                    : Theme.Accent;

            var vehicle = _state.SelectedVehicle?.DisplayName ?? "Автомобиль";
            _proof.Text = RepairScanComparisonService.BuildReport(
                _comparison,
                vehicle,
                _complaint.Text,
                _cause.Text,
                _repair.Text);
            return;
        }

        _comparisonVerdict.Text = "Запишите scan ДО и контрольный scan ПОСЛЕ.";
        _comparisonVerdict.TextColor = Theme.TextSoft;

        if (string.IsNullOrWhiteSpace(_case.BeforeScan) &&
            string.IsNullOrWhiteSpace(_case.AfterScan))
        {
            _proof.Text = "Скан ДО/ПОСЛЕ ещё не сравнивался.";
            return;
        }

        _proof.Text =
            "ДО:\n" + (string.IsNullOrWhiteSpace(_case.BeforeScan) ? "—" : _case.BeforeScan) +
            "\n\nПОСЛЕ:\n" + (string.IsNullOrWhiteSpace(_case.AfterScan) ? "—" : _case.AfterScan);
    }

    private async Task ShareComparisonReportAsync()
    {
        if (_comparison is null)
        {
            await DisplayAlert(
                "До / После",
                "Сначала запишите scan ДО и контрольный scan ПОСЛЕ.",
                "OK");
            return;
        }

        var vehicle = _state.SelectedVehicle?.DisplayName ?? "Автомобиль";
        var text = RepairScanComparisonService.BuildReport(
            _comparison,
            vehicle,
            _complaint.Text,
            _cause.Text,
            _repair.Text);

        var vin = _comparison.After.Vin;
        if (string.IsNullOrWhiteSpace(vin)) vin = "vehicle";
        var path = Path.Combine(
            FileSystem.CacheDirectory,
            $"AutoDiag_BEFORE_AFTER_{vin}_{DateTime.Now:yyyyMMdd_HHmm}.txt");
        await File.WriteAllTextAsync(path, text);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "AutoDiag Pro — До / После ремонта",
            File = new ShareFile(path)
        });
    }

    private async Task AskAiAsync()
    {
        var v = _state.SelectedVehicle;
        if (v is null || _case is null) return;

        _status.Text = "AutoDiag AI анализирует кейс...";
        _status.TextColor = Theme.Accent;

        try
        {
            var context =
                $"Автомобиль: {v.DisplayName}; VIN: {v.Vin}; пробег: {v.MileageKm} км.\n" +
                $"Жалоба: {_complaint.Text}\nDTC: {_case.DtcCodes}\nScan ДО:\n{_case.BeforeScan}\n" +
                $"Подтверждённая причина: {_cause.Text}\nЧто сделано: {_repair.Text}\nScan ПОСЛЕ:\n{_case.AfterScan}";

            var result = await _api.AskAiAsync(
                "Ты Repair Brain AutoDiag Pro. Составь практический план диагностики и ремонта: факты, вероятные причины, проверки от дешёвых к дорогим, что считать подтверждением причины, какие детали могут понадобиться, и что проверить контрольным scan. Не выдумывай OEM номера.",
                context,
                true);

            _notes.Text = string.IsNullOrWhiteSpace(_notes.Text)
                ? result.Answer
                : _notes.Text + "\n\nAI:\n" + result.Answer;
            _status.Text = "AI-план добавлен в заметки.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }



    private View BuildPhotoGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        grid.Add(_beforePhoto, 0, 0);
        grid.Add(_afterPhoto, 1, 0);
        return grid;
    }

    private async Task CapturePhotoAsync(bool before)
    {
        if (_case is null) return;

        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                _status.Text = "Камера недоступна на этом устройстве.";
                _status.TextColor = Theme.Accent;
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = before ? "Фото ДО ремонта" : "Фото ПОСЛЕ ремонта"
            });

            if (photo is null) return;

            var folder = Path.Combine(FileSystem.AppDataDirectory, "repair_photos");
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
            var target = Path.Combine(folder, $"{_case.Id}_{(before ? "before" : "after")}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}");

            await using (var source = await photo.OpenReadAsync())
            await using (var destination = File.Create(target))
                await source.CopyToAsync(destination);

            if (before) _case.BeforePhotoPath = target;
            else _case.AfterPhotoPath = target;

            _case.UpdatedAt = DateTimeOffset.Now;
            await _store.SaveAsync(await _store.LoadAsync());
            RefreshPhotos();

            _status.Text = before ? "Фото ДО сохранено." : "Фото ПОСЛЕ сохранено.";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = "Фото: " + ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private void RefreshPhotos()
    {
        if (_case is null) return;

        _beforePhoto.IsVisible = !string.IsNullOrWhiteSpace(_case.BeforePhotoPath) && File.Exists(_case.BeforePhotoPath);
        _afterPhoto.IsVisible = !string.IsNullOrWhiteSpace(_case.AfterPhotoPath) && File.Exists(_case.AfterPhotoPath);

        if (_beforePhoto.IsVisible) _beforePhoto.Source = ImageSource.FromFile(_case.BeforePhotoPath);
        if (_afterPhoto.IsVisible) _afterPhoto.Source = ImageSource.FromFile(_case.AfterPhotoPath);
    }

    private static Editor Field(string placeholder) => new()
    {
        Placeholder = placeholder,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 72,
        BackgroundColor = Theme.Surface,
        TextColor = Theme.Text,
        PlaceholderColor = Theme.Muted
    };
}