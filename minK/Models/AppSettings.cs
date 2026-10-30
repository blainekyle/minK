namespace mink.Models
{
    public class OllamaConfig
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ModelName { get; set; } = "qwen2.5-coder:7b";
        public double Temperature { get; set; } = 0.2;
        public int ContextWindow { get; set; } = 8192;
    }

    public class AppSettings
    {
        public OllamaConfig Ollama { get; set; } = new OllamaConfig();

        // Editor Preferences
        public string Theme { get; set; } = "mink-dark";
        public int FontSize { get; set; } = 14;
        public int TabSize { get; set; } = 2;
        public bool WordWrap { get; set; } = true;
        public bool ShowMinimap { get; set; } = true;
        public bool AutoCloseTagsAndBrackets { get; set; } = true;

        // Workspace & Preview Preferences
        public bool DefaultLivePreview { get; set; } = true;
        public bool ShowPreviewOnStartup { get; set; } = true;
        public bool ShowAgentOnStartup { get; set; } = true;

        // Dialogs & Confirmations
        public bool ConfirmClearChat { get; set; } = true;
    }
}
