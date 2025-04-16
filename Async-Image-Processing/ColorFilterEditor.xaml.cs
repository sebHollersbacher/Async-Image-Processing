using CommunityToolkit.Maui.Views;

namespace Async_Image_Processing
{
    public partial class ColorFilterEditor : Popup
    {
        public ColorFilterEditor()
        {
            InitializeComponent();
            GenerateMatrixGrid();
        }

        private void GenerateMatrixGrid()
        {
            var columnHeaders = new[] { "Red", "Green", "Blue", "Alpha", "Offset" };
            var rowHeaders = new[] { "new Red", "new Green", "new Blue", "new Alpha" };
            
            for (int i = 0; i <= rowHeaders.Length; i++)
                MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i <= columnHeaders.Length; i++)
                MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // Header row
            MatrixGrid.Add(new Label { Text = "", FontAttributes = FontAttributes.Bold }, 0, 0);
            for (int col = 0; col < columnHeaders.Length; col++)
            {
                MatrixGrid.Add(new Label
                {
                    Text = columnHeaders[col],
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center
                }, col + 1, 0);
            }
            

            // Data rows
            for (int row = 0; row < rowHeaders.Length; row++)
            {
                // Header
                MatrixGrid.Add(new Label
                {
                    Text = rowHeaders[row],
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center
                }, 0, row + 1);

                // Fields
                for (int col = 0; col < columnHeaders.Length; col++)
                {
                    var entry = new Entry
                    {
                        Keyboard = Keyboard.Numeric,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Text = row == col ? "1" : "0",
                        Margin = 2
                    };

                    MatrixGrid.Add(entry, col + 1, row + 1);
                }
            }
        }

        private float[] GetMatrixValues()
        {
            int rows = MatrixGrid.RowDefinitions.Count;
            int cols = MatrixGrid.ColumnDefinitions.Count;
            float[] values = new float[(rows-1) * (cols-1)];

            foreach (var child in MatrixGrid.Children)
            {
                if (child is Entry entry)
                {
                    int row = Grid.GetRow(entry);
                    int col = Grid.GetColumn(entry);
                    int index = (row-1) * (cols-1) + (col-1);

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