using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace mink.Models
{
    public class DocumentTab : ObservableObject
    {
        private string _title = "index.html";
        private string? _filePath;
        private string _content = "";
        private string _savedContent = "";
        private string _language = "html";
        private bool _isDirty;
        private int _pendingHunkCount;
        private string? _activeTurnId;
        private int _cursorLine = 1;
        private int _cursorColumn = 1;

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    OnPropertyChanged(nameof(DisplayTitle));
                }
            }
        }

        public string DisplayTitle => _isDirty ? $"{_title} ●" : _title;

        public string? FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string Content
        {
            get => _content;
            set
            {
                if (SetProperty(ref _content, value))
                {
                    IsDirty = _content != _savedContent;
                }
            }
        }

        public string SavedContent
        {
            get => _savedContent;
            set
            {
                if (SetProperty(ref _savedContent, value))
                {
                    IsDirty = _content != _savedContent;
                }
            }
        }

        public string Language
        {
            get => _language;
            set
            {
                if (SetProperty(ref _language, value))
                {
                    OnPropertyChanged(nameof(LanguageDisplay));
                }
            }
        }

        public string LanguageDisplay => _language switch
        {
            "html" => "HTML",
            "css" => "CSS",
            "scss" => "SCSS",
            "less" => "Less",
            "javascript" => "JavaScript",
            "typescript" => "TypeScript",
            "python" => "Python",
            "json" => "JSON",
            "markdown" => "Markdown",
            "xml" => "XML / SVG",
            "yaml" => "YAML",
            "shell" => "Shell",
            "powershell" => "PowerShell",
            "bat" => "Batch",
            "ini" => "Config / INI",
            "sql" => "SQL",
            "csharp" => "C#",
            "dockerfile" => "Dockerfile",
            _ => "Plain Text"
        };

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (SetProperty(ref _isDirty, value))
                {
                    OnPropertyChanged(nameof(DisplayTitle));
                }
            }
        }

        public int PendingHunkCount
        {
            get => _pendingHunkCount;
            set
            {
                if (SetProperty(ref _pendingHunkCount, value))
                {
                    OnPropertyChanged(nameof(HasPendingDiffs));
                }
            }
        }

        public bool HasPendingDiffs => _pendingHunkCount > 0;

        public string? ActiveTurnId
        {
            get => _activeTurnId;
            set => SetProperty(ref _activeTurnId, value);
        }

        public int CursorLine
        {
            get => _cursorLine;
            set => SetProperty(ref _cursorLine, value);
        }

        public int CursorColumn
        {
            get => _cursorColumn;
            set => SetProperty(ref _cursorColumn, value);
        }

        public static DocumentTab FromDisk(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);
            string text = sr.ReadToEnd();

            var tab = new DocumentTab
            {
                FilePath = filePath,
                Title = Path.GetFileName(filePath),
                _savedContent = text,
                _content = text,
                _isDirty = false
            };
            tab.DetectLanguage(filePath);
            return tab;
        }

        public static DocumentTab ForNewPath(string filePath)
        {
            var tab = new DocumentTab
            {
                FilePath = filePath,
                Title = Path.GetFileName(filePath),
                _savedContent = "",
                _content = "",
                _isDirty = false
            };
            tab.DetectLanguage(filePath);
            return tab;
        }

        public void MarkSaved(string? newPath = null)
        {
            if (!string.IsNullOrEmpty(newPath))
            {
                FilePath = newPath;
                Title = Path.GetFileName(newPath);
                DetectLanguage(newPath);
            }
            SavedContent = Content;
            IsDirty = false;
        }

        public void DetectLanguage(string pathOrName)
        {
            string fileName = Path.GetFileName(pathOrName);
            if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase))
            {
                Language = "dockerfile";
                return;
            }

            string extension = Path.GetExtension(pathOrName).ToLowerInvariant();
            Language = extension switch
            {
                ".html" or ".htm" or ".xhtml" => "html",
                ".css" => "css",
                ".scss" => "scss",
                ".less" => "less",
                ".js" or ".mjs" or ".cjs" or ".jsx" => "javascript",
                ".ts" or ".tsx" => "typescript",
                ".py" or ".pyw" => "python",
                ".json" or ".jsonc" or ".webmanifest" => "json",
                ".md" or ".markdown" => "markdown",
                ".xml" or ".svg" or ".csproj" or ".props" or ".targets" or ".axaml" or ".xaml" => "xml",
                ".yaml" or ".yml" => "yaml",
                ".sh" or ".bash" or ".zsh" => "shell",
                ".ps1" or ".psm1" or ".psd1" => "powershell",
                ".bat" or ".cmd" => "bat",
                ".ini" or ".cfg" or ".conf" or ".toml" or ".env" or ".editorconfig" => "ini",
                ".sql" => "sql",
                ".cs" => "csharp",
                _ => "plaintext"
            };
        }
    }
}
