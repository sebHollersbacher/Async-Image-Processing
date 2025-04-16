using CommunityToolkit.Maui.Views;

namespace Async_Image_Processing
{
    public partial class ImageFilterEditor : Popup
    {
        public ImageFilterEditor()
        {
            InitializeComponent();
            SizePicker.SelectedIndex = 0;
        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            if (SizePicker.SelectedItem is int size)
            {
                GenerateMatrixGrid(size);
            }
        }

        private void GenerateMatrixGrid(int size)
        {
            MatrixGrid.Children.Clear();
            MatrixGrid.RowDefinitions.Clear();
            MatrixGrid.ColumnDefinitions.Clear();

            for (int i = 0; i < size; i++)
            {
                MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    var entry = new Entry
                    {
                        Keyboard = Keyboard.Numeric,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Placeholder = "0",
                        Margin = 2
                    };

                    MatrixGrid.Add(entry, col, row);
                }
            }
        }

        private float[] GetMatrixValues()
        {
            int size = MatrixGrid.RowDefinitions.Count;
            float[] values = new float[size * size];

            foreach (var child in MatrixGrid.Children)
            {
                if (child is Entry entry)
                {
                    int row = Grid.GetRow(entry);
                    int col = Grid.GetColumn(entry);
                    int index = row * size + col;

                    values[index] = float.TryParse(entry.Text, out var v) ? v : 0f;
                }
            }

            return values;
        }

        private async void OnOkClicked(object sender, EventArgs e)
        {
            await CloseAsync(GetMatrixValues(), CancellationToken.None);
        }
    }
}