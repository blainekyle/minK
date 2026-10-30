using DiffPlex;
using mink.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace mink.Services
{
    public class DiffAndPatchService
    {
        private static readonly Regex SearchReplaceRegex = new(
            @"<<<<<<<[ \t]*SEARCH[ \t]*\r?\n([\s\S]*?)=======[ \t]*\r?\n([\s\S]*?)>>>>>>>[ \t]*REPLACE",
            RegexOptions.Compiled);

        private static readonly Regex FencedBlockRegex = new(
            @"```(?:[a-zA-Z0-9_+-]*)[ \t]*\r?\n([\s\S]*?)```",
            RegexOptions.Compiled);

        public bool TryExtractProposedContent(
            string originalContent,
            string rawResponse,
            bool editModeEnabled,
            out string proposedContent,
            out string cleanExplanation)
        {
            proposedContent = originalContent;
            cleanExplanation = rawResponse.Trim();

            if (!editModeEnabled || string.IsNullOrWhiteSpace(rawResponse))
            {
                return false;
            }

            string normOriginal = NormalizeLineEndings(originalContent);

            // 1. Check for targeted SEARCH / REPLACE blocks first
            var srMatches = SearchReplaceRegex.Matches(rawResponse);
            if (srMatches.Count > 0)
            {
                string working = normOriginal;
                int appliedCount = 0;

                foreach (Match match in srMatches)
                {
                    string searchRaw = NormalizeLineEndings(match.Groups[1].Value);
                    string replaceRaw = NormalizeLineEndings(match.Groups[2].Value);

                    if (searchRaw.EndsWith("\n")) searchRaw = searchRaw[..^1];
                    if (replaceRaw.EndsWith("\n")) replaceRaw = replaceRaw[..^1];

                    if (string.IsNullOrEmpty(searchRaw) && string.IsNullOrWhiteSpace(working))
                    {
                        working = replaceRaw;
                        appliedCount++;
                        continue;
                    }

                    if (TryApplySearchReplace(working, searchRaw, replaceRaw, out string updated))
                    {
                        working = updated;
                        appliedCount++;
                    }
                }

                string stripped = SearchReplaceRegex.Replace(rawResponse, "").Trim();
                stripped = Regex.Replace(stripped, @"```[a-zA-Z0-9_+-]*\s*```", "").Trim();
                cleanExplanation = string.IsNullOrWhiteSpace(stripped)
                    ? $"Applied {appliedCount} targeted edit block(s) to the active document."
                    : stripped;

                if (appliedCount > 0 && working != normOriginal)
                {
                    proposedContent = working;
                    return true;
                }

                return false;
            }

            // 2. Fallback: Check for complete updated file in a fenced code block
            var fenceMatches = FencedBlockRegex.Matches(rawResponse);
            if (fenceMatches.Count > 0)
            {
                Match bestMatch = fenceMatches
                    .Cast<Match>()
                    .OrderByDescending(m => m.Groups[1].Value.Length)
                    .First();

                string candidate = NormalizeLineEndings(bestMatch.Groups[1].Value);
                if (candidate.EndsWith("\n") && !normOriginal.EndsWith("\n") && !string.IsNullOrEmpty(normOriginal))
                {
                    candidate = candidate[..^1];
                }

                int origLines = string.IsNullOrEmpty(normOriginal) ? 0 : normOriginal.Split('\n').Length;
                int candLines = candidate.Split('\n').Length;

                bool safeFullReplace =
                    string.IsNullOrWhiteSpace(normOriginal) ||
                    origLines <= 30 ||
                    candLines >= (int)(origLines * 0.45) ||
                    (normOriginal.Contains("<html", StringComparison.OrdinalIgnoreCase) &&
                     candidate.Contains("<html", StringComparison.OrdinalIgnoreCase));

                if (safeFullReplace && candidate != normOriginal)
                {
                    string stripped = rawResponse.Remove(bestMatch.Index, bestMatch.Length).Trim();
                    cleanExplanation = string.IsNullOrWhiteSpace(stripped)
                        ? "Updated active document with proposed changes."
                        : stripped;

                    proposedContent = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryApplySearchReplace(string source, string search, string replace, out string result)
        {
            result = source;
            if (string.IsNullOrEmpty(search))
            {
                return false;
            }

            // 1. Exact full-line-boundary match
            int searchPos = 0;
            while (searchPos <= source.Length - search.Length)
            {
                int idx = source.IndexOf(search, searchPos, StringComparison.Ordinal);
                if (idx < 0) break;

                bool atLineStart = idx == 0 || source[idx - 1] == '\n';
                int endIdx = idx + search.Length;
                bool atLineEnd = endIdx == source.Length || source[endIdx] == '\n';

                if (atLineStart && atLineEnd)
                {
                    result = source[..idx] + replace + source[endIdx..];
                    return true;
                }

                searchPos = idx + 1;
            }

            // 2. Line-by-line trimmed whitespace-tolerant match
            string[] srcLines = source.Split('\n');
            string[] searchLines = search.Split('\n');
            if (searchLines.Length == 0 || searchLines.Length > srcLines.Length)
            {
                return false;
            }

            for (int i = 0; i <= srcLines.Length - searchLines.Length; i++)
            {
                bool allMatch = true;
                for (int j = 0; j < searchLines.Length; j++)
                {
                    if (srcLines[i + j].Trim() != searchLines[j].Trim())
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    var sb = new List<string>(srcLines.Length);
                    for (int k = 0; k < i; k++) sb.Add(srcLines[k]);
                    if (!string.IsNullOrEmpty(replace))
                    {
                        sb.AddRange(replace.Split('\n'));
                    }
                    for (int k = i + searchLines.Length; k < srcLines.Length; k++) sb.Add(srcLines[k]);
                    result = string.Join("\n", sb);
                    return true;
                }
            }

            return false;
        }

        public List<DiffHunk> ComputeHunks(string turnId, string beforeText, string afterText)
        {
            string normBefore = NormalizeLineEndings(beforeText);
            string normAfter = NormalizeLineEndings(afterText);

            var hunks = new List<DiffHunk>();
            if (normBefore == normAfter)
            {
                return hunks;
            }

            var differ = Differ.Instance;
            var diffResult = differ.CreateCustomDiffs(
                normBefore,
                normAfter,
                ignoreWhiteSpace: false,
                ignoreCase: false,
                chunker: text => text.Split('\n'));

            int index = 1;
            foreach (var block in diffResult.DiffBlocks)
            {
                var origLines = diffResult.PiecesOld
                    .Skip(block.DeleteStartA)
                    .Take(block.DeleteCountA)
                    .ToArray();

                var modLines = diffResult.PiecesNew
                    .Skip(block.InsertStartB)
                    .Take(block.InsertCountB)
                    .ToArray();

                var hunk = new DiffHunk
                {
                    Id = $"{turnId}-hunk-{index++}",
                    TurnId = turnId,
                    OriginalLines = origLines,
                    ModifiedLines = modLines,
                    NewStartLine = block.InsertCountB > 0 ? block.InsertStartB + 1 : 0,
                    NewLineCount = block.InsertCountB,
                    AnchorLine = block.InsertStartB
                };

                hunks.Add(hunk);
            }

            return hunks;
        }

        public static string NormalizeLineEndings(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
