using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using SkiaSharp;

namespace Async_Image_Processing
{
    public partial class MainPage
    {
        public ObservableCollection<ImageSource> ImagesList { get; } = [];
        private CancellationTokenSource? _cts;
        private const int image_resolution = 128;
        private string? _folderDirectory;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private void OnCancelLoadingClicked(object sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private async void OnGrayLoadingClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();
            
            try
            {
                await ConvertImagesAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                await DisplayAlert("Operation Canceled", "Converting to grayscale has been canceled", "OK");
            }
        }

        private Task ConvertImagesAsync(CancellationToken cancellationToken)
        {
            var copyList = ImagesList.ToList();
            ImageProgressBar.Progress = 0;
            var loadedImagesCount = 0;
            var total = copyList.Count;
            
            return Task.Run(async () =>
            {
                foreach (var image in copyList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var filtered = await ApplyGrayscaleAsync(image);
                    loadedImagesCount++;

                    var idx = copyList.IndexOf(image);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ImagesList[idx] = filtered;
                        ImageProgressBar.Progress = (double)loadedImagesCount / total;
                    });
                }
            }, cancellationToken);
        }

        private static async Task<ImageSource> ApplyGrayscaleAsync(ImageSource source)
        {
            if (source is not StreamImageSource streamSource)
                return source;

            await using var originalStream = await streamSource.Stream(CancellationToken.None);

            var memoryStream = new MemoryStream();
            await originalStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var skStream = new SKManagedStream(memoryStream);
            using var bitmap = SKBitmap.Decode(skStream);

            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;
            canvas.Clear();

            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(new[]
            {
                0.33f, 0.33f, 0.33f, 0, 0,
                0.33f, 0.33f, 0.33f, 0, 0,
                0.33f, 0.33f, 0.33f, 0, 0,
                0, 0, 0, 1, 0
            });

            canvas.DrawBitmap(bitmap, 0, 0, paint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            if (data == null)
                throw new InvalidOperationException("Failed to encode image.");

            var imageBytes = data.ToArray();

            return ImageSource.FromStream(() => new MemoryStream(imageBytes));
        }

        private async void OnBrowseFolderClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();
            
            var res = await FolderPicker.PickAsync(_cts.Token);

            if (res.Folder?.Path == null) return;
            _folderDirectory = res.Folder.Path;
            SelectedFolderPathEntry.Text = _folderDirectory;
        }

        private async void OnLoadImagesClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();

            try
            {
                await LoadImagesAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                await DisplayAlert("Operation Canceled", "Loading images has been canceled", "OK");
            }
        }

        private async Task LoadImagesAsync(CancellationToken cancellationToken)
        {
            // Run everything on a single worker thread
            if (_folderDirectory == null) return;

            await Task.Run(async () =>
            {
                ImagesList.Clear();
                var imageFiles = Directory.GetFiles(_folderDirectory, "*.jpg");
                var loadedImages = 0;
                ImageSource imageSource;

                foreach (var file in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using (var stream = File.OpenRead(file))
                    {
                        using (var originalBitmap = SKBitmap.Decode(stream))
                        {
                            float scalingFactor = Math.Min((float)image_resolution / originalBitmap.Width,
                                (float)image_resolution / originalBitmap.Height);

                            if (scalingFactor is < 1 and > 0)
                            {
                                var newWidth = (int)(originalBitmap.Width * scalingFactor);
                                var newHeight = (int)(originalBitmap.Height * scalingFactor);
                                using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight),
                                    SKSamplingOptions.Default);
                                using var image = SKImage.FromBitmap(resizedBitmap);
                                using var data = image.Encode();
                                var imageBytes = data.ToArray();
                                imageSource =
                                    ImageSource.FromStream(() => new MemoryStream(imageBytes));
                                loadedImages++;
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