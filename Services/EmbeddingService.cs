using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IHttpClientFactory _httpFactory;

        public EmbeddingService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        public async Task<double[]> ComputeEmbeddingAsync(string text, int dim, string? apiKey)
        {
            if (string.IsNullOrEmpty(text)) return new double[dim];

            if (!string.IsNullOrEmpty(apiKey) && _httpFactory != null)
            {
                var client = _httpFactory.CreateClient("OpenAI");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.Remove("User-Agent");
                client.DefaultRequestHeaders.Add("User-Agent", "ARCompletions-Worker/1.0");

                var payload = new { input = text, model = "text-embedding-3-small" };
                var content = new StringContent(JsonSerializer.Serialize(payload));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var rnd = new Random();
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        var resp = await client.PostAsync("v1/embeddings", content);
                        if (resp.IsSuccessStatusCode)
                        {
                            using var st = await resp.Content.ReadAsStreamAsync();
                            using var doc = await JsonDocument.ParseAsync(st);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                            {
                                var emb = data[0].GetProperty("embedding");
                                var list = new List<double>();
                                foreach (var e in emb.EnumerateArray()) list.Add(e.GetDouble());
                                if (list.Count == dim) return list.ToArray();
                                var outv = new double[dim];
                                for (int i = 0; i < Math.Min(dim, list.Count); i++) outv[i] = list[i];
                                return outv;
                            }
                        }
                        else
                        {
                            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                            {
                                var jitter = rnd.Next(0, 100);
                                var delayMs = (int)(Math.Pow(2, attempt - 1) * 500) + jitter;
                                await Task.Delay(delayMs);
                                continue;
                            }
                            break;
                        }
                    }
                    catch
                    {
                        var jitter = rnd.Next(0, 100);
                        var delayMs = (int)(Math.Pow(2, attempt - 1) * 500) + jitter;
                        await Task.Delay(delayMs);
                    }
                }
            }

            // fallback deterministic hash projection
            var vec = new double[dim];
            foreach (var tk in RegexSplit(text))
            {
                int h = tk.GetHashCode();
                if (h == int.MinValue) h = 0;
                h = Math.Abs(h);
                int idx = h % dim;
                vec[idx] += 1.0;
            }
            double sum = 0;
            for (int i = 0; i < dim; i++) sum += vec[i] * vec[i];
            double norm = Math.Sqrt(sum);
            if (norm > 0)
            {
                for (int i = 0; i < dim; i++) vec[i] /= norm;
            }
            return vec;
        }

        private static IEnumerable<string> RegexSplit(string s)
        {
            if (string.IsNullOrEmpty(s)) yield break;
            foreach (var tk in System.Text.RegularExpressions.Regex.Split(s.ToLowerInvariant().Replace('\r',' ').Replace('\n',' '), "\\W+"))
            {
                if (string.IsNullOrWhiteSpace(tk)) continue;
                yield return tk;
            }
        }
    }
}
