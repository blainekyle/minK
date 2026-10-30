using Avalonia.Controls;
using Avalonia.Interactivity;
using mink.Controls;
using System;

namespace mink.Views
{
    public partial class PreviewWindow : Window
    {
        private bool _dockRequested;
        public event Action<bool>? WindowClosingWithAction;

        public IWebSurface PreviewSurface => DetachedPreviewSurface;

        public PreviewWindow()
        {
            InitializeComponent();
            Closed += OnWindowClosed;
        }

        private void OnDockBackClick(object? sender, RoutedEventArgs e)
        {
            _dockRequested = true;
            Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            WindowClosingWithAction?.Invoke(_dockRequested);
        }
    }
}
