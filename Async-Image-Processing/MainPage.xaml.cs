using System;
using System.Collections.Generic;
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