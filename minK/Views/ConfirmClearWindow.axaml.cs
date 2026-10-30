using Avalonia.Controls;
using Avalonia.Interactivity;

namespace mink.Views
{
    public partial class ConfirmClearWindow : Window
    {
        public bool Confirmed { get; private set; }
        public bool DontAskAgain { get; private set; }

        public ConfirmClearWindow()
        {
            InitializeComponent();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DontAskAgain = false;
            Close(false);
        }

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DontAskAgain = DontAskAgainCheck.IsChecked == true;
            Close(true);
        }
    }
}
