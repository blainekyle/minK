using Avalonia.Controls;
using Avalonia.Interactivity;
using mink.Models;
using System;
using System.Globalization;

namespace mink.Views
{
    public partial class PreferencesWindow : Window
    {
        private readonly AppSettings _current;
        public AppSettings? ResultSettings { get; private set; }

        public PreferencesWindow() : this(new AppSettings())
        {
        }

        public PreferencesWindow(AppSettings current)
        {
            InitializeComponent();
            _current = current;

            FontSizeBox.Text = current.FontSize.ToString(CultureInfo.InvariantCulture);
            TabSizeCombo.SelectedIndex = current.TabSize == 4 ? 1 : 0;
            WordWrapCheck.IsChecked = current.WordWrap;
            ShowMinimapCheck.IsChecked = current.ShowMinimap;
            AutoCloseCheck.IsChecked = current.AutoCloseTagsAndBrackets;

            DefaultPreviewModeCombo.SelectedIndex = current.DefaultLivePreview ? 0 : 1;
            ShowPreviewStartupCheck.IsChecked = current.ShowPreviewOnStartup;
            ShowAgentStartupCheck.IsChecked = current.ShowAgentOnStartup;

            ConfirmClearChatCheck.IsChecked = current.ConfirmClearChat;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            ResultSettings = null;
            Close(false);
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (!int.TryParse(FontSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fontSize))
            {
                fontSize = 14;
            }
            fontSize = Math.Clamp(fontSize, 9, 36);

            int tabSize = TabSizeCombo.SelectedIndex == 1 ? 4 : 2;
            bool defaultLive = DefaultPreviewModeCombo.SelectedIndex != 1;

            ResultSettings = new AppSettings
            {
                Ollama = new OllamaConfig
                {
                    BaseUrl = _current.Ollama.BaseUrl,
                    ModelName = _current.Ollama.ModelName,
                    Temperature = _current.Ollama.Temperature,
                    ContextWindow = _current.Ollama.ContextWindow
                },
                Theme = _current.Theme,
                FontSize = fontSize,
                TabSize = tabSize,
                WordWrap = WordWrapCheck.IsChecked ?? true,
                ShowMinimap = ShowMinimapCheck.IsChecked ?? true,
                AutoCloseTagsAndBrackets = AutoCloseCheck.IsChecked ?? true,
                DefaultLivePreview = defaultLive,
                ShowPreviewOnStartup = ShowPreviewStartupCheck.IsChecked ?? true,
                ShowAgentOnStartup = ShowAgentStartupCheck.IsChecked ?? true,
                ConfirmClearChat = ConfirmClearChatCheck.IsChecked ?? true
            };

            Close(true);
        }
    }
}
