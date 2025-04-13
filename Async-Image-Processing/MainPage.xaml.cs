﻿using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using SkiaSharp;

namespace Async_Image_Processing
{
    public partial class MainPage
    {
        private const int image_resolution = 128;

        public ObservableCollection<ImageModel> ImagesList { get; } = [];
        private CancellationTokenSource? _cts;
        private string? _folderDirectory;
        private List<SKPaint> _filters = [];

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private void OnCancelLoadingClicked(object sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private async void OnGrayscaleClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();

            IProgress<(ImageSource, int, double)> progress = new Progress<(ImageSource, int, double)>(tuple =>
            {
                (ImageSource newImage, int index, double progress) = tuple;
                ImagesList[index].DisplayImage = newImage;
                UpdateProgressBar(progress);
            });

            try
            {
                await ConvertImagesAsync(_cts.Token, progress);
            }
            catch (OperationCanceledException)
            {
                await DisplayAlert("Operation Canceled", "Converting to grayscale has been canceled", "OK");
                // TODO: restore default
            }
        }

        private Task ConvertImagesAsync(CancellationToken cancellationToken,
            IProgress<(ImageSource, int, double)>? imageChangedProgress = null)
        {
            var copyList = ImagesList.ToList();
            ImageProgressBar.Progress = 0;
            var loadedImagesCount = 0;
            var total = copyList.Count;

            return Task.Run(async () =>
            {
                var filter =
                    ImageTransformationHelper.GetPaintForFilter(ImageTransformationHelper.FilterType.Grayscale);
                _filters.Add(filter);

                foreach (var image in copyList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var filtered =
                        await ImageTransformationHelper.ApplyFilterToImageSourceAsync(image.DisplayImage, filter,
                            cancellationToken);
                    loadedImagesCount++;

                    imageChangedProgress?.Report((filtered, copyList.IndexOf(image), loadedImagesCount / (double)total));
                }
            }, cancellationToken);
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

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();

            var res = await FolderPicker.PickAsync(_cts.Token);

            if (res.Folder?.Path == null) return;
            var saveFolder = res.Folder.Path;

            var total = ImagesList.Count;
            var saved = 0;
            IProgress<double> progress = new Progress<double>(UpdateProgressBar);

            // TODO: fix cancel
            await Task.Run(() =>
            {
                Parallel.ForEach(ImagesList,
                    image =>
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        // Load the original
                        using var original = SKBitmap.Decode(image.OriginalPath);

                        // Apply all filters
                        var processed = original;
                        foreach (var filter in _filters)
                        {
                            processed = ImageTransformationHelper.Filter(processed, filter);
                        }

                        // Define save path
                        var fileName = Path.GetFileName(image.OriginalPath);
                        var savePath = Path.Combine(saveFolder, fileName);

                        // Save it
                        using var fs = File.OpenWrite(savePath);
                        processed.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(fs);

                        Interlocked.Increment(ref saved);
                        progress.Report(saved / (double)total);
                    });
            }, _cts.Token);
        }

        private async void OnLoadImagesClicked(object sender, EventArgs e)
        {
            _cts?.CancelAsync();
            _cts = new CancellationTokenSource();

            IProgress<(ImageModel, double)>? progress = new Progress<(ImageModel, double)>(tuple =>
            {
                (ImageModel model, double progress) = tuple;
                ImagesList.Add(model);
                UpdateProgressBar(progress);
            });

            try
            {
                await LoadImagesAsync(_cts.Token, progress);
            }
            catch (OperationCanceledException)
            {
                await DisplayAlert("Operation Canceled", "Loading images has been canceled", "OK");
            }
        }

        private async Task LoadImagesAsync(CancellationToken cancellationToken,
            IProgress<(ImageModel, double)> imageLoadedProgress)
        {
            if (_folderDirectory == null) return;

            await Task.Run(async () =>
            {
                ImagesList.Clear();
                var imageFiles = Directory.GetFiles(_folderDirectory, "*.jpg");
                var loadedImages = 0;

                foreach (var file in imageFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ImageSource imageSource;
                    await using (var stream = File.OpenRead(file))
                    {
                        using (var originalBitmap = SKBitmap.Decode(stream))
                        {
                            var scalingFactor = Math.Min((float)image_resolution / originalBitmap.Width,
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

                    var model = new ImageModel(imageSource, file);
                    imageLoadedProgress.Report((model, (double)loadedImages / imageFiles.Length));
                }
            }, cancellationToken);
        }

        private void UpdateProgressBar(double percentage)
        {
            ImageProgressBar.Progress = percentage;
        }
    }
}