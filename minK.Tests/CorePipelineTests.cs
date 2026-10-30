using mink.Models;
using mink.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace mink.Tests
{
    public class CorePipelineTests
    {
        private readonly DiffAndPatchService _diffService = new();

        [Fact]
        public void SearchReplace_AppliesMultipleTargetedEdits_AndExtractsCleanSummary()
        {
            const string original = """
<!DOCTYPE html>
<html>
<head>
  <title>Old Title</title>
</head>
<body>
  <h1>Hello World</h1>
  <footer>Copyright 2023</footer>
</body>
</html>
""";

            const string llmOutput = """
Updated the page title and bumped the footer copyright year to 2026.

<<<<<<< SEARCH
  <title>Old Title</title>
=======
  <title>minK Web App</title>
>>>>>>> REPLACE

<<<<<<< SEARCH
  <footer>Copyright 2023</footer>
=======
  <footer>Copyright 2026</footer>
  <p class="tagline">Local First</p>
>>>>>>> REPLACE
""";

            bool applied = _diffService.TryExtractProposedContent(
                original,
                llmOutput,
                editModeEnabled: true,
                out string proposed,
                out string explanation);

            Assert.True(applied);
            Assert.Contains("<title>minK Web App</title>", proposed);
            Assert.Contains("<footer>Copyright 2026</footer>", proposed);
            Assert.Contains("<p class=\"tagline\">Local First</p>", proposed);
            Assert.DoesNotContain("<<<<<<< SEARCH", explanation);
            Assert.Contains("Updated the page title", explanation);

            var hunks = _diffService.ComputeHunks("turn1", original, proposed);
            Assert.Equal(2, hunks.Count);

            Assert.Equal(4, hunks[0].NewStartLine);
            Assert.Equal(1, hunks[0].NewLineCount);
            Assert.Single(hunks[0].OriginalLines);
            Assert.Equal("  <title>Old Title</title>", hunks[0].OriginalLines[0]);

            Assert.Equal(8, hunks[1].NewStartLine);
            Assert.Equal(2, hunks[1].NewLineCount);
            Assert.Single(hunks[1].OriginalLines);
            Assert.Equal(2, hunks[1].ModifiedLines.Length);
        }

        [Fact]
        public void SearchReplace_HandlesIndentationDriftGracefully()
        {
            const string original = "<div>\n    <span>Original</span>\n</div>";
            const string llmOutput = """
Changed span text.
<<<<<<< SEARCH
  <span>Original</span>
=======
    <span>Updated</span>
>>>>>>> REPLACE
""";

            bool applied = _diffService.TryExtractProposedContent(
                original,
                llmOutput,
                editModeEnabled: true,
                out string proposed,
                out _);

            Assert.True(applied);
            Assert.Equal("<div>\n    <span>Updated</span>\n</div>", proposed);
        }

        [Fact]
        public void SafetyGuard_PreventsTinySnippetFromClobberingLargeHtmlFile()
        {
            string largeHtml = "<!DOCTYPE html>\n<html>\n<body>\n" +
                               string.Join("\n", Enumerable.Range(1, 60).Select(i => $"  <p>Line {i}</p>")) +
                               "\n</body>\n</html>";

            const string conversationalAnswer = """
You can center elements using flexbox like this:
```css
.container {
  display: flex;
}
```
""";

            bool applied = _diffService.TryExtractProposedContent(
                largeHtml,
                conversationalAnswer,
                editModeEnabled: true,
                out string proposed,
                out _);

            Assert.False(applied);
            Assert.Equal(largeHtml, proposed);
        }

        [Fact]
        public void ComputeHunks_AccuratelyMarksPureDeletionAnchors()
        {
            const string before = "line1\nline2\nline3\nline4";
            const string after = "line1\nline4";

            var hunks = _diffService.ComputeHunks("turnDel", before, after);
            Assert.Single(hunks);
            Assert.Equal(0, hunks[0].NewLineCount);
            Assert.Equal(1, hunks[0].AnchorLine);
            Assert.Equal(new[] { "line2", "line3" }, hunks[0].OriginalLines);
        }

        [Fact]
        public async Task OllamaClient_NormalizesLanUrls_AndProbesLocalhostQwen()
        {
            Assert.Equal("http://192.168.1.150:11434", OllamaClient.NormalizeBaseUrl("192.168.1.150:11434/"));
            Assert.Equal("http://localhost:11434", OllamaClient.NormalizeBaseUrl("http://localhost:11434/"));

            var client = new OllamaClient();
            var (reachable, models, _) = await client.ProbeServerAsync("http://localhost:11434");

            Assert.True(reachable);
            Assert.Contains(models, m => m.Contains("qwen2.5-coder", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ChatTurn_RestoreAndCancelButtonVisibility_MatchesFullAndPartialCancelStates()
        {
            var turn = new ChatTurn
            {
                HasEdits = true,
                IsStreaming = false,
                TotalHunkCount = 3,
                PendingHunkCount = 3
            };

            // 1. Freshly applied turn: Keep All + Cancel visible, Restore hidden
            Assert.True(turn.CanAcceptAll);
            Assert.True(turn.CanRevertTurn);
            Assert.False(turn.CanRestoreTurn);

            // 2. Partial hunk reverted: Restore appears beside Cancel
            turn.RevertedHunkCount = 1;
            turn.PendingHunkCount = 2;
            Assert.True(turn.CanAcceptAll);
            Assert.True(turn.CanRevertTurn);
            Assert.True(turn.CanRestoreTurn);

            // 3. Remaining hunks accepted, then user manually edits/deletes lines: Restore appears beside Cancel
            turn.RevertedHunkCount = 0;
            turn.PendingHunkCount = 0;
            turn.IsPartiallyModified = true;
            Assert.False(turn.CanAcceptAll);
            Assert.True(turn.CanRevertTurn);
            Assert.True(turn.CanRestoreTurn);

            // 4. Whole turn canceled: Cancel hides and Restore appears in its place
            turn.IsPartiallyModified = false;
            turn.IsReverted = true;
            Assert.False(turn.CanAcceptAll);
            Assert.False(turn.CanRevertTurn);
            Assert.True(turn.CanRestoreTurn);

            // 5. Restored back to full turn state: Restore hides and Cancel returns
            turn.IsReverted = false;
            turn.RevertedHunkCount = 0;
            turn.IsPartiallyModified = false;
            turn.PendingHunkCount = 3;
            Assert.True(turn.CanAcceptAll);
            Assert.True(turn.CanRevertTurn);
            Assert.False(turn.CanRestoreTurn);
        }

        [Fact]
        public void SettingsService_PersistsGeneralPreferencesAndConfirmClearChatReset()
        {
            string tempSettingsPath = Path.Combine(Path.GetTempPath(), $"mink-test-settings-{Guid.NewGuid():N}.json");
            try
            {
                var service = new SettingsService(tempSettingsPath);
                var saved = new AppSettings
                {
                    FontSize = 16,
                    TabSize = 4,
                    WordWrap = false,
                    ShowMinimap = false,
                    AutoCloseTagsAndBrackets = false,
                    DefaultLivePreview = false,
                    ShowPreviewOnStartup = false,
                    ShowAgentOnStartup = true,
                    ConfirmClearChat = false,
                    Ollama = new OllamaConfig
                    {
                        BaseUrl = "http://192.168.1.42:11434",
                        ModelName = "qwen2.5-coder:7b"
                    }
                };

                service.Save(saved);
                var reloaded = service.Load();

                Assert.Equal(16, reloaded.FontSize);
                Assert.Equal(4, reloaded.TabSize);
                Assert.False(reloaded.WordWrap);
                Assert.False(reloaded.ShowMinimap);
                Assert.False(reloaded.AutoCloseTagsAndBrackets);
                Assert.False(reloaded.DefaultLivePreview);
                Assert.False(reloaded.ShowPreviewOnStartup);
                Assert.True(reloaded.ShowAgentOnStartup);
                Assert.False(reloaded.ConfirmClearChat);
                Assert.Equal("http://192.168.1.42:11434", reloaded.Ollama.BaseUrl);

                // Resetting ConfirmClearChat in Preferences persists cleanly
                reloaded.ConfirmClearChat = true;
                service.Save(reloaded);
                Assert.True(service.Load().ConfirmClearChat);
            }
            finally
            {
                if (File.Exists(tempSettingsPath))
                {
                    File.Delete(tempSettingsPath);
                }
            }
        }

        [Fact]
        public void ResolveCliPaths_ResolvesRelativePathsSkipsFlagsAndHandlesDriveRootQuotes()
        {
            string baseDir = @"C:\Projects\DemoSite";
            string[] rawArgs = { "-n", "index.html", @"css\site.css", ".", "C:\"" };

            string[] resolved = SingleInstanceService.ResolveCliPaths(rawArgs, baseDir);

            Assert.Equal(4, resolved.Length);
            Assert.Equal(@"C:\Projects\DemoSite\index.html", resolved[0]);
            Assert.Equal(@"C:\Projects\DemoSite\css\site.css", resolved[1]);
            Assert.Equal(@"C:\Projects\DemoSite", resolved[2]);
            Assert.Equal(@"C:\", resolved[3]);
        }

        [Fact]
        public async Task SingleInstanceService_ForwardsCliFilePathsOverNamedPipe()
        {
            string testPipe = $"minK-TestPipe-{Guid.NewGuid():N}";
            var tcs = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var server = new SingleInstanceService(paths => tcs.TrySetResult(paths), testPipe);
            server.StartServer();

            // Brief delay to allow async NamedPipeServerStream listener to bind
            await Task.Delay(60);

            string[] expected = { @"C:\Work\index.html", @"C:\Work\styles.css" };
            bool forwarded = SingleInstanceService.TryForwardToRunningInstance(expected, testPipe, timeoutMs: 1000);

            Assert.True(forwarded);
            string[] received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(expected, received);
        }

        [Fact]
        public void DocumentTab_ForNewPath_DetectsWebCodeAndPlaintextAssociations()
        {
            var htmlTab = DocumentTab.ForNewPath(@"C:\Work\page.html");
            Assert.Equal("page.html", htmlTab.Title);
            Assert.Equal("html", htmlTab.Language);
            Assert.False(htmlTab.IsDirty);

            var scssTab = DocumentTab.ForNewPath(@"C:\Work\theme.scss");
            Assert.Equal("scss", scssTab.Language);

            var dockerTab = DocumentTab.ForNewPath(@"C:\Work\Dockerfile");
            Assert.Equal("dockerfile", dockerTab.Language);

            var gitCommitTab = DocumentTab.ForNewPath(@"C:\Work\.git\COMMIT_EDITMSG");
            Assert.Equal("plaintext", gitCommitTab.Language);
        }
    }
}
