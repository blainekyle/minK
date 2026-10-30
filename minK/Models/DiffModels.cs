using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace mink.Models
{
    public class DiffHunk
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("turnId")]
        public string TurnId { get; set; } = "";

        [JsonPropertyName("newStartLine")]
        public int NewStartLine { get; set; }

        [JsonPropertyName("newLineCount")]
        public int NewLineCount { get; set; }

        [JsonPropertyName("anchorLine")]
        public int AnchorLine { get; set; }

        [JsonPropertyName("originalLines")]
        public string[] OriginalLines { get; set; } = Array.Empty<string>();

        [JsonPropertyName("modifiedLines")]
        public string[] ModifiedLines { get; set; } = Array.Empty<string>();

        [JsonPropertyName("hadTrailingNewline")]
        public bool HadTrailingNewline { get; set; } = true;
    }

    public class ChatTurn : ObservableObject
    {
        private string _assistantMessage = "";
        private bool _isStreaming;
        private bool _hasEdits;
        private bool _isReverted;
        private bool _isPartiallyModified;
        private int _pendingHunkCount;
        private int _totalHunkCount;
        private int _revertedHunkCount;

        public string Id { get; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; } = DateTime.Now;
        public string TimeDisplay => CreatedAt.ToString("h:mm tt");

        public string UserPrompt { get; set; } = "";
        public string? TargetTabId { get; set; }
        public string TargetTabTitle { get; set; } = "";
        public string SnapshotBefore { get; set; } = "";
        public string SnapshotAfter { get; set; } = "";
        public List<DiffHunk> Hunks { get; set; } = new();

        public string AssistantMessage
        {
            get => _assistantMessage;
            set => SetProperty(ref _assistantMessage, value);
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set
            {
                if (SetProperty(ref _isStreaming, value))
                {
                    NotifyActionStates();
                }
            }
        }

        public bool HasEdits
        {
            get => _hasEdits;
            set
            {
                if (SetProperty(ref _hasEdits, value))
                {
                    NotifyActionStates();
                }
            }
        }

        public bool IsReverted
        {
            get => _isReverted;
            set
            {
                if (SetProperty(ref _isReverted, value))
                {
                    NotifyActionStates();
                }
            }
        }

        public bool IsPartiallyModified
        {
            get => _isPartiallyModified;
            set
            {
                if (SetProperty(ref _isPartiallyModified, value))
                {
                    NotifyActionStates();
                }
            }
        }

        public int TotalHunkCount
        {
            get => _totalHunkCount;
            set
            {
                if (SetProperty(ref _totalHunkCount, value))
                {
                    NotifyActionStates();
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
                    NotifyActionStates();
                }
            }
        }

        public int RevertedHunkCount
        {
            get => _revertedHunkCount;
            set
            {
                if (SetProperty(ref _revertedHunkCount, value))
                {
                    NotifyActionStates();
                }
            }
        }

        public bool CanAcceptAll => _hasEdits && !_isReverted && !_isStreaming && _pendingHunkCount > 0;
        public bool CanRevertTurn => _hasEdits && !_isReverted && !_isStreaming;
        public bool CanRestoreTurn => _hasEdits && !_isStreaming && (_isReverted || _revertedHunkCount > 0 || _isPartiallyModified);

        public IBrush EditStatusBrush => _isReverted
            ? SolidColorBrush.Parse("#E3B341")
            : SolidColorBrush.Parse("#7EE787");

        public string EditStatusBadge
        {
            get
            {
                if (!_hasEdits) return "";
                if (_isReverted) return "⟲ Turn Canceled";
                if (_revertedHunkCount > 0 && _pendingHunkCount > 0)
                    return $"● {_pendingHunkCount} pending, {_revertedHunkCount} reverted in {TargetTabTitle}";
                if (_revertedHunkCount > 0 && _pendingHunkCount == 0)
                    return $"● Partial edits kept ({_revertedHunkCount} reverted) in {TargetTabTitle}";
                if (_pendingHunkCount > 0)
                    return $"● {_pendingHunkCount} of {_totalHunkCount} edit(s) pending in {TargetTabTitle}";
                if (_isPartiallyModified)
                    return $"● Modified since turn in {TargetTabTitle}";
                return $"✓ Edits applied to {TargetTabTitle}";
            }
        }

        private void NotifyActionStates()
        {
            OnPropertyChanged(nameof(CanAcceptAll));
            OnPropertyChanged(nameof(CanRevertTurn));
            OnPropertyChanged(nameof(CanRestoreTurn));
            OnPropertyChanged(nameof(EditStatusBadge));
            OnPropertyChanged(nameof(EditStatusBrush));
        }
    }

    public class ExplorerFileItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Icon { get; set; } = "📄";
    }
}
