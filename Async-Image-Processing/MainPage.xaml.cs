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
            if (source is not StreamImageSource streamSource)
                return source;

            var s = streamSource.Stream;
            // Get the image stream
            using var originalStream = await streamSource.Stream(CancellationToken.None);

            // Load into a memory stream so we can safely read it multiple times if needed
            var memoryStream = new MemoryStream();
            await originalStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Decode image with SkiaSharp
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
                    0,     0,     0,     1, 0
                })
            };

            canvas.DrawBitmap(bitmap, 0, 0, paint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            if (data == null)
                throw new InvalidOperationException("Failed to encode image.");

            var imageBytes = data.ToArray();

            // Return ImageSource with a new MemoryStream each time it's requested
            return ImageSource.FromStream(() => new MemoryStream(imageBytes));
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
                ImageSource imgSource1 = null;
                
                foreach (string file in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var stream = File.OpenRead(file))
                    {
                        // Create a SKBitmap from the stream
                        using (var originalBitmap = SKBitmap.Decode(stream))
                        {
                            // Define the maximum width and height for the downscaled image
                            int maxWidth = 128;  // For example, scale to 800px width
                            int maxHeight = 128; // For example, scale to 800px height

                            // Calculate the scaling factor
                            float scalingFactor = Math.Min((float)maxWidth / originalBitmap.Width, (float)maxHeight / originalBitmap.Height);

                            if (scalingFactor < 1)
                            {
                                // Downscale the image
                                int newWidth = (int)(originalBitmap.Width * scalingFactor);
                                int newHeight = (int)(originalBitmap.Height * scalingFactor);
                                using (var resizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.None))
                                {
                                    // Convert resized bitmap to Maui ImageSource
                                    using (var image = SKImage.FromBitmap(resizedBitmap))
                                    {
                                        // Encode image to SKData
                                        using (var data = image.Encode())
                                        {
                                            var imageBytes = data.ToArray(); // cache the bytes
                                            ImageSource imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                                            imgSource1 = imageSource;
                                            loadedImages++;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // If image does not need resizing, just add original
                                imgSource1 = ImageSource.FromFile(file);
                                loadedImages++;
                            }
                        }
                    }
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ImagesList.Add(imgSource1);
                        ImageProgressBar.Progress = (double)loadedImages / imageFiles.Length;
                    });

                    // if (images.Count > BatchSize || loadedImages >= imageFiles.Length)
                    // {
                    //     // Update UI in UI-Threead
                    //     await MainThread.InvokeOnMainThreadAsync(() =>
                    //     {
                    //         foreach (var image in images)
                    //             ImagesList.Add(image);
                    //         ImageProgressBar.Progress = (double)loadedImages / imageFiles.Length;
                    //     });
                    //     // Continue with worker thread
                    //     // await Task.Delay(Delay, cancellationToken);
                    //     images.Clear();
                    // }
                }

                await MainThread.InvokeOnMainThreadAsync(() => ImageProgressBar.Progress = 1);
            }, cancellationToken);
        }
    }
}