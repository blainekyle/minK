using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mink.Services
{
    public sealed class SingleInstanceService : IDisposable
    {
        private readonly string _pipeName;
        private readonly Action<string[]> _onPathsReceived;
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenTask;

        public SingleInstanceService(Action<string[]> onPathsReceived, string? customPipeName = null)
        {
            _onPathsReceived = onPathsReceived;
            _pipeName = customPipeName ?? GetDefaultPipeName();
        }

        public static string GetDefaultPipeName()
        {
            string rawUser = Environment.UserName ?? "default";
            var sb = new StringBuilder("minK-IPC-");
            foreach (char c in rawUser)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString();
        }

        public static string[] ResolveCliPaths(string[]? args, string baseDirectory)
        {
            if (args == null || args.Length == 0)
            {
                return Array.Empty<string>();
            }

            string effectiveBase = string.IsNullOrWhiteSpace(baseDirectory)
                ? Environment.CurrentDirectory
                : baseDirectory;

            var resolved = new List<string>();
            foreach (string raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string trimmed = raw.Trim();
                if (trimmed.StartsWith('-')) continue;

                // Strip stray quotes (including Windows Explorer "%V" drive-root trailing quote quirk, e.g. C:")
                string cleaned = trimmed.Trim('"').Trim();
                if (string.IsNullOrEmpty(cleaned)) continue;

                if (cleaned.Length == 2 && char.IsLetter(cleaned[0]) && cleaned[1] == ':')
                {
                    cleaned += Path.DirectorySeparatorChar;
                }

                try
                {
                    string fullPath = Path.IsPathRooted(cleaned)
                        ? Path.GetFullPath(cleaned)
                        : Path.GetFullPath(Path.Combine(effectiveBase, cleaned));
                    resolved.Add(fullPath);
                }
                catch
                {
                    // Ignore malformed path argument
                }
            }

            return resolved.ToArray();
        }

        public static bool TryForwardToRunningInstance(string[] resolvedPaths, string? customPipeName = null, int timeoutMs = 160)
        {
            string pipeName = customPipeName ?? GetDefaultPipeName();
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                client.Connect(timeoutMs);

                string payload = JsonSerializer.Serialize(resolvedPaths ?? Array.Empty<string>());
                byte[] bytes = Encoding.UTF8.GetBytes(payload + "\n");
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void StartServer()
        {
            if (_listenTask != null) return;
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 4,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[]? paths = JsonSerializer.Deserialize<string[]>(line);
                        if (paths != null)
                        {
                            _onPathsReceived(paths);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Brief backoff if pipe creation races during rapid invocations
                    try { await Task.Delay(50, ct).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
