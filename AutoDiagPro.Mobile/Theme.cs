using Microsoft.Maui.Controls.Shapes;

namespace AutoDiagPro.Mobile;

public static class Theme
{
    public static readonly Color Page = Color.FromArgb("#090C0E");
    public static readonly Color Card = Color.FromArgb("#12171A");
    public static readonly Color Line = Color.FromArgb("#2A3137");
    public static readonly Color Text = Color.FromArgb("#EEF1F3");
    public static readonly Color Muted = Color.FromArgb("#8295A0");
    public static readonly Color Accent = Color.FromArgb("#E7A13B");
    public static readonly Color Green = Color.FromArgb("#40C98A");
    public static readonly Color Red = Color.FromArgb("#EE636B");

    public static Border CardView(View content, Thickness? padding = null) =>
        new()
        {
            BackgroundColor = Card,
            Stroke = Line,
            StrokeThickness = 1,
            Padding = padding ?? new Thickness(16),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = content
        };

    public static Label H1(string text) =>
        new() { Text = text, FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Text };

    public static Label MutedText(string text) =>
        new() { Text = text, FontSize = 12, TextColor = Muted };
}