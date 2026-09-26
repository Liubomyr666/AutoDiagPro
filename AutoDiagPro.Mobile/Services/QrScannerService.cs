#if IOS
using Foundation;
using ImageIO;
using UIKit;
using Vision;
#endif

namespace AutoDiagPro.Mobile.Services;

public sealed class QrScannerService
{
    public async Task<string?> CaptureAndDecodeAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new InvalidOperationException("Камера недоступна.");

        var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
        {
            Title = "QR / штрихкод детали"
        });
        if (photo is null) return null;

        var temp = Path.Combine(FileSystem.CacheDirectory,
            "partcode_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Path.GetExtension(photo.FileName));

        await using (var source = await photo.OpenReadAsync())
        await using (var target = File.Create(temp))
            await source.CopyToAsync(target);

        try
        {
            return DecodeImage(temp);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }

    public async Task<string?> PickAndDecodeAsync()
    {
        var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
        {
            Title = "Выберите фото QR / штрихкода"
        });
        if (photo is null) return null;

        var temp = Path.Combine(FileSystem.CacheDirectory,
            "partcode_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Path.GetExtension(photo.FileName));

        await using (var source = await photo.OpenReadAsync())
        await using (var target = File.Create(temp))
            await source.CopyToAsync(target);

        try
        {
            return DecodeImage(temp);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }

    private static string? DecodeImage(string path)
    {
#if IOS
        using var image = UIImage.FromFile(path);
        if (image?.CGImage is null)
            throw new InvalidOperationException("Не удалось открыть изображение.");

        string? payload = null;
        NSError? visionError = null;

        using var request = new VNDetectBarcodesRequest((req, err) =>
        {
            if (err is not null)
            {
                visionError = err;
                return;
            }

            if (req is VNDetectBarcodesRequest barcodeRequest)
            {
                payload = barcodeRequest.Results?
                    .Select(x => x.PayloadStringValue)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }
        });

        using var handler = new VNImageRequestHandler(
            image.CGImage,
            CGImagePropertyOrientation.Up,
            new NSDictionary());

        if (!handler.Perform(new VNRequest[] { request }, out var performError))
            throw new InvalidOperationException("Vision: " + (performError?.LocalizedDescription ?? "ошибка распознавания."));

        if (visionError is not null)
            throw new InvalidOperationException("Vision: " + visionError.LocalizedDescription);

        return string.IsNullOrWhiteSpace(payload) ? null : payload.Trim();
#else
        return null;
#endif
    }
}