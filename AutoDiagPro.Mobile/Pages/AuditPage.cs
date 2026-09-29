using AutoDiagPro.Mobile.Models;
using AutoDiagPro.Mobile.Services;

namespace AutoDiagPro.Mobile.Pages;

public sealed class AuditPage : ContentPage
{
    private readonly ApiService _api = AppServices.Get<ApiService>();
    private readonly VerticalStackLayout _items = new() { Spacing = 9 };
    private readonly Label _status = Theme.MutedText("Загрузка журнала...");

    public AuditPage()
    {
        Title = "Журнал действий";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        var refresh = Theme.CompactButton("Обновить");
        refresh.Clicked += async (_, _) => await LoadAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 34),
                Spacing = 14,
                Children =
                {
                    Theme.Eyebrow("AUDIT LOG"),
                    Theme.H1("Журнал действий"),
                    Theme.MutedText("Последние действия пользователей и изменения данных на AutoDiag Server."),
                    refresh,
                    _status,
                    _items
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await AccessPolicy.RequireAdminAsync(this)) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _items.Clear();
        _status.Text = "Загрузка...";
        _status.TextColor = Theme.Muted;

        try
        {
            var list = (await _api.GetAuditAsync())
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToList();

            foreach (var item in list)
                _items.Add(Card(item));

            if (list.Count == 0)
                _items.Add(Theme.CardView(Theme.MutedText("Журнал пока пуст.")));

            _status.Text = $"Записей: {list.Count}";
            _status.TextColor = Theme.Green;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.TextColor = Theme.Red;
        }
    }

    private static View Card(ServerAuditRecord item)
    {
        var details = string.Join(" • ", new[]
        {
            item.EntityType,
            item.EntityId,
            item.Details
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return Theme.CardView(new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label
                {
                    Text = item.Action,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Text
                },
                Theme.MutedText(item.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss")),
                string.IsNullOrWhiteSpace(details)
                    ? Theme.MutedText("Без дополнительных данных")
                    : Theme.MutedText(details)
            }
        }, new Thickness(13), 15);
    }
}
