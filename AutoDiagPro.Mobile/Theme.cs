using Microsoft.Maui.Controls.Shapes;

namespace AutoDiagPro.Mobile;

public static class Theme
{
    public static readonly Color Page = Color.FromArgb("#090C0E");
    public static readonly Color Surface = Color.FromArgb("#0D1114");
    public static readonly Color Card = Color.FromArgb("#12171A");
    public static readonly Color Card2 = Color.FromArgb("#171C20");
    public static readonly Color Line = Color.FromArgb("#2A3137");
    public static readonly Color Text = Color.FromArgb("#F3F4F5");
    public static readonly Color TextSoft = Color.FromArgb("#B8C0C5");
    public static readonly Color Muted = Color.FromArgb("#7F8A91");
    public static readonly Color Accent = Color.FromArgb("#E7A13B");
    public static readonly Color AccentSoft = Color.FromArgb("#2B2115");
    public static readonly Color Green = Color.FromArgb("#40C98A");
    public static readonly Color Red = Color.FromArgb("#EE636B");

    public static Border CardView(View content, Thickness? padding = null, double radius = 16) =>
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
        new() { Text = text, FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Text };

    public static Label H2(string text) =>
        new() { Text = text, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Text };

    public static Label Eyebrow(string text) =>
        new() { Text = text.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Muted, CharacterSpacing = 1.1 };

    public static Label MutedText(string text) =>
        new() { Text = text, FontSize = 12, TextColor = Muted, LineBreakMode = LineBreakMode.WordWrap };

    public static Label Body(string text) =>
        new() { Text = text, FontSize = 13, TextColor = TextSoft, LineBreakMode = LineBreakMode.WordWrap };

    public static Border Pill(string text, Color? color = null)
    {
        var c = color ?? Accent;
        return new Border
        {
            BackgroundColor = Color.FromRgba(c.Red, c.Green, c.Blue, 0.10f),
            Stroke = Color.FromRgba(c.Red, c.Green, c.Blue, 0.34f),
            StrokeThickness = 1,
            Padding = new Thickness(10, 5),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label { Text = text, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = c }
        };
    }

    public static Button PrimaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Accent,
        TextColor = Color.FromArgb("#111315"),
        CornerRadius = 12,
        HeightRequest = 48,
        FontAttributes = FontAttributes.Bold
    };

    public static Button SecondaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Card2,
        TextColor = Text,
        BorderColor = Line,
        BorderWidth = 1,
        CornerRadius = 12,
        HeightRequest = 46,
        FontAttributes = FontAttributes.Bold
    };

    public static Button CompactButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Surface,
        TextColor = TextSoft,
        BorderColor = Line,
        BorderWidth = 1,
        CornerRadius = 10,
        HeightRequest = 40,
        FontSize = 12
    };
}