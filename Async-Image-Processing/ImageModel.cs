using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Async_Image_Processing;

public sealed class ImageModel(ImageSource displayImage, string originalPath) : INotifyPropertyChanged
{
    public string OriginalPath { get; set; } = originalPath;

    private ImageSource _displayImage = displayImage;

    public ImageSource DisplayImage
    {
        get => _displayImage;
        set
        {
            if (_displayImage != value)
            {
                _displayImage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}