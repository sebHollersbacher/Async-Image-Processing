using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Views;
using SkiaSharp;

namespace Async_Image_Processing
{
    public partial class MainPage
    {
        private const int image_resolution = 128;

        public ObservableCollection<ImageModel> ImagesList { get; } = [];
        private CancellationTokenSource? _cancellationTokenSource;
        private string? _folderDirectory;
        private readonly List<SKPaint> _filters = [];

        private SKColorFilter? _customColorFilter;
        private SKImageFilter? _customImageFilter;

        public IEnumerable<ImageTransformationHelper.FilterType> FilterTypes =>
            Enum.GetValues<ImageTransformationHelper.FilterType>();

        public bool IsCustomFilterSelected => SelectedFilter == ImageTransformationHelper.FilterType.Custom;
        private ImageTransformationHelper.FilterType _selectedFilter;

        public ImageTransformationHelper.FilterType SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomFilterSelected));
                }
            }
        }

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        private async void OnEditImageFilterClicked(object sender, EventArgs e)
        {
            var popup = new ImageFilterEditor();
            var result = await this.ShowPopupAsync(popup);
            if (result is float[] matrixData)
            {
                var size = (int)Math.Sqrt(matrixData.Length);
                _customImageFilter = SKImageFilter.CreateMatrixConvolution(
                    new SKSizeI(size, size),
                    matrixData,
                    1f,
                    0f,
                    new SKPointI(1, 1),
                    SKShaderTileMode.Clamp,
                    false);
            }
        }

        private async void OnEditColorFilterClicked(object sender, EventArgs e)
        {
            var popup = new ColorFilterEditor();
            var result = await this.ShowPopupAsync(popup);
            if (result is float[] matrixData)
            {
                _customColorFilter = SKColorFilter.CreateColorMatrix(matrixData);
            }
        }

        private async void OnImageSelected(object sender, SelectionChangedEventArgs e)
        {
            var selectedImage = e.CurrentSelection.FirstOrDefault() as ImageModel;
            if (selectedImage == null)
                return;

            await Navigation.PushAsync(new FullImagePage(selectedImage.OriginalPath, _filters));
            ((CollectionView)sender).SelectedItem = null;
        }

        private void OnCancelLoadingClicked(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private async void OnGrayscaleClicked(object sender, EventArgs e)
        {
            _cancellationTokenSource?.CancelAsync();
            _cancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<(ImageSource, int, double)>(tuple =>
            {
                (ImageSource newImage, int index, double progress) = tuple;
                ImagesList[index].DisplayImage = newImage;
                UpdateProgressBar(progress);
            });

            var originalImages = ImagesList
                .ToDictionary(img => img, img => img.DisplayImage);
            try
            {
                await ConvertImagesAsync(_cancellationTokenSource.Token, progress);
            }
            catch (OperationCanceledException)
            {
                await DisplayAlert("Operation Canceled", "Converting to grayscale has been canceled", "OK");
                foreach (var originalImage in originalImages)
                {
                    originalImage.Key.DisplayImage = originalImage.Value;
                }
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
                    ImageTransformationHelper.GetPaintForFilter(SelectedFilter, _customColorFilter, _customImageFilter);
                _filters.Add(filter);
                foreach (var image in copyList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var filtered =
                        await ImageTransformationHelper.ApplyFilterToImageSourceAsync(image.DisplayImage, filter,
                            cancellationToken);
                    loadedImagesCount++;

                    imageChangedProgress?.Report((filtered, copyList.IndexOf(image),
                        loadedImagesCount / (double)total));
                }
            }, cancellationToken);
        }

        private async void OnBrowseFolderClicked(object sender, EventArgs e)
        {
            _cancellationTokenSource?.CancelAsync();
            _cancellationTokenSource = new CancellationTokenSource();

            var res = await FolderPicker.PickAsync(_cancellationTokenSource.Token);

            if (res.Folder?.Path == null) return;
            _folderDirectory = res.Folder.Path;
            SelectedFolderPathEntry.Text = _folderDirectory;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            _cancellationTokenSource?.CancelAsync();
            _cancellationTokenSource = new CancellationTokenSource();

            var res = await FolderPicker.PickAsync(_cancellationTokenSource.Token);

            if (res.Folder?.Path == null) return;
            var saveFolder = res.Folder.Path;

            IProgress<double> progress = new Progress<double>(UpdateProgressBar);

            try
            {
                await SaveImages(saveFolder, _cancellationTokenSource.Token, progress);
            }
            catch (Exception ex) when (
                ex is OperationCanceledException ||
                (ex is AggregateException aggEx &&
                 aggEx.InnerExceptions.All(exception => exception is OperationCanceledException)))
            {
                await DisplayAlert("Operation Canceled", "Saving Images was canceled.", "OK");
            }
        }
        
        private Task SaveImages(string saveFolder, CancellationToken cancellationToken,
            IProgress<double>? imageSavedProgress = null)
        {
            var total = ImagesList.Count;
            var saved = 0;
            return Task.Run(() =>
            {
                Parallel.ForEach(
                    ImagesList,
                    new ParallelOptions
                        { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount },
                    image =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using var original = SKBitmap.Decode(image.OriginalPath);
                        var processed = original;

                        foreach (var filter in _filters)
                        {
                            var next = ImageTransformationHelper.Filter(processed, filter);
                            processed.Dispose();
                            processed = next;
                        }

                        var fileName = Path.GetFileName(image.OriginalPath);
                        var savePath = Path.Combine(saveFolder, fileName);

                        using var fs = File.OpenWrite(savePath);
                        processed.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(fs);
                        processed.Dispose();

                        Interlocked.Increment(ref saved);
                        imageSavedProgress?.Report(saved / (double)total);
                    });
            }, cancellationToken);
        }

        private async void OnLoadImagesClicked(object sender, EventArgs e)
        {
            _cancellationTokenSource?.CancelAsync();
            _cancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<(ImageModel, double)>(tuple =>
            {
                (ImageModel model, double progress) = tuple;
                ImagesList.Add(model);
                UpdateProgressBar(progress);
            });

            try
            {
                await LoadImagesAsync(_cancellationTokenSource.Token, progress);
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
                _filters.Clear();
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