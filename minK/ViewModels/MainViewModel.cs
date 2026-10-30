using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using mink.Controls;
using mink.Models;
using mink.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace mink.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private readonly OllamaClient _ollamaClient = new();
    private readonly DiffAndPatchService _diffService = new();

    private AppSettings _settings;
    private DocumentTab? _activeTab;
    private DocumentTab? _lastHtmlTab;

    private bool _isExplorerVisible;
    private bool _isPreviewVisible = true;
    private bool _isPreviewDetached;
    private bool _isLivePreviewMode = true;
    private bool _isChatVisible = true;

    private string _chatInput = "";
    private bool _isAgentEditMode = true;
    private bool _isGenerating;
    private bool _isOllamaOnline;
    private string _openedFolderName = "NO FOLDER OPENED";

    private CancellationTokenSource? _generationCts;
    private CancellationTokenSource? _previewDebounceCts;
    private int _untitledCounter = 1;
    private long _saveRevision = 1;

    public IWebSurface? EditorSurface { get; private set; }
    public IWebSurface? DockedPreviewSurface { get; private set; }
    public IWebSurface? DetachedPreviewSurface { get; private set; }

    // UI Dialog & Window Hooks
    public Func<Task<string[]?>>? PickOpenFilesDialog { get; set; }
    public Func<string, Task<string?>>? PickSaveFileDialog { get; set; }
    public Func<Task<string?>>? PickOpenFolderDialog { get; set; }
    public Func<AppSettings, OllamaClient, Task<AppSettings?>>? ShowSettingsDialog { get; set; }
    public Func<AppSettings, Task<AppSettings?>>? ShowPreferencesDialog { get; set; }
    public Func<Task<(bool Confirmed, bool DontAskAgain)>>? ConfirmClearChatDialog { get; set; }
    public Action? RequestOpenDetachedPreviewWindow { get; set; }
    public Action? RequestCloseDetachedPreviewWindow { get; set; }
    public Action? ScrollChatToBottom { get; set; }

    public ObservableCollection<DocumentTab> Tabs { get; } = new();
    public ObservableCollection<ChatTurn> ChatTurns { get; } = new();
    public ObservableCollection<ExplorerFileItem> FolderFiles { get; } = new();

    public AppSettings Settings => _settings;
    public OllamaClient OllamaClient => _ollamaClient;

    public DocumentTab? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != null)
            {
                _activeTab.PropertyChanged -= OnActiveTabPropertyChanged;
            }

            if (SetProperty(ref _activeTab, value))
            {
                if (_activeTab != null)
                {
                    _activeTab.PropertyChanged += OnActiveTabPropertyChanged;
                    if (_activeTab.Language is "html" or "xml")
                    {
                        _lastHtmlTab = _activeTab;
                    }
                    _ = SyncActiveTabToMonacoAsync(_activeTab);
                }

                NotifyActiveTabComputedProperties();
                _ = RefreshPreviewImmediateAsync();
            }
        }
    }

    public bool IsExplorerVisible
    {
        get => _isExplorerVisible;
        set => SetProperty(ref _isExplorerVisible, value);
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (SetProperty(ref _isPreviewVisible, value))
            {
                OnPropertyChanged(nameof(IsDockedPreviewVisible));
                if (_isPreviewVisible)
                {
                    _ = RefreshPreviewImmediateAsync();
                }
            }
        }
    }

    public bool IsPreviewDetached
    {
        get => _isPreviewDetached;
        set
        {
            if (SetProperty(ref _isPreviewDetached, value))
            {
                OnPropertyChanged(nameof(IsDockedPreviewVisible));
            }
        }
    }

    public bool IsDockedPreviewVisible => _isPreviewVisible && !_isPreviewDetached;

    public bool IsLivePreviewMode
    {
        get => _isLivePreviewMode;
        set
        {
            if (SetProperty(ref _isLivePreviewMode, value))
            {
                OnPropertyChanged(nameof(LiveModeButtonBg));
                OnPropertyChanged(nameof(SavedModeButtonBg));
                OnPropertyChanged(nameof(PreviewModeStatusText));
                _ = RefreshPreviewImmediateAsync();
            }
        }
    }

    public IBrush LiveModeButtonBg => _isLivePreviewMode
        ? SolidColorBrush.Parse("#0E639C")
        : Brushes.Transparent;

    public IBrush SavedModeButtonBg => !_isLivePreviewMode
        ? SolidColorBrush.Parse("#0E639C")
        : Brushes.Transparent;

    public string PreviewModeStatusText => _isLivePreviewMode
        ? "Preview: Live Edits"
        : "Preview: Last Saved State";

    public bool IsChatVisible
    {
        get => _isChatVisible;
        set
        {
            if (SetProperty(ref _isChatVisible, value))
            {
                OnPropertyChanged(nameof(AgentSidebarIconFill));
            }
        }
    }

    public IBrush AgentSidebarIconFill => _isChatVisible
        ? SolidColorBrush.Parse("#CCCCCC")
        : Brushes.Transparent;

    public string ChatInput
    {
        get => _chatInput;
        set => SetProperty(ref _chatInput, value);
    }

    public bool IsAgentEditMode
    {
        get => _isAgentEditMode;
        set
        {
            if (SetProperty(ref _isAgentEditMode, value))
            {
                OnPropertyChanged(nameof(AgentModePillBg));
                OnPropertyChanged(nameof(AgentModeLabel));
            }
        }
    }

    public IBrush AgentModePillBg => _isAgentEditMode
        ? SolidColorBrush.Parse("#1F472A")
        : SolidColorBrush.Parse("#2D2D30");

    public string AgentModeLabel => _isAgentEditMode
        ? "✎ Agent Edits: ON"
        : "💬 Ask Only";

    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                OnPropertyChanged(nameof(IsNotGenerating));
            }
        }
    }

    public bool IsNotGenerating => !_isGenerating;

    public bool IsOllamaOnline
    {
        get => _isOllamaOnline;
        set
        {
            if (SetProperty(ref _isOllamaOnline, value))
            {
                OnPropertyChanged(nameof(OllamaDotBrush));
                OnPropertyChanged(nameof(OllamaStatusBarText));
            }
        }
    }

    public IBrush OllamaDotBrush => _isOllamaOnline
        ? SolidColorBrush.Parse("#3FB950")
        : SolidColorBrush.Parse("#F85149");

    public string OllamaHeaderSummary => $"{_settings.Ollama.ModelName}";
    public string OllamaEndpointShort => _settings.Ollama.BaseUrl.Replace("http://", "").Replace("https://", "");

    public string OllamaStatusBarText => _isOllamaOnline
        ? $"● Ollama: {_settings.Ollama.ModelName} ({OllamaEndpointShort})"
        : $"○ Ollama Offline ({OllamaEndpointShort}) — Click to Configure";

    public string OpenedFolderName
    {
        get => _openedFolderName;
        set => SetProperty(ref _openedFolderName, value);
    }

    public string WindowTitle => _activeTab != null
        ? $"{_activeTab.DisplayTitle} — minK"
        : "minK";

    public string CursorStatusText => _activeTab != null
        ? $"Ln {_activeTab.CursorLine}, Col {_activeTab.CursorColumn}"
        : "Ln 1, Col 1";

    public string ActiveLanguageDisplay => _activeTab?.LanguageDisplay ?? "HTML";
    public bool ActiveTabHasPendingDiffs => _activeTab?.HasPendingDiffs ?? false;
    public bool ActiveTabCanRestoreTurn => GetLatestEditTurnForTab(_activeTab)?.CanRestoreTurn ?? false;
    public string ActiveTabDiffBannerText => _activeTab != null && _activeTab.PendingHunkCount > 0
        ? $"✦ Qwen marked {_activeTab.PendingHunkCount} edit(s) — Hover any highlighted green/red line for individual edit controls"
        : "";

    public string PreviewTargetTitle => GetPreviewTargetTab()?.Title ?? "No HTML tab";
    public string ChatTargetLabel => _activeTab != null ? $"Active: {_activeTab.Title}" : "No active file";

    // Commands
    public IRelayCommand NewFileCommand { get; }
    public IAsyncRelayCommand OpenFileCommand { get; }
    public IAsyncRelayCommand OpenFolderCommand { get; }
    public IAsyncRelayCommand SaveFileCommand { get; }
    public IAsyncRelayCommand SaveFileAsCommand { get; }
    public IRelayCommand<DocumentTab?> CloseTabCommand { get; }
    public IRelayCommand<DocumentTab?> SelectTabCommand { get; }
    public IRelayCommand<ExplorerFileItem?> OpenExplorerFileCommand { get; }
    public IRelayCommand FormatDocumentCommand { get; }
    public IRelayCommand ToggleExplorerCommand { get; }
    public IRelayCommand TogglePreviewPaneCommand { get; }
    public IRelayCommand ToggleChatPaneCommand { get; }
    public IRelayCommand SetLivePreviewModeCommand { get; }
    public IRelayCommand SetSavedPreviewModeCommand { get; }
    public IAsyncRelayCommand RefreshPreviewCommand { get; }
    public IRelayCommand DetachPreviewCommand { get; }
    public IRelayCommand ToggleAgentEditModeCommand { get; }
    public IAsyncRelayCommand SendPromptCommand { get; }
    public IRelayCommand StopGenerationCommand { get; }
    public IAsyncRelayCommand ClearChatCommand { get; }
    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenPreferencesCommand { get; }
    public IRelayCommand<ChatTurn?> AcceptTurnCommand { get; }
    public IRelayCommand<ChatTurn?> CancelTurnCommand { get; }
    public IRelayCommand<ChatTurn?> RestoreTurnCommand { get; }
    public IRelayCommand AcceptActiveTabTurnCommand { get; }
    public IRelayCommand CancelActiveTabTurnCommand { get; }
    public IRelayCommand RestoreActiveTabTurnCommand { get; }
    public IRelayCommand<string?> SetActiveLanguageCommand { get; }

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        _isLivePreviewMode = _settings.DefaultLivePreview;
        _isPreviewVisible = _settings.ShowPreviewOnStartup;
        _isChatVisible = _settings.ShowAgentOnStartup;

        NewFileCommand = new RelayCommand(CreateNewUntitledTab);
        OpenFileCommand = new AsyncRelayCommand(OpenFilesInteractiveAsync);
        OpenFolderCommand = new AsyncRelayCommand(OpenFolderInteractiveAsync);
        SaveFileCommand = new AsyncRelayCommand(SaveActiveFileAsync);
        SaveFileAsCommand = new AsyncRelayCommand(SaveActiveFileAsAsync);
        CloseTabCommand = new RelayCommand<DocumentTab?>(CloseTab);
        SelectTabCommand = new RelayCommand<DocumentTab?>(tab => { if (tab != null) ActiveTab = tab; });
        OpenExplorerFileCommand = new RelayCommand<ExplorerFileItem?>(item => { if (item != null) OpenFileFromPath(item.FullPath); });
        FormatDocumentCommand = new RelayCommand(() => _ = SendToEditorAsync(new { type = "formatDocument" }));
        ToggleExplorerCommand = new RelayCommand(() => IsExplorerVisible = !IsExplorerVisible);
        TogglePreviewPaneCommand = new RelayCommand(TogglePreviewPane);
        ToggleChatPaneCommand = new RelayCommand(() => IsChatVisible = !IsChatVisible);
        SetLivePreviewModeCommand = new RelayCommand(() => IsLivePreviewMode = true);
        SetSavedPreviewModeCommand = new RelayCommand(() => IsLivePreviewMode = false);
        RefreshPreviewCommand = new AsyncRelayCommand(RefreshPreviewImmediateAsync);
        DetachPreviewCommand = new RelayCommand(DetachPreviewToWindow);
        ToggleAgentEditModeCommand = new RelayCommand(() => IsAgentEditMode = !IsAgentEditMode);
        SendPromptCommand = new AsyncRelayCommand(SendPromptAsync);
        StopGenerationCommand = new RelayCommand(() => _generationCts?.Cancel());
        ClearChatCommand = new AsyncRelayCommand(ClearChatInteractiveAsync);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        OpenPreferencesCommand = new AsyncRelayCommand(OpenPreferencesAsync);
        AcceptTurnCommand = new RelayCommand<ChatTurn?>(AcceptTurn);
        CancelTurnCommand = new RelayCommand<ChatTurn?>(CancelTurn);
        RestoreTurnCommand = new RelayCommand<ChatTurn?>(RestoreTurn);
        AcceptActiveTabTurnCommand = new RelayCommand(AcceptActiveTabTurn);
        CancelActiveTabTurnCommand = new RelayCommand(CancelActiveTabTurn);
        RestoreActiveTabTurnCommand = new RelayCommand(RestoreActiveTabTurn);
        SetActiveLanguageCommand = new RelayCommand<string?>(SetActiveLanguage);

        CreateStarterWelcomeTab();
        _ = CheckOllamaStatusAsync();
    }

    public void AttachEditorSurface(IWebSurface surface)
    {
        EditorSurface = surface;
        EditorSurface.WebMessageReceived += (_, msg) =>
            Dispatcher.UIThread.Post(() => HandleEditorWebMessage(msg));

        string monacoRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot", "monaco");
        EditorSurface.MapLocalFolder("mink.editor", monacoRoot);
        _ = EditorSurface.NavigateAsync("https://mink.editor/index.html");
    }

    public void AttachDockedPreviewSurface(IWebSurface surface)
    {
        DockedPreviewSurface = surface;
        DockedPreviewSurface.SurfaceReady += (_, _) =>
            Dispatcher.UIThread.Post(() => _ = RefreshPreviewImmediateAsync());
    }

    public void AttachDetachedPreviewSurface(IWebSurface? surface)
    {
        DetachedPreviewSurface = surface;
        if (DetachedPreviewSurface != null)
        {
            DetachedPreviewSurface.SurfaceReady += (_, _) =>
                Dispatcher.UIThread.Post(() => _ = RefreshPreviewImmediateAsync());
        }
    }

    public void OnDetachedWindowClosed(bool dockBackToMain)
    {
        DetachedPreviewSurface = null;
        IsPreviewDetached = false;
        IsPreviewVisible = dockBackToMain;
        if (dockBackToMain)
        {
            _ = RefreshPreviewImmediateAsync();
        }
    }

    private void TogglePreviewPane()
    {
        if (IsPreviewDetached)
        {
            RequestCloseDetachedPreviewWindow?.Invoke();
            return;
        }
        IsPreviewVisible = !IsPreviewVisible;
    }

    private void DetachPreviewToWindow()
    {
        if (IsPreviewDetached) return;
        IsPreviewVisible = true;
        IsPreviewDetached = true;
        RequestOpenDetachedPreviewWindow?.Invoke();
    }

    private void CreateStarterWelcomeTab()
    {
        const string starterHtml = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>minK Workspace</title>
  <style>
    :root {
      --bg: #121417;
      --card: #1c2026;
      --accent: #38bdf8;
      --text: #e2e8f0;
      --muted: #94a3b8;
    }
    body {
      margin: 0;
      padding: 40px 28px;
      font-family: system-ui, -apple-system, 'Segoe UI', sans-serif;
      background: var(--bg);
      color: var(--text);
      line-height: 1.6;
    }
    .card {
      max-width: 620px;
      margin: 0 auto;
      background: var(--card);
      border: 1px solid #2d3540;
      border-radius: 10px;
      padding: 28px 32px;
      box-shadow: 0 12px 32px rgba(0, 0, 0, 0.35);
    }
    h1 {
      margin-top: 10px;
      margin-bottom: 10px;
      font-size: 1.65rem;
      color: var(--accent);
    }
    p { color: var(--muted); margin-bottom: 0; }
    .pill {
      display: inline-block;
      background: rgba(56, 189, 248, 0.14);
      color: var(--accent);
      border: 1px solid rgba(56, 189, 248, 0.3);
      border-radius: 999px;
      padding: 4px 12px;
      font-size: 0.82rem;
      font-weight: 600;
    }
  </style>
</head>
<body>
  <main class="card">
    <span class="pill">100% Local &amp; Air-Gapped</span>
    <h1>minK Ready</h1>
    <p>Press <strong>Ctrl+O</strong> to open an HTML file, edit directly with auto-closing tags, or ask your local Qwen model in the right panel to make reversible inline edits.</p>
  </main>
</body>
</html>
""";

        var tab = new DocumentTab
        {
            Title = "index.html",
            Language = "html",
            SavedContent = starterHtml,
            Content = starterHtml,
            IsDirty = false
        };

        Tabs.Add(tab);
        ActiveTab = tab;
    }

    public void CreateNewUntitledTab()
    {
        var tab = new DocumentTab
        {
            Title = $"untitled-{_untitledCounter++}.html",
            Language = "html",
            SavedContent = "",
            Content = "",
            IsDirty = false
        };
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    public void OpenStartupFiles(string[]? args)
    {
        string[] resolvedPaths = SingleInstanceService.ResolveCliPaths(args, Environment.CurrentDirectory);
        if (resolvedPaths.Length == 0) return;

        var starter = (Tabs.Count == 1 && Tabs[0].FilePath == null && !Tabs[0].IsDirty) ? Tabs[0] : null;
        bool openedFile = false;

        foreach (string fullPath in resolvedPaths)
        {
            if (Directory.Exists(fullPath))
            {
                LoadFolder(fullPath);
                string indexCandidate = Path.Combine(fullPath, "index.html");
                if (!openedFile && File.Exists(indexCandidate))
                {
                    OpenFileFromPath(indexCandidate);
                    openedFile = true;
                }
            }
            else if (File.Exists(fullPath))
            {
                OpenFileFromPath(fullPath);
                openedFile = true;
            }
            else
            {
                string fileName = Path.GetFileName(fullPath);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    OpenFileFromPath(fullPath, allowCreateNew: true);
                    openedFile = true;
                }
            }
        }

        if (openedFile && starter != null && Tabs.Count > 1 && Tabs.Contains(starter))
        {
            CloseTab(starter);
        }
    }

    private async Task OpenFilesInteractiveAsync()
    {
        if (PickOpenFilesDialog == null) return;
        string[]? paths = await PickOpenFilesDialog();
        if (paths == null || paths.Length == 0) return;

        if (Tabs.Count == 1 && Tabs[0].FilePath == null && !Tabs[0].IsDirty && Tabs[0].Title == "index.html")
        {
            var starter = Tabs[0];
            foreach (string p in paths)
            {
                OpenFileFromPath(p);
            }
            if (Tabs.Count > 1)
            {
                CloseTab(starter);
            }
            return;
        }

        foreach (string path in paths)
        {
            OpenFileFromPath(path);
        }
    }

    public void OpenFileFromPath(string fullPath, bool allowCreateNew = false)
    {
        bool exists = File.Exists(fullPath);
        if (!exists && (!allowCreateNew || Directory.Exists(fullPath))) return;

        var existing = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ActiveTab = existing;
            return;
        }

        try
        {
            var tab = exists
                ? DocumentTab.FromDisk(fullPath)
                : DocumentTab.ForNewPath(fullPath);
            Tabs.Add(tab);
            ActiveTab = tab;
        }
        catch
        {
            // Ignore unreadable binary/locked file
        }
    }

    private async Task OpenFolderInteractiveAsync()
    {
        if (PickOpenFolderDialog == null) return;
        string? folder = await PickOpenFolderDialog();
        if (!string.IsNullOrEmpty(folder))
        {
            LoadFolder(folder);
        }
    }

    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        FolderFiles.Clear();
        string trimmedFolder = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folderLabel = Path.GetFileName(trimmedFolder);
        OpenedFolderName = (string.IsNullOrEmpty(folderLabel) ? folderPath : folderLabel).ToUpperInvariant();

        var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".html", ".htm", ".xhtml", ".css", ".scss", ".less",
            ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx",
            ".json", ".jsonc", ".webmanifest", ".py", ".pyw",
            ".md", ".markdown", ".svg", ".xml", ".yaml", ".yml",
            ".txt", ".ini", ".cfg", ".conf", ".toml", ".env",
            ".sh", ".ps1", ".bat", ".cmd", ".sql", ".cs"
        };

        try
        {
            var files = Directory.GetFiles(folderPath)
                .Where(f => allowedExts.Contains(Path.GetExtension(f)) ||
                            string.Equals(Path.GetFileName(f), "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Path.GetFileName(f), "LICENSE", StringComparison.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName);

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string icon = ext switch
                {
                    ".html" or ".htm" or ".xhtml" => "🌐",
                    ".css" or ".scss" or ".less" => "🎨",
                    ".js" or ".mjs" or ".cjs" or ".jsx" or ".ts" or ".tsx" => "⚡",
                    ".py" or ".pyw" => "🐍",
                    ".json" or ".jsonc" or ".webmanifest" => "{ }",
                    ".md" or ".markdown" => "📝",
                    _ => "📄"
                };

                FolderFiles.Add(new ExplorerFileItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Icon = icon
                });
            }

            IsExplorerVisible = true;
        }
        catch
        {
            // Ignore directory enumeration permission errors
        }
    }

    private async Task SaveActiveFileAsync()
    {
        if (_activeTab == null) return;

        if (string.IsNullOrEmpty(_activeTab.FilePath))
        {
            await SaveActiveFileAsAsync();
            return;
        }

        await WriteTabToDiskAsync(_activeTab, _activeTab.FilePath);
    }

    private async Task SaveActiveFileAsAsync()
    {
        if (_activeTab == null || PickSaveFileDialog == null) return;

        string? chosenPath = await PickSaveFileDialog(_activeTab.Title);
        if (string.IsNullOrEmpty(chosenPath)) return;

        string prevLang = _activeTab.Language;
        await WriteTabToDiskAsync(_activeTab, chosenPath);

        if (_activeTab.Language != prevLang)
        {
            await SendToEditorAsync(new
            {
                type = "setLanguage",
                tabId = _activeTab.Id,
                language = _activeTab.Language
            });
        }
    }

    private async Task WriteTabToDiskAsync(DocumentTab tab, string targetPath)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        string contentToWrite = tab.Content;
        string? parentDir = Path.GetDirectoryName(targetPath);
        await Task.Run(() =>
        {
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            File.WriteAllText(targetPath, contentToWrite, utf8NoBom);
        });

        tab.MarkSaved(targetPath);
        _saveRevision++;

        if (tab.Language is "html" or "xml")
        {
            _lastHtmlTab = tab;
        }

        NotifyActiveTabComputedProperties();
        await RefreshPreviewImmediateAsync();
    }

    private void CloseTab(DocumentTab? tab)
    {
        if (tab == null) return;

        int idx = Tabs.IndexOf(tab);
        if (idx < 0) return;

        _ = SendToEditorAsync(new { type = "closeTab", tabId = tab.Id });
        Tabs.RemoveAt(idx);

        if (_lastHtmlTab == tab)
        {
            _lastHtmlTab = Tabs.FirstOrDefault(t => t.Language is "html" or "xml");
        }

        if (Tabs.Count == 0)
        {
            CreateNewUntitledTab();
        }
        else if (ActiveTab == tab)
        {
            int nextIdx = Math.Clamp(idx - 1, 0, Tabs.Count - 1);
            ActiveTab = Tabs[nextIdx];
        }
    }

    private void SetActiveLanguage(string? language)
    {
        if (_activeTab == null || string.IsNullOrWhiteSpace(language)) return;
        _activeTab.Language = language;
        if (language is "html" or "xml")
        {
            _lastHtmlTab = _activeTab;
        }
        NotifyActiveTabComputedProperties();
        _ = SendToEditorAsync(new
        {
            type = "setLanguage",
            tabId = _activeTab.Id,
            language = _activeTab.Language
        });
        _ = RefreshPreviewImmediateAsync();
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTab.IsDirty) or
            nameof(DocumentTab.Title) or
            nameof(DocumentTab.PendingHunkCount) or
            nameof(DocumentTab.Language))
        {
            NotifyActiveTabComputedProperties();
        }
    }

    private void NotifyActiveTabComputedProperties()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CursorStatusText));
        OnPropertyChanged(nameof(ActiveLanguageDisplay));
        OnPropertyChanged(nameof(ActiveTabHasPendingDiffs));
        OnPropertyChanged(nameof(ActiveTabCanRestoreTurn));
        OnPropertyChanged(nameof(ActiveTabDiffBannerText));
        OnPropertyChanged(nameof(PreviewTargetTitle));
        OnPropertyChanged(nameof(ChatTargetLabel));
    }

    private Task PushEditorSettingsAsync()
    {
        return SendToEditorAsync(new
        {
            type = "updateSettings",
            fontSize = _settings.FontSize,
            tabSize = _settings.TabSize,
            wordWrap = _settings.WordWrap,
            showMinimap = _settings.ShowMinimap,
            autoClose = _settings.AutoCloseTagsAndBrackets
        });
    }

    private Task SyncActiveTabToMonacoAsync(DocumentTab tab)
    {
        return SendToEditorAsync(new
        {
            type = "switchTab",
            tabId = tab.Id,
            content = tab.Content,
            language = tab.Language
        });
    }

    private async Task SendToEditorAsync(object messageObject)
    {
        var surface = EditorSurface;
        if (surface == null) return;
        string json = await Task.Run(() => JsonSerializer.Serialize(messageObject));
        await surface.PostJsonMessageAsync(json);
    }

    private void HandleEditorWebMessage(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;
            string? type = typeProp.GetString();

            switch (type)
            {
                case "ready":
                    _ = PushEditorSettingsAsync();
                    if (_activeTab != null)
                    {
                        _ = SyncActiveTabToMonacoAsync(_activeTab);
                    }
                    break;

                case "contentChanged":
                {
                    string? tabId = root.GetProperty("tabId").GetString();
                    string content = root.GetProperty("content").GetString() ?? "";
                    var tab = Tabs.FirstOrDefault(t => t.Id == tabId);
                    if (tab != null)
                    {
                        tab.Content = content;
                        var latestTurn = ChatTurns.LastOrDefault(ct => ct.TargetTabId == tab.Id && ct.HasEdits && !ct.IsReverted);
                        if (latestTurn != null && !latestTurn.IsStreaming)
                        {
                            latestTurn.IsPartiallyModified =
                                DiffAndPatchService.NormalizeLineEndings(content) != latestTurn.SnapshotAfter;
                            if (tab == _activeTab)
                            {
                                OnPropertyChanged(nameof(ActiveTabCanRestoreTurn));
                            }
                        }
                        if (_isLivePreviewMode)
                        {
                            ScheduleDebouncedLivePreview();
                        }
                    }
                    break;
                }

                case "cursorChanged":
                {
                    string? tabId = root.GetProperty("tabId").GetString();
                    var tab = Tabs.FirstOrDefault(t => t.Id == tabId);
                    if (tab != null)
                    {
                        tab.CursorLine = root.GetProperty("line").GetInt32();
                        tab.CursorColumn = root.GetProperty("column").GetInt32();
                        if (tab == _activeTab)
                        {
                            OnPropertyChanged(nameof(CursorStatusText));
                        }
                    }
                    break;
                }

                case "shortcut":
                {
                    string? action = root.GetProperty("action").GetString();
                    switch (action)
                    {
                        case "save": _ = SaveActiveFileAsync(); break;
                        case "saveAs": _ = SaveActiveFileAsAsync(); break;
                        case "openFile": _ = OpenFilesInteractiveAsync(); break;
                        case "newFile": CreateNewUntitledTab(); break;
                        case "toggleExplorer": IsExplorerVisible = !IsExplorerVisible; break;
                        case "openPreferences": _ = OpenPreferencesAsync(); break;
                    }
                    break;
                }

                case "hunkResolved":
                {
                    string? tabId = root.GetProperty("tabId").GetString();
                    string? turnId = root.GetProperty("turnId").GetString();
                    string? action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() : null;
                    int pending = root.GetProperty("pendingCount").GetInt32();
                    string content = root.GetProperty("content").GetString() ?? "";

                    var tab = Tabs.FirstOrDefault(t => t.Id == tabId);
                    if (tab != null)
                    {
                        tab.Content = content;
                        tab.PendingHunkCount = pending;
                        if (pending == 0) tab.ActiveTurnId = null;
                    }

                    var turn = ChatTurns.FirstOrDefault(ct => ct.Id == turnId);
                    if (turn != null)
                    {
                        turn.PendingHunkCount = pending;
                        if (action == "reverted")
                        {
                            turn.RevertedHunkCount++;
                        }
                        turn.IsPartiallyModified =
                            DiffAndPatchService.NormalizeLineEndings(content) != turn.SnapshotAfter;
                    }

                    NotifyActiveTabComputedProperties();
                    if (_isLivePreviewMode)
                    {
                        _ = RefreshPreviewImmediateAsync();
                    }
                    break;
                }

                case "turnResolved":
                {
                    string? tabId = root.GetProperty("tabId").GetString();
                    string? turnId = root.GetProperty("turnId").GetString();
                    string? action = root.GetProperty("action").GetString();
                    int pending = root.GetProperty("pendingCount").GetInt32();
                    string content = root.GetProperty("content").GetString() ?? "";

                    var tab = Tabs.FirstOrDefault(t => t.Id == tabId);
                    if (tab != null)
                    {
                        tab.Content = content;
                        tab.PendingHunkCount = pending;
                        if (pending == 0) tab.ActiveTurnId = null;
                    }

                    var turn = ChatTurns.FirstOrDefault(ct => ct.Id == turnId);
                    if (turn != null)
                    {
                        turn.PendingHunkCount = pending;
                        if (action == "revertedTurn")
                        {
                            turn.IsReverted = true;
                            turn.RevertedHunkCount = 0;
                            turn.IsPartiallyModified = false;
                        }
                        else
                        {
                            turn.IsPartiallyModified =
                                DiffAndPatchService.NormalizeLineEndings(content) != turn.SnapshotAfter;
                        }
                    }

                    NotifyActiveTabComputedProperties();
                    if (_isLivePreviewMode)
                    {
                        _ = RefreshPreviewImmediateAsync();
                    }
                    break;
                }
            }
        }
        catch
        {
            // Ignore malformed interop payload
        }
    }

    // --- Live & Saved Chromium Preview Engine ---

    private DocumentTab? GetPreviewTargetTab()
    {
        if (_activeTab != null && _activeTab.Language is "html" or "xml")
        {
            return _activeTab;
        }
        if (_lastHtmlTab != null && Tabs.Contains(_lastHtmlTab))
        {
            return _lastHtmlTab;
        }
        return Tabs.FirstOrDefault(t => t.Language is "html" or "xml");
    }

    private IWebSurface? GetActivePreviewSurface()
    {
        if (!_isPreviewVisible) return null;
        return _isPreviewDetached ? DetachedPreviewSurface : DockedPreviewSurface;
    }

    private void ScheduleDebouncedLivePreview()
    {
        _previewDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewDebounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, cts.Token).ConfigureAwait(false);
                if (!cts.Token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(RefreshPreviewImmediateAsync);
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    public async Task RefreshPreviewImmediateAsync()
    {
        OnPropertyChanged(nameof(PreviewTargetTitle));

        var surface = GetActivePreviewSurface();
        if (surface == null) return;

        var htmlTab = GetPreviewTargetTab();
        if (htmlTab == null)
        {
            await surface.NavigateToStringAsync(BuildStatusCardHtml(
                "No HTML Document Open",
                "Open or switch to an .html file tab to preview it in Chromium."));
            return;
        }

        if (_isLivePreviewMode)
        {
            string rawHtml = htmlTab.Content;
            string? filePath = htmlTab.FilePath;
            var siblingSnapshots = Tabs
                .Where(s => s != htmlTab && !string.IsNullOrWhiteSpace(s.Title) && s.Language is "css" or "javascript")
                .Select(s => (s.Title, s.Language, s.Content))
                .ToList();

            string? assetDir = !string.IsNullOrEmpty(filePath) ? Path.GetDirectoryName(filePath) : null;
            bool hasAssetDir = !string.IsNullOrEmpty(assetDir) && Directory.Exists(assetDir);
            if (hasAssetDir)
            {
                surface.MapLocalFolder("mink.assets", assetDir!);
            }

            string liveHtml = await Task.Run(() =>
                BuildLivePreviewHtmlOffThread(rawHtml, siblingSnapshots, hasAssetDir));

            await surface.NavigateToStringAsync(liveHtml);
        }
        else
        {
            if (!string.IsNullOrEmpty(htmlTab.FilePath) && File.Exists(htmlTab.FilePath))
            {
                string dir = Path.GetDirectoryName(htmlTab.FilePath)!;
                string fileName = Path.GetFileName(htmlTab.FilePath);
                surface.MapLocalFolder("mink.saved", dir);
                await surface.NavigateAsync($"https://mink.saved/{Uri.EscapeDataString(fileName)}?rev={_saveRevision}");
            }
            else if (!string.IsNullOrEmpty(htmlTab.SavedContent))
            {
                await surface.NavigateToStringAsync(htmlTab.SavedContent);
            }
            else
            {
                await surface.NavigateToStringAsync(BuildStatusCardHtml(
                    "No Saved State Yet",
                    "This document has not been saved to disk. Press Ctrl+S to save, or toggle to ⚡ Live Edits."));
            }
        }
    }

    private static string BuildLivePreviewHtmlOffThread(
        string html,
        List<(string Title, string Language, string Content)> siblings,
        bool injectAssetBaseTag)
    {
        foreach (var sibling in siblings)
        {
            if (sibling.Language == "css")
            {
                string pattern = $@"<link\b[^>]*href\s*=\s*[""'](?:\./)?{Regex.Escape(sibling.Title)}[""'][^>]*/?>";
                if (Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase))
                {
                    html = Regex.Replace(
                        html,
                        pattern,
                        _ => $"<style>/* Live Tab: {sibling.Title} */\n{sibling.Content}\n</style>",
                        RegexOptions.IgnoreCase);
                }
            }
            else if (sibling.Language == "javascript")
            {
                string pattern = $@"<script\b[^>]*src\s*=\s*[""'](?:\./)?{Regex.Escape(sibling.Title)}[""'][^>]*>\s*</script>";
                if (Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase))
                {
                    html = Regex.Replace(
                        html,
                        pattern,
                        _ => $"<script>/* Live Tab: {sibling.Title} */\n{sibling.Content}\n</script>",
                        RegexOptions.IgnoreCase);
                }
            }
        }

        if (injectAssetBaseTag && !html.Contains("<base ", StringComparison.OrdinalIgnoreCase))
        {
            const string baseTag = "<base href=\"https://mink.assets/\" />";
            int headIdx = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
            if (headIdx >= 0)
            {
                html = html.Insert(headIdx + "<head>".Length, "\n  " + baseTag);
            }
            else
            {
                html = baseTag + "\n" + html;
            }
        }

        return html;
    }

    private static string BuildStatusCardHtml(string title, string subtitle)
    {
        return $$"""
<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8" />
  <style>
    body {
      margin: 0;
      height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #1a1a1c;
      color: #9da5b4;
      font-family: 'Segoe UI', system-ui, sans-serif;
      text-align: center;
      padding: 24px;
    }
    .box {
      max-width: 380px;
      background: #252526;
      border: 1px solid #38383d;
      border-radius: 8px;
      padding: 22px 26px;
    }
    h3 { margin: 0 0 8px 0; color: #e2e8f0; font-size: 15px; }
    p { margin: 0; font-size: 12.5px; line-height: 1.5; }
  </style>
</head>
<body>
  <div class="box">
    <h3>{{title}}</h3>
    <p>{{subtitle}}</p>
  </div>
</body>
</html>
""";
    }

    // --- Non-Blocking Local / LAN Ollama Agent & Reversible Turn Pipeline ---

    public async Task CheckOllamaStatusAsync()
    {
        string url = _settings.Ollama.BaseUrl;
        bool online = await Task.Run(() => _ollamaClient.PingAsync(url));
        IsOllamaOnline = online;
    }

    private async Task OpenSettingsAsync()
    {
        if (ShowSettingsDialog == null) return;

        AppSettings? updated = await ShowSettingsDialog(_settings, _ollamaClient);
        if (updated == null) return;

        _settings = updated;
        await Task.Run(() => _settingsService.Save(_settings));

        OnPropertyChanged(nameof(OllamaHeaderSummary));
        OnPropertyChanged(nameof(OllamaEndpointShort));
        OnPropertyChanged(nameof(OllamaStatusBarText));

        await PushEditorSettingsAsync();
        await CheckOllamaStatusAsync();
    }

    private async Task OpenPreferencesAsync()
    {
        if (ShowPreferencesDialog == null) return;

        AppSettings? updated = await ShowPreferencesDialog(_settings);
        if (updated == null) return;

        _settings = updated;
        await Task.Run(() => _settingsService.Save(_settings));
        await PushEditorSettingsAsync();
    }

    private async Task ClearChatInteractiveAsync()
    {
        if (ChatTurns.Count == 0) return;

        if (_settings.ConfirmClearChat && ConfirmClearChatDialog != null)
        {
            var (confirmed, dontAskAgain) = await ConfirmClearChatDialog();
            if (!confirmed) return;

            if (dontAskAgain)
            {
                _settings.ConfirmClearChat = false;
                await Task.Run(() => _settingsService.Save(_settings));
            }
        }

        foreach (var tab in Tabs)
        {
            if (tab.PendingHunkCount > 0)
            {
                tab.PendingHunkCount = 0;
                tab.ActiveTurnId = null;
                _ = SendToEditorAsync(new
                {
                    type = "acceptWholeTurn",
                    tabId = tab.Id,
                    turnId = ""
                });
            }
        }

        ChatTurns.Clear();
        NotifyActiveTabComputedProperties();
    }

    private sealed record AgentTurnBackgroundResult(
        bool ReachedServer,
        bool Canceled,
        string? ErrorMessage,
        string FinalDisplayMessage,
        bool HasCodeEdits,
        string ProposedContent,
        List<DiffHunk> Hunks,
        string? PreSerializedEditorPayload);

    private async Task SendPromptAsync()
    {
        string promptText = ChatInput.Trim();
        if (string.IsNullOrEmpty(promptText) || IsGenerating) return;

        ChatInput = "";
        var targetTab = _activeTab;
        string? targetTabId = targetTab?.Id;
        string targetTabTitle = targetTab?.Title ?? "";
        string targetLanguage = targetTab?.Language ?? "html";
        bool applyEdits = _isAgentEditMode && targetTab != null;
        string rawBefore = targetTab?.Content ?? "";

        var turn = new ChatTurn
        {
            UserPrompt = promptText,
            TargetTabId = targetTabId,
            TargetTabTitle = targetTabTitle,
            AssistantMessage = "Thinking...",
            IsStreaming = true
        };

        ChatTurns.Add(turn);
        ScrollChatToBottom?.Invoke();

        IsGenerating = true;
        var cts = new CancellationTokenSource();
        _generationCts = cts;
        var ct = cts.Token;

        string baseUrl = _settings.Ollama.BaseUrl;
        string modelName = _settings.Ollama.ModelName;
        double temperature = _settings.Ollama.Temperature;
        int contextWindow = _settings.Ollama.ContextWindow;
        string turnId = turn.Id;

        // Offload 100% of HTTP streaming, JSON line parsing, regex patching, and DiffPlex diffing
        // onto a background worker thread so the UI thread never blocks for even a single frame.
        AgentTurnBackgroundResult bgResult = await Task.Run(async () =>
        {
            string snapshotBefore = DiffAndPatchService.NormalizeLineEndings(rawBefore);
            string systemInstruction = BuildSystemPrompt(targetTabTitle, targetLanguage, applyEdits);
            string fullPrompt = BuildContextualUserPrompt(targetTabTitle, targetLanguage, snapshotBefore, promptText, targetTab != null);

            var rawBuilder = new StringBuilder(4096);
            string latestUiMessage = "Thinking...";
            int uiPostPending = 0;
            bool reachedServer = false;
            int receivedFirstToken = 0;

            void PostCoalescedStatus(string statusText, bool markOnline = false)
            {
                Volatile.Write(ref latestUiMessage, statusText);
                if (Interlocked.CompareExchange(ref uiPostPending, 1, 0) == 0)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Interlocked.Exchange(ref uiPostPending, 0);
                        if (markOnline && !IsOllamaOnline)
                        {
                            IsOllamaOnline = true;
                        }
                        if (turn.IsStreaming)
                        {
                            turn.AssistantMessage = Volatile.Read(ref latestUiMessage);
                            ScrollChatToBottom?.Invoke();
                        }
                    }, DispatcherPriority.Background);
                }
            }

            // Heartbeat timer while Ollama evaluates the prompt or loads model weights into VRAM
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var elapsedSw = Stopwatch.StartNew();
            var heartbeatTask = Task.Run(async () =>
            {
                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
                    while (await timer.WaitForNextTickAsync(heartbeatCts.Token).ConfigureAwait(false))
                    {
                        if (Volatile.Read(ref receivedFirstToken) != 0) break;
                        int secs = Math.Max(1, (int)elapsedSw.Elapsed.TotalSeconds);
                        PostCoalescedStatus($"Thinking... ({secs}s)");
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            var uiThrottleSw = Stopwatch.StartNew();
            int patchStartOffset = -1;
            string summaryPrefix = "";
            int streamedNewlines = 0;

            try
            {
                await foreach (string token in _ollamaClient.StreamGenerateAsync(
                    baseUrl,
                    modelName,
                    fullPrompt,
                    systemInstruction,
                    temperature,
                    contextWindow,
                    ct).ConfigureAwait(false))
                {
                    if (Interlocked.Exchange(ref receivedFirstToken, 1) == 0)
                    {
                        heartbeatCts.Cancel();
                    }

                    reachedServer = true;
                    rawBuilder.Append(token);

                    for (int i = 0; i < token.Length; i++)
                    {
                        if (token[i] == '\n') streamedNewlines++;
                    }

                    // Throttle UI updates to ~11 Hz max so Avalonia layout stays completely idle
                    if (uiThrottleSw.ElapsedMilliseconds >= 90)
                    {
                        uiThrottleSw.Restart();
                        string snapshot = rawBuilder.ToString();

                        if (applyEdits)
                        {
                            if (patchStartOffset < 0)
                            {
                                int srIdx = snapshot.IndexOf("<<<<<<< SEARCH", StringComparison.Ordinal);
                                int fenceIdx = snapshot.IndexOf("```", StringComparison.Ordinal);
                                int detectedIdx = (srIdx >= 0 && fenceIdx >= 0)
                                    ? Math.Min(srIdx, fenceIdx)
                                    : Math.Max(srIdx, fenceIdx);

                                if (detectedIdx >= 0)
                                {
                                    patchStartOffset = detectedIdx;
                                    summaryPrefix = snapshot[..detectedIdx].Trim();
                                }
                            }

                            if (patchStartOffset >= 0)
                            {
                                string progressMsg = string.IsNullOrEmpty(summaryPrefix)
                                    ? $"Generating inline edits... ({Math.Max(1, streamedNewlines)} lines)"
                                    : $"{summaryPrefix}\n\n[Generating inline edits... ({Math.Max(1, streamedNewlines)} lines)]";
                                PostCoalescedStatus(progressMsg, markOnline: true);
                            }
                            else
                            {
                                PostCoalescedStatus(snapshot, markOnline: true);
                            }
                        }
                        else
                        {
                            PostCoalescedStatus(snapshot, markOnline: true);
                        }
                    }
                }

                heartbeatCts.Cancel();
                string finalRaw = rawBuilder.ToString();

                if (applyEdits &&
                    targetTabId != null &&
                    _diffService.TryExtractProposedContent(
                        snapshotBefore,
                        finalRaw,
                        editModeEnabled: true,
                        out string proposedContent,
                        out string cleanExplanation))
                {
                    var hunks = _diffService.ComputeHunks(turnId, snapshotBefore, proposedContent);
                    if (hunks.Count > 0)
                    {
                        string editorJson = JsonSerializer.Serialize(new
                        {
                            type = "applyAgentTurn",
                            tabId = targetTabId,
                            turnId = turnId,
                            snapshotBefore = snapshotBefore,
                            newContent = proposedContent,
                            hunks = hunks
                        });

                        return new AgentTurnBackgroundResult(
                            ReachedServer: true,
                            Canceled: false,
                            ErrorMessage: null,
                            FinalDisplayMessage: cleanExplanation,
                            HasCodeEdits: true,
                            ProposedContent: proposedContent,
                            Hunks: hunks,
                            PreSerializedEditorPayload: editorJson);
                    }

                    return new AgentTurnBackgroundResult(
                        ReachedServer: true,
                        Canceled: false,
                        ErrorMessage: null,
                        FinalDisplayMessage: cleanExplanation,
                        HasCodeEdits: false,
                        ProposedContent: snapshotBefore,
                        Hunks: new List<DiffHunk>(),
                        PreSerializedEditorPayload: null);
                }

                string display = string.IsNullOrWhiteSpace(finalRaw)
                    ? "(No response returned from model.)"
                    : finalRaw.Trim();

                return new AgentTurnBackgroundResult(
                    ReachedServer: reachedServer,
                    Canceled: false,
                    ErrorMessage: null,
                    FinalDisplayMessage: display,
                    HasCodeEdits: false,
                    ProposedContent: snapshotBefore,
                    Hunks: new List<DiffHunk>(),
                    PreSerializedEditorPayload: null);
            }
            catch (OperationCanceledException)
            {
                heartbeatCts.Cancel();
                string partial = rawBuilder.Length > 0
                    ? rawBuilder.ToString().Trim() + "\n\n[Generation stopped]"
                    : "[Generation stopped]";

                return new AgentTurnBackgroundResult(
                    ReachedServer: reachedServer,
                    Canceled: true,
                    ErrorMessage: null,
                    FinalDisplayMessage: partial,
                    HasCodeEdits: false,
                    ProposedContent: snapshotBefore,
                    Hunks: new List<DiffHunk>(),
                    PreSerializedEditorPayload: null);
            }
            catch (Exception ex)
            {
                heartbeatCts.Cancel();
                return new AgentTurnBackgroundResult(
                    ReachedServer: false,
                    Canceled: false,
                    ErrorMessage: $"Connection error reaching {baseUrl} ({modelName}):\n{ex.Message}",
                    FinalDisplayMessage: "",
                    HasCodeEdits: false,
                    ProposedContent: snapshotBefore,
                    Hunks: new List<DiffHunk>(),
                    PreSerializedEditorPayload: null);
            }
            finally
            {
                try { await heartbeatTask.ConfigureAwait(false); } catch { }
            }
        });

        // Fast, non-blocking final state commit on UI thread
        turn.SnapshotBefore = DiffAndPatchService.NormalizeLineEndings(rawBefore);
        turn.IsStreaming = false;
        IsGenerating = false;
        cts.Dispose();
        if (_generationCts == cts) _generationCts = null;

        if (bgResult.ErrorMessage != null)
        {
            IsOllamaOnline = false;
            turn.AssistantMessage = bgResult.ErrorMessage;
        }
        else
        {
            if (bgResult.ReachedServer) IsOllamaOnline = true;
            turn.AssistantMessage = bgResult.FinalDisplayMessage;

            if (bgResult.HasCodeEdits && targetTab != null && bgResult.PreSerializedEditorPayload != null)
            {
                foreach (var prevTurn in ChatTurns)
                {
                    if (prevTurn != turn && prevTurn.TargetTabId == targetTab.Id && prevTurn.PendingHunkCount > 0)
                    {
                        prevTurn.PendingHunkCount = 0;
                    }
                }

                turn.SnapshotAfter = bgResult.ProposedContent;
                turn.Hunks = bgResult.Hunks;
                turn.TotalHunkCount = bgResult.Hunks.Count;
                turn.PendingHunkCount = bgResult.Hunks.Count;
                turn.HasEdits = true;

                targetTab.Content = bgResult.ProposedContent;
                targetTab.PendingHunkCount = bgResult.Hunks.Count;
                targetTab.ActiveTurnId = turn.Id;

                if (EditorSurface != null)
                {
                    await EditorSurface.PostJsonMessageAsync(bgResult.PreSerializedEditorPayload);
                }

                NotifyActiveTabComputedProperties();
                if (_isLivePreviewMode)
                {
                    await RefreshPreviewImmediateAsync();
                }
            }
        }

        ScrollChatToBottom?.Invoke();
    }

    private static string BuildSystemPrompt(string title, string language, bool applyEdits)
    {
        if (!applyEdits || string.IsNullOrEmpty(title))
        {
            return "You are a concise, helpful local coding assistant inside minK. Answer clearly and directly without fluff.";
        }

        return $$"""
You are a precise local code editing agent inside minK.
The user is editing `{{title}}` (language: `{{language}}`).

RULES WHEN MAKING CODE EDITS:
1. First write a brief 1-3 sentence summary explaining what you changed.
2. When editing existing code, output targeted SEARCH/REPLACE blocks using this EXACT syntax:
<<<<<<< SEARCH
exact lines from the current file
=======
updated lines
>>>>>>> REPLACE

3. Include 1-3 unchanged surrounding lines in SEARCH so each block matches uniquely.
4. Only if the active file is completely empty or requires a 100% full-file rewrite, output the entire file inside a single ```{{language}} code block.
5. If the user is only asking a question and does NOT want code modified, answer concisely without any SEARCH/REPLACE blocks.
""";
    }

    private static string BuildContextualUserPrompt(
        string title,
        string language,
        string snapshotBefore,
        string userRequest,
        bool hasActiveTab)
    {
        if (!hasActiveTab)
        {
            return userRequest;
        }

        return $$"""
Active Document (`{{title}}`, {{language}}):
```{{language}}
{{snapshotBefore}}
```

User Request:
{{userRequest}}
""";
    }

    private ChatTurn? GetLatestEditTurnForTab(DocumentTab? tab)
    {
        if (tab == null) return null;
        if (!string.IsNullOrEmpty(tab.ActiveTurnId))
        {
            var active = ChatTurns.LastOrDefault(t => t.Id == tab.ActiveTurnId);
            if (active != null) return active;
        }
        return ChatTurns.LastOrDefault(t => t.TargetTabId == tab.Id && t.HasEdits);
    }

    private void AcceptTurn(ChatTurn? turn)
    {
        if (turn == null || string.IsNullOrEmpty(turn.TargetTabId)) return;
        _ = SendToEditorAsync(new
        {
            type = "acceptWholeTurn",
            tabId = turn.TargetTabId,
            turnId = turn.Id
        });
    }

    private void CancelTurn(ChatTurn? turn)
    {
        if (turn == null || string.IsNullOrEmpty(turn.TargetTabId) || turn.IsReverted) return;
        _ = SendToEditorAsync(new
        {
            type = "cancelWholeTurn",
            tabId = turn.TargetTabId,
            turnId = turn.Id,
            snapshotBefore = turn.SnapshotBefore
        });
    }

    private void RestoreTurn(ChatTurn? turn)
    {
        if (turn == null || !turn.HasEdits || turn.IsStreaming || string.IsNullOrEmpty(turn.TargetTabId))
        {
            return;
        }

        var targetTab = Tabs.FirstOrDefault(t => t.Id == turn.TargetTabId);
        if (targetTab == null) return;

        if (_activeTab != targetTab)
        {
            ActiveTab = targetTab;
        }

        foreach (var other in ChatTurns)
        {
            if (other != turn && other.TargetTabId == targetTab.Id && other.PendingHunkCount > 0)
            {
                other.PendingHunkCount = 0;
            }
        }

        turn.IsReverted = false;
        turn.RevertedHunkCount = 0;
        turn.IsPartiallyModified = false;
        turn.TotalHunkCount = turn.Hunks.Count;
        turn.PendingHunkCount = turn.Hunks.Count;

        targetTab.Content = turn.SnapshotAfter;
        targetTab.PendingHunkCount = turn.Hunks.Count;
        targetTab.ActiveTurnId = turn.Id;

        _ = SendToEditorAsync(new
        {
            type = "applyAgentTurn",
            tabId = targetTab.Id,
            turnId = turn.Id,
            snapshotBefore = turn.SnapshotBefore,
            newContent = turn.SnapshotAfter,
            hunks = turn.Hunks
        });

        NotifyActiveTabComputedProperties();
        if (_isLivePreviewMode)
        {
            _ = RefreshPreviewImmediateAsync();
        }
    }

    private void AcceptActiveTabTurn()
    {
        if (_activeTab == null) return;
        var turn = ChatTurns.LastOrDefault(t => t.Id == _activeTab.ActiveTurnId)
                   ?? ChatTurns.LastOrDefault(t => t.TargetTabId == _activeTab.Id && t.HasEdits && !t.IsReverted);
        if (turn != null)
        {
            AcceptTurn(turn);
        }
        else
        {
            _ = SendToEditorAsync(new { type = "acceptWholeTurn", tabId = _activeTab.Id, turnId = "" });
        }
    }

    private void CancelActiveTabTurn()
    {
        if (_activeTab == null) return;
        var turn = ChatTurns.LastOrDefault(t => t.Id == _activeTab.ActiveTurnId)
                   ?? ChatTurns.LastOrDefault(t => t.TargetTabId == _activeTab.Id && t.HasEdits && !t.IsReverted);
        if (turn != null)
        {
            CancelTurn(turn);
        }
    }

    private void RestoreActiveTabTurn()
    {
        var turn = GetLatestEditTurnForTab(_activeTab);
        if (turn != null && turn.CanRestoreTurn)
        {
            RestoreTurn(turn);
        }
    }
}
