using SkiaSharp;

namespace Async_Image_Processing;

public static class ImageTransformationHelper
{
    public enum FilterType
    {
        Grayscale,
        Sepia,
        Blur,
        Sharpen,
        Custom
    }

    public static async Task<ImageSource> ApplyFilterToImageSourceAsync(
        ImageSource source,
        SKPaint paint,
        CancellationToken cancellationToken)
    {
        if (source is not StreamImageSource streamSource)
            return source;

        await using var originalStream = await streamSource.Stream(cancellationToken);
        await using var memoryStream = new MemoryStream();
        await originalStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var skStream = new SKManagedStream(memoryStream);
        using var bitmap = SKBitmap.Decode(skStream);

        if (bitmap == null)
            throw new InvalidOperationException("Failed to decode bitmap.");

        using var filtered = Filter(bitmap, paint);

        using var image = SKImage.FromBitmap(filtered);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();

        return ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    public static SKBitmap Filter(SKBitmap bitmap, SKPaint filter)
    {
        var info = new SKImageInfo(bitmap.Width, bitmap.Height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear();

        canvas.DrawBitmap(bitmap, 0, 0, filter);
        var image = surface.Snapshot();
        var newBitmap = new SKBitmap(image.Width, image.Height);
        image.ReadPixels(newBitmap.Info, newBitmap.GetPixels(), newBitmap.RowBytes, 0, 0);
        return newBitmap;
    }

    public static SKPaint GetPaintForFilter(FilterType type, SKColorFilter? customColorFilter = null,
        SKImageFilter? customImageFilter = null)
    {
        var paint = new SKPaint();
        switch (type)
        {
            case FilterType.Grayscale:
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(new[]
                {
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0, 0, 0, 1, 0
                });
                break;

            case FilterType.Sepia:
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(new[]
                {
                    0.393f, 0.769f, 0.189f, 0, 0,
                    0.349f, 0.686f, 0.168f, 0, 0,
                    0.272f, 0.534f, 0.131f, 0, 0,
                    0, 0, 0, 1, 0
                });
                break;

            case FilterType.Blur:
                paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                    new SKSizeI(3, 3),
                    [
                        0.11f, 0.11f, 0.11f,
                        0.11f, 0.11f, 0.11f,
                        0.11f, 0.11f, 0.11f
                    ],
                    1f,
                    0f,
                    new SKPointI(1, 1),
                    SKShaderTileMode.Clamp,
                    false);
                break;

            case FilterType.Sharpen:
                paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                    new SKSizeI(3, 3),
                    [
                        0, -1, 0,
                        -1, 5, -1,
                        0, -1, 0
                    ],
                    1f,
                    0f,
                    new SKPointI(1, 1),
                    SKShaderTileMode.Clamp,
                    false);
                break;
            case FilterType.Custom:
                if (customColorFilter != null)
                {
                    paint.ColorFilter = customColorFilter;
                }

                if (customImageFilter != null)
                {
                    paint.ImageFilter = customImageFilter;
                }

                break;
        }

        return paint;
    }
}