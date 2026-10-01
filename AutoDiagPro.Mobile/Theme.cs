using Microsoft.Maui.Controls.Shapes;

namespace AutoDiagPro.Mobile;

public static class Theme
{
    public static readonly Color Page = Color.FromArgb("#070B10");
    public static readonly Color Surface = Color.FromArgb("#0B1118");
    public static readonly Color Card = Color.FromArgb("#101720");
    public static readonly Color Card2 = Color.FromArgb("#151E28");
    public static readonly Color Line = Color.FromArgb("#263545");
    public static readonly Color Text = Color.FromArgb("#F4F8FC");
    public static readonly Color TextSoft = Color.FromArgb("#C4D2DE");
    public static readonly Color Muted = Color.FromArgb("#7890A3");
    public static readonly Color Accent = Color.FromArgb("#2F80FF");
    public static readonly Color AccentSoft = Color.FromArgb("#0C2442");
    public static readonly Color Green = Color.FromArgb("#35D08A");
    public static readonly Color Red = Color.FromArgb("#FF6473");

    public static Border CardView(View content, Thickness? padding = null, double radius = 18) =>
        new()
        {
            BackgroundColor = Card,
            Stroke = Line,
            StrokeThickness = 1,
            Padding = padding ?? new Thickness(16),
            StrokeShape = new RoundRectangle { CornerRadius = radius },
            Content = content
        };

    public static Border SoftCard(View content, Thickness? padding = null) =>
        new()
        {
            BackgroundColor = Surface,
            Stroke = Line,
            StrokeThickness = 1,
            Padding = padding ?? new Thickness(14),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = content
        };

    public static Label H1(string text) =>
        new()
        {
            Text = text,
            FontSize = 25,
            FontAttributes = FontAttributes.Bold,
            TextColor = Text,
            MaxLines = 2,
            FontAutoScalingEnabled = false
        };

    public static Label H2(string text) =>
        new()
        {
            Text = text,
            FontSize = 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = Text,
            FontAutoScalingEnabled = false
        };

    public static Label Eyebrow(string text) =>
        new()
        {
            Text = text.ToUpperInvariant(),
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Muted,
            CharacterSpacing = 0.8,
            FontAutoScalingEnabled = false
        };

    public static Label MutedText(string text) =>
        new()
        {
            Text = text,
            FontSize = 12,
            TextColor = Muted,
            LineBreakMode = LineBreakMode.WordWrap,
            FontAutoScalingEnabled = false
        };

    public static Label Body(string text) =>
        new()
        {
            Text = text,
            FontSize = 13,
            TextColor = TextSoft,
            LineBreakMode = LineBreakMode.WordWrap,
            FontAutoScalingEnabled = false
        };

    public static Border Pill(string text, Color? color = null)
    {
        var c = color ?? Accent;
        return new Border
        {
            BackgroundColor = Color.FromRgba(c.Red, c.Green, c.Blue, 0.10f),
            Stroke = Color.FromRgba(c.Red, c.Green, c.Blue, 0.30f),
            StrokeThickness = 1,
            Padding = new Thickness(9, 4),
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = text,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = c,
                FontAutoScalingEnabled = false
            }
        };
    }

    public static Button PrimaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Accent,
        TextColor = Color.FromArgb("#111315"),
        CornerRadius = 14,
        HeightRequest = 46,
        FontAttributes = FontAttributes.Bold,
        FontAutoScalingEnabled = false
    };

    public static Button SecondaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Card2,
        TextColor = Text,
        BorderColor = Line,
        BorderWidth = 1,
        CornerRadius = 14,
        HeightRequest = 46,
        FontAttributes = FontAttributes.Bold,
        FontAutoScalingEnabled = false
    };

    public static Button CompactButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Surface,
        TextColor = TextSoft,
        BorderColor = Line,
        BorderWidth = 1,
        CornerRadius = 10,
        HeightRequest = 38,
        FontSize = 12,
        FontAutoScalingEnabled = false
    };
}
