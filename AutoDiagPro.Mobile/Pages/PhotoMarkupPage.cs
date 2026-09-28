using CoreGraphics;
using Foundation;
using Microsoft.Maui.Graphics;
using UIKit;

namespace AutoDiagPro.Mobile.Pages;

public sealed class PhotoMarkupPage : ContentPage
{
    private readonly byte[] _original;
    private readonly UIImage _nativeImage;
    private readonly MarkupDrawable _drawable;
    private readonly GraphicsView _graphics;
    private readonly TaskCompletionSource<byte[]?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<byte[]?> Completion => _completion.Task;

    public PhotoMarkupPage(byte[] original)
    {
        _original = original;
        _nativeImage = UIImage.LoadFromData(NSData.FromArray(original))
            ?? throw new InvalidOperationException("Не удалось открыть фото.");
        _drawable = new MarkupDrawable((float)_nativeImage.Size.Width, (float)_nativeImage.Size.Height);
        _graphics = new GraphicsView
        {
            Drawable = _drawable,
            BackgroundColor = Colors.Transparent
        };

        Title = "Разметка повреждений";
        BackgroundColor = Theme.Page;
        Shell.SetNavBarIsVisible(this, true);

        _graphics.StartInteraction += (_, e) => StartStroke(e);
        _graphics.DragInteraction += (_, e) => ContinueStroke(e);
        _graphics.EndInteraction += (_, _) => EndStroke();
        _graphics.CancelInteraction += (_, _) => EndStroke();

        var preview = new Grid
        {
            HeightRequest = 470,
            BackgroundColor = Colors.Black
        };
        preview.Add(new Microsoft.Maui.Controls.Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(_original)),
            Aspect = Aspect.AspectFit
        });
        preview.Add(_graphics);

        var undo = Theme.SecondaryButton("Отменить линию");
        undo.Clicked += (_, _) =>
        {
            _drawable.Undo();
            _graphics.Invalidate();
        };

        var clear = Theme.SecondaryButton("Очистить");
        clear.Clicked += (_, _) =>
        {
            _drawable.Clear();
            _graphics.Invalidate();
        };

        var originalButton = Theme.SecondaryButton("Без разметки");
        originalButton.Clicked += async (_, _) => await FinishAsync(_original);

        var save = Theme.PrimaryButton("Сохранить разметку");
        save.Clicked += async (_, _) =>
        {
            try
            {
                await FinishAsync(RenderMarkedPhoto());
            }
            catch (Exception ex)
            {
                await DisplayAlert("Разметка", ex.Message, "OK");
            }
        };

        var tools = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        tools.Add(undo, 0, 0);
        tools.Add(clear, 1, 0);
        tools.Add(originalButton, 0, 1);
        tools.Add(save, 1, 1);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(14, 14, 14, 30),
                Spacing = 12,
                Children =
                {
                    Theme.Eyebrow("PHOTO DAMAGE MARKUP"),
                    Theme.H1("Отметьте повреждения"),
                    Theme.MutedText("Проведите пальцем по царапине, вмятине или другой зоне. Красная разметка сохранится прямо на копии фото."),
                    Theme.CardView(preview, new Thickness(4), 18),
                    tools
                }
            }
        };
    }

    protected override bool OnBackButtonPressed()
    {
        _completion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    private void StartStroke(TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        var point = Normalize(e.Touches[0]);
        if (point is null) return;
        _drawable.Start(point.Value);
        _graphics.Invalidate();
    }

    private void ContinueStroke(TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        var point = Normalize(e.Touches[0]);
        if (point is null) return;
        _drawable.Add(point.Value);
        _graphics.Invalidate();
    }

    private void EndStroke() => _drawable.End();

    private PointF? Normalize(PointF p)
    {
        var width = (float)_graphics.Width;
        var height = (float)_graphics.Height;
        if (width <= 1 || height <= 1) return null;

        var rect = MarkupDrawable.ImageRect(
            width,
            height,
            (float)_nativeImage.Size.Width,
            (float)_nativeImage.Size.Height);

        if (p.X < rect.X || p.X > rect.Right || p.Y < rect.Y || p.Y > rect.Bottom)
            return null;

        return new PointF(
            Math.Clamp((p.X - rect.X) / rect.Width, 0, 1),
            Math.Clamp((p.Y - rect.Y) / rect.Height, 0, 1));
    }

    private byte[] RenderMarkedPhoto()
    {
        if (_drawable.Strokes.Count == 0)
            return _original;

        UIGraphics.BeginImageContextWithOptions(_nativeImage.Size, false, (nfloat)1.0);
        try
        {
            _nativeImage.Draw(new CGRect(0, 0, _nativeImage.Size.Width, _nativeImage.Size.Height));
            var context = UIGraphics.GetCurrentContext()
                ?? throw new InvalidOperationException("Не удалось создать слой разметки.");

            context.SetStrokeColor(UIColor.SystemRed.CGColor);
            context.SetLineWidth((nfloat)Math.Max(4, (double)_nativeImage.Size.Width / 180d));
            context.SetLineCap(CGLineCap.Round);
            context.SetLineJoin(CGLineJoin.Round);

            foreach (var stroke in _drawable.Strokes.Where(x => x.Count > 0))
            {
                context.BeginPath();
                var first = stroke[0];
                context.MoveTo(
                    (nfloat)(first.X * (float)_nativeImage.Size.Width),
                    (nfloat)(first.Y * (float)_nativeImage.Size.Height));

                foreach (var point in stroke.Skip(1))
                {
                    context.AddLineToPoint(
                        (nfloat)(point.X * (float)_nativeImage.Size.Width),
                        (nfloat)(point.Y * (float)_nativeImage.Size.Height));
                }
                context.StrokePath();
            }

            var marked = UIGraphics.GetImageFromCurrentImageContext()
                ?? throw new InvalidOperationException("Не удалось сохранить разметку.");
            using var data = marked.AsJPEG(0.92f)
                ?? throw new InvalidOperationException("Не удалось создать JPEG.");
            return data.ToArray();
        }
        finally
        {
            UIGraphics.EndImageContext();
        }
    }

    private async Task FinishAsync(byte[]? result)
    {
        _completion.TrySetResult(result);
        await Navigation.PopModalAsync();
    }

    private sealed class MarkupDrawable : IDrawable
    {
        private readonly float _imageWidth;
        private readonly float _imageHeight;
        private List<PointF>? _current;

        public List<List<PointF>> Strokes { get; } = new();

        public MarkupDrawable(float imageWidth, float imageHeight)
        {
            _imageWidth = imageWidth;
            _imageHeight = imageHeight;
        }

        public void Start(PointF point)
        {
            _current = new List<PointF> { point };
            Strokes.Add(_current);
        }

        public void Add(PointF point)
        {
            if (_current is null) Start(point);
            else _current.Add(point);
        }

        public void End() => _current = null;

        public void Undo()
        {
            _current = null;
            if (Strokes.Count > 0) Strokes.RemoveAt(Strokes.Count - 1);
        }

        public void Clear()
        {
            _current = null;
            Strokes.Clear();
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var rect = ImageRect(dirtyRect.Width, dirtyRect.Height, _imageWidth, _imageHeight);
            canvas.StrokeColor = Colors.Red;
            canvas.StrokeSize = 5;

            foreach (var stroke in Strokes)
            {
                for (var i = 1; i < stroke.Count; i++)
                {
                    var a = stroke[i - 1];
                    var b = stroke[i];
                    canvas.DrawLine(
                        rect.X + a.X * rect.Width,
                        rect.Y + a.Y * rect.Height,
                        rect.X + b.X * rect.Width,
                        rect.Y + b.Y * rect.Height);
                }
            }
        }

        public static RectF ImageRect(float viewWidth, float viewHeight, float imageWidth, float imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
                return new RectF(0, 0, viewWidth, viewHeight);

            var imageRatio = imageWidth / imageHeight;
            var viewRatio = viewWidth / viewHeight;

            if (imageRatio > viewRatio)
            {
                var height = viewWidth / imageRatio;
                return new RectF(0, (viewHeight - height) / 2f, viewWidth, height);
            }

            var width = viewHeight * imageRatio;
            return new RectF((viewWidth - width) / 2f, 0, width, viewHeight);
        }
    }
}
