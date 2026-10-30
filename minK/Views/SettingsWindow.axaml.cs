using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using mink.Models;
using mink.Services;
using System.Globalization;
using System.Threading.Tasks;

namespace mink.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _current;
        private readonly OllamaClient _ollamaClient;
        public AppSettings? ResultSettings { get; private set; }

        public SettingsWindow() : this(new AppSettings(), new OllamaClient())
        {
        }

        public SettingsWindow(AppSettings current, OllamaClient ollamaClient)
        {
            InitializeComponent();
            _current = current;
            _ollamaClient = ollamaClient;

            BaseUrlBox.Text = current.Ollama.BaseUrl;
            ModelNameBox.Text = current.Ollama.ModelName;
            TemperatureBox.Text = current.Ollama.Temperature.ToString("0.##", CultureInfo.InvariantCulture);

            string ctxStr = current.Ollama.ContextWindow.ToString(CultureInfo.InvariantCulture);
            ContextWindowCombo.SelectedIndex = ctxStr switch
            {
                "4096" => 0,
                "16384" => 2,
                "32768" => 3,
                _ => 1
            };
        }

        private async void OnTestConnectionClick(object? sender, RoutedEventArgs e)
        {
            string url = OllamaClient.NormalizeBaseUrl(BaseUrlBox.Text ?? "http://localhost:11434");
            BaseUrlBox.Text = url;

            TestConnectionBtn.IsEnabled = false;
            ConnectionStatusText.IsVisible = true;
            ConnectionStatusText.Foreground = SolidColorBrush.Parse("#CCCCCC");
            ConnectionStatusText.Text = $"Probing {url} ...";

            var (reachable, models, error) = await Task.Run(() => _ollamaClient.ProbeServerAsync(url));
            TestConnectionBtn.IsEnabled = true;

            if (reachable)
            {
                ConnectionStatusText.Foreground = SolidColorBrush.Parse("#3FB950");
                ConnectionStatusText.Text = models.Count > 0
                    ? $"● Connected! Found {models.Count} model(s) on {url}."
                    : $"● Connected to {url} (no models pulled yet).";

                DiscoveredModelsCombo.ItemsSource = models;
                if (models.Count > 0)
                {
                    string currentModel = (ModelNameBox.Text ?? "").Trim();
                    int matchIdx = models.FindIndex(m => string.Equals(m, currentModel, System.StringComparison.OrdinalIgnoreCase));
                    DiscoveredModelsCombo.SelectedIndex = matchIdx >= 0 ? matchIdx : 0;
                }
            }
            else
            {
                ConnectionStatusText.Foreground = SolidColorBrush.Parse("#F85149");
                ConnectionStatusText.Text = $"● Unreachable ({error ?? "Check server URL & OLLAMA_HOST=0.0.0.0 for LAN"}).";
            }
        }

        private void OnDiscoveredModelSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (DiscoveredModelsCombo.SelectedItem is string selectedModel && !string.IsNullOrWhiteSpace(selectedModel))
            {
                ModelNameBox.Text = selectedModel;
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            ResultSettings = null;
            Close(false);
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TemperatureBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double temp))
            {
                temp = 0.2;
            }
            temp = System.Math.Clamp(temp, 0.0, 2.0);

            int contextWindow = 8192;
            if (ContextWindowCombo.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int parsedCtx))
            {
                contextWindow = parsedCtx;
            }

            ResultSettings = new AppSettings
            {
                Ollama = new OllamaConfig
                {
                    BaseUrl = OllamaClient.NormalizeBaseUrl(BaseUrlBox.Text ?? "http://localhost:11434"),
                    ModelName = string.IsNullOrWhiteSpace(ModelNameBox.Text) ? "qwen2.5-coder:7b" : ModelNameBox.Text.Trim(),
                    Temperature = temp,
                    ContextWindow = contextWindow
                },
                Theme = _current.Theme,
                FontSize = _current.FontSize,
                TabSize = _current.TabSize,
                WordWrap = _current.WordWrap,
                ShowMinimap = _current.ShowMinimap,
                AutoCloseTagsAndBrackets = _current.AutoCloseTagsAndBrackets,
                DefaultLivePreview = _current.DefaultLivePreview,
                ShowPreviewOnStartup = _current.ShowPreviewOnStartup,
                ShowAgentOnStartup = _current.ShowAgentOnStartup,
                ConfirmClearChat = _current.ConfirmClearChat
            };

            Close(true);
        }
    }
}
