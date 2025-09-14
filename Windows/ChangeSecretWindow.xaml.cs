using System.Windows;

namespace BinanceUsdtTicker
{
    public partial class ChangeSecretWindow : Window
    {
        public string? SecretValue { get; private set; }

        public ChangeSecretWindow(string caption)
        {
            InitializeComponent();
            Title = caption;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (NewValue.Password != ConfirmValue.Password)
            {
                MessageBox.Show("Values do not match", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SecretValue = NewValue.Password;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
