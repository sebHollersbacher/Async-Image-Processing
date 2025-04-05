using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Async_Image_Processing
{
    public partial class MainPage
    {
        private const int BatchSize = 5;   // Number of images loaded at once
        private const int Delay = 50;   // Delay for smoother UI
        
        public ObservableCollection<ImageSource> ImagesList { get; } = new();
        private string _imagesDirectory;
        private CancellationTokenSource _cts;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private void OnCancelLoadingClicked(object sender, EventArgs e)
        {
            _cts?.Cancel();
            ImageProgressBar.Progress = 0;
        }

        private async void OnGrayLoadingClicked(object sender, EventArgs e)
        {
            var filteredImages = new List<ImageSource>();

            foreach (var image in ImagesList)
            {
                var filtered = await ApplyGrayscaleAsync(image);
                filteredImages.Add(filtered);
            }

            ImagesList.Clear();

            foreach (var filtered in filteredImages)
                ImagesList.Add(filtered);
        }
        
        private async Task<ImageSource> ApplyGrayscaleAsync(ImageSource source)
        {
            if (source is not FileImageSource fileSource)
                return source; // Skip non-file images

            using var stream = File.OpenRead(fileSource.File);  // Open the image file stream
            using var skStream = new SKManagedStream(stream);
            using var bitmap = SKBitmap.Decode(skStream);       // Decode it into a SkiaSharp Bitmap

            // Create a surface to apply the filter on
            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;
            canvas.Clear();

            // Apply grayscale color filter
            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                {
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0.33f, 0.33f, 0.33f, 0, 0,
                    0,     0,     0,     1, 0
                })
            };

            // Draw the bitmap with the grayscale filter applied
            canvas.DrawBitmap(bitmap, 0, 0, paint);
            using var image = surface.Snapshot();
    
            // Ensure we get the correct encoded image data
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            if (data == null) 
                throw new InvalidOperationException("Failed to encode image.");

            // Convert encoded data into a MemoryStream
            var imageBytes = data.ToArray();  // Convert to byte array
            return ImageSource.FromStream(() => new MemoryStream(imageBytes));  // Return the ImageSource
        }
        
        private async void OnLoadImagesClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();

            // TODO: proper checks
            var res = await FolderPicker.PickAsync(_cts.Token);
            _imagesDirectory = res.Folder.Path;

            try
            {
                await LoadImagesAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // TODO: handle cancel
            }
        }

        private async Task LoadImagesAsync(CancellationToken cancellationToken)
        {
            // Run everything on a single worker thread
            await Task.Run(async () => 
            {
                ImagesList.Clear();
                string[] imageFiles = Directory.GetFiles(_imagesDirectory, "*.jpg");
                int loadedImages = 0;
                List<ImageSource> images = new();
                
                foreach (string file in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    images.Add(ImageSource.FromFile(file));
                    loadedImages++;

                    if (images.Count > BatchSize || loadedImages >= imageFiles.Length)
                    {
                        // Update UI in UI-Threead
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            foreach (var image in images)
                                ImagesList.Add(image);
                            ImageProgressBar.Progress = (double)loadedImages / imageFiles.Length;
                        });
                        // Continue with worker thread
                        await Task.Delay(Delay, cancellationToken);
                        images.Clear();
                    }
                }

                await MainThread.InvokeOnMainThreadAsync(() => ImageProgressBar.Progress = 1);
            }, cancellationToken);
        }
    }
}