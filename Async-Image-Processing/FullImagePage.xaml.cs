using SkiaSharp;

namespace Async_Image_Processing;

public partial class FullImagePage : ContentPage
{
    public FullImagePage(string imageUrl, List<SKPaint> filters)
    {
        InitializeComponent();

        using var stream = File.OpenRead(imageUrl);
        using var original = SKBitmap.Decode(stream);
        
        var current = original;
        foreach (var filter in filters)
        {
            var newCurrent = ImageTransformationHelper.Filter(current, filter);
            current.Dispose();
            current = newCurrent;
        }
                
        using var image = SKImage.FromBitmap(current);
        current.Dispose();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        FullImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
    }
}