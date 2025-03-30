using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Async_Image_Processing
{
    public partial class MainPage
    {
        public ObservableCollection<ImageSource> ImagesList { get; set; } = new();
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
            // Worker Thread
            ImagesList.Clear();
            int loadedImages = 0;
            string[] imageFiles = Directory.GetFiles(_imagesDirectory, "*.jpg");

            IProgress<ImageSource> progress = new Progress<ImageSource>(image =>
            {
                // on UI thread
                ImagesList.Add(image);
                ImageProgressBar.Progress = (double)++loadedImages / imageFiles.Length;
            });

            // Worker Threads
            await Parallel.ForEachAsync(imageFiles, cancellationToken, async (file, token) =>
            {
                // Worker Thread
                var imageSource = ImageSource.FromFile(file);
                progress.Report(imageSource);

                await Task.Delay(100, token); // Delay to not overwhelm UI
            });
            
            // On UI Thread
            await MainThread.InvokeOnMainThreadAsync(() => ImageProgressBar.Progress = 1);
        }
    }
}