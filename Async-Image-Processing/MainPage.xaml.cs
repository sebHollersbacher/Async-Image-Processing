using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using SkiaSharp;

namespace Async_Image_Processing
{
    public partial class MainPage
    {
        public ObservableCollection<ImageSource> ImagesList { get; } = new();
        private CancellationTokenSource _cts;
        private const int IMAGE_RESOLUTION = 128;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private void OnCancelLoadingClicked(object sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private void OnGrayLoadingClicked(object sender, EventArgs e)
        {
            var copyList = ImagesList.ToList();
            ImagesList.Clear();
            ImageProgressBar.Progress = 0;
            int loadedImagesCount = 0;
            var total = copyList.Count;
            
            Task.Run(async () =>
            {
                foreach (var image in copyList)
                {
                    var filtered = await ApplyGrayscaleAsync(image);
                    loadedImagesCount++;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ImagesList.Add(filtered);
                        ImageProgressBar.Progress = (double)loadedImagesCount / total;
                    });
                }
            });
        }

        private async Task<ImageSource> ApplyGrayscaleAsync(ImageSource source)
        {
            if (source is not StreamImageSource streamSource)
                return source;

            var s = streamSource.Stream;
            await using var originalStream = await streamSource.Stream(CancellationToken.None);

            var memoryStream = new MemoryStream();
            await originalStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            using var skStream = new SKManagedStream(memoryStream);
            using var bitmap = SKBitmap.Decode(skStream);

            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;
            canvas.Clear();

            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                {
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0, 0, 0, 1, 0
                })
            };

            canvas.DrawBitmap(bitmap, 0, 0, paint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            if (data == null)
                throw new InvalidOperationException("Failed to encode image.");

            var imageBytes = data.ToArray();

            return ImageSource.FromStream(() => new MemoryStream(imageBytes));
        }

        private async void OnLoadImagesClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();

            // TODO: proper checks
            var res = await FolderPicker.PickAsync(_cts.Token);
            var imagesDirectory = res.Folder.Path;

            try
            {
                await LoadImagesAsync(imagesDirectory, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // TODO: handle cancel
            }
        }

        private async Task LoadImagesAsync(string imagesDirectory, CancellationToken cancellationToken)
        {
            // Run everything on a single worker thread
            await Task.Run(async () =>
            {
                ImagesList.Clear();
                var imageFiles = Directory.GetFiles(imagesDirectory, "*.jpg");
                var loadedImages = 0;
                ImageSource imageSource;

                foreach (var file in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using (var stream = File.OpenRead(file))
                    {
                        using (var originalBitmap = SKBitmap.Decode(stream))
                        {
                            float scalingFactor = Math.Min((float)IMAGE_RESOLUTION / originalBitmap.Width,
                               (float)IMAGE_RESOLUTION / originalBitmap.Height);

                            if (scalingFactor is < 1 and > 0)
                            {
                                var newWidth = (int)(originalBitmap.Width * scalingFactor);
                                var newHeight = (int)(originalBitmap.Height * scalingFactor);
                                using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight),
                                    SKFilterQuality.None);
                                using (var image = SKImage.FromBitmap(resizedBitmap))
                                {
                                    using (var data = image.Encode())
                                    {
                                        var imageBytes = data.ToArray();
                                        imageSource =
                                            ImageSource.FromStream(() => new MemoryStream(imageBytes));
                                        loadedImages++;
                                    }
                                }
                            }
                            else
                            {
                                imageSource = ImageSource.FromFile(file);
                                loadedImages++;
                            }
                        }
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ImagesList.Add(imageSource);
                        ImageProgressBar.Progress = (double)loadedImages / imageFiles.Length;
                    });
                }

                await MainThread.InvokeOnMainThreadAsync(() => ImageProgressBar.Progress = 1);
            }, cancellationToken);
        }
    }
}