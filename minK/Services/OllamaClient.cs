using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace mink.Services
{
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;

        public OllamaClient(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        public static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "http://localhost:11434";

            string trimmed = baseUrl.Trim().TrimEnd('/');
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "http://" + trimmed;
            }
            return trimmed;
        }

        public async Task<(bool Reachable, List<string> Models, string? Error)> ProbeServerAsync(
            string baseUrl,
            CancellationToken ct = default)
        {
            var models = new List<string>();
            try
            {
                string url = $"{NormalizeBaseUrl(baseUrl)}/api/tags";
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(4));

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, models, $"HTTP {(int)response.StatusCode}");
                }

                string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("models", out var modelsArr) &&
                    modelsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in modelsArr.EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var nameProp) &&
                            nameProp.ValueKind == JsonValueKind.String)
                        {
                            string? name = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                models.Add(name);
                            }
                        }
                    }
                }

                return (true, models, null);
            }
            catch (Exception ex)
            {
                return (false, models, ex.Message);
            }
        }

        public async Task<bool> PingAsync(string baseUrl, CancellationToken ct = default)
        {
            var (reachable, _, _) = await ProbeServerAsync(baseUrl, ct).ConfigureAwait(false);
            return reachable;
        }

        public async IAsyncEnumerable<string> StreamGenerateAsync(
            string baseUrl,
            string model,
            string prompt,
            string system = "",
            double temperature = 0.2,
            int contextWindow = 8192,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            var payload = new
            {
                model,
                prompt,
                system,
                stream = true,
                options = new
                {
                    temperature,
                    num_ctx = contextWindow
                }
            };

            string jsonBody = JsonSerializer.Serialize(payload);
            string endpoint = $"{NormalizeBaseUrl(baseUrl)}/api/generate";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096);

            while (!ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? token = null;
                using (var doc = JsonDocument.Parse(line))
                {
                    if (doc.RootElement.TryGetProperty("response", out var respProp))
                    {
                        token = respProp.GetString();
                    }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                }
            }
        }
    }
}
