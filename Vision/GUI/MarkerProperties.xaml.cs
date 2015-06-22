using System.Windows;

namespace Vision.GUI
{
    /// <summary>
    /// Interaction logic for MarkerProperties.xaml
    /// </summary>
    public partial class MarkerProperties : Window
    {
        public MarkerProperties()
        {
            InitializeComponent();
        }

        public ushort MarkerSize { get; private set; }
        public ushort MarkerWidth { get; private set; }
        public ushort MarkerHeight { get; private set; }

        private void OkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            ushort markerSize, width, height;
            if (!ushort.TryParse(MarkerSizeTextBox.Text, out markerSize) ||
                !ushort.TryParse(WidthTextBox.Text, out width) ||
                !ushort.TryParse(HeightTextBox.Text, out height))
            {
                MessageBox.Show(this, "Неверные данные метки", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
            else
            {
                MarkerSize = markerSize;
                MarkerWidth = width;
                MarkerHeight = height;
                DialogResult = true;
            }

            Close();
        }

        private void CancelButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
