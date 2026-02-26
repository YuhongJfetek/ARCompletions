using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace ARCompletions.Services
{
    public class AnalysisWorker : BackgroundService
    {
        private readonly IServiceProvider _services;

        public AnalysisWorker(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<Data.ARCompletionsContext>();
                        var job = db.AnalysisJobs.Where(j => j.Status == "Queued").OrderBy(j => j.CreatedAt).FirstOrDefault();
                        if (job != null)
                        {
                            job.Status = "Executing";
                            job.StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            db.SaveChanges();

                            try
                            {
                                // parse params for time window: expects JSON like {"from":1234567890, "to":1234567899}
                                long from = 0;
                                long to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                try
                                {
                                    var pj = System.Text.Json.JsonDocument.Parse(job.Params ?? "{}");
                                    if (pj.RootElement.TryGetProperty("from", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.Number)
                                        from = f.GetInt64();
                                    if (pj.RootElement.TryGetProperty("to", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Number)
                                        to = t.GetInt64();
                                }
                                catch { /* ignore parse errors */ }

                                // default last 7 days if not set
                                if (from == 0) from = to - 7 * 24 * 3600;

                                // collect AI replies in window
                                var aiMessages = db.ChatMessages
                                    .Where(m => m.Role == "ai" && m.CreatedAt >= from && m.CreatedAt <= to)
                                    .OrderBy(m => m.CreatedAt)
                                    .ToList();

                                // limit for pairwise similarity
                                int limit = 1000;
                                if (aiMessages.Count > limit)
                                    aiMessages = aiMessages.Skip(aiMessages.Count - limit).ToList();

                                // build tokenized documents and ensure embeddings exist
                                var docs = aiMessages.Select(m => new { m.Id, m.Content }).ToList();

                                int vectorDim = 1536;
                                var docTexts = new List<string>();
                                for (int i = 0; i < docs.Count; i++)
                                {
                                    var d = docs[i];
                                    // check existing embedding
                                    var existsEmbedding = db.ChatEmbeddings.FirstOrDefault(e => e.ChatMessageId == d.Id);
                                    if (existsEmbedding == null)
                                    {
                                        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                                        // determine API key: prefer configured Embedding:OpenAiApiKey, fallback to env OPENAI_API_KEY
                                                        var conf = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                                                        var apiKey = conf["Embedding:OpenAiApiKey"];
                                                        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                                                        var embedSvc = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                                                        var vec = await embedSvc.ComputeEmbeddingAsync(Normalize(d.Content), vectorDim, apiKey);
                                        if (vec == null) vec = new double[vectorDim];

                                        if (db.Database.ProviderName != null && db.Database.ProviderName.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            // create vector literal like '[0.1,0.2,...]'
                                            var literal = "[" + string.Join(',', vec.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
                                            var sql = $"INSERT INTO ChatEmbeddings(\"Id\", \"ChatMessageId\", \"Embedding\", \"CreatedAt\") VALUES ('" + Guid.NewGuid().ToString() + "', '" + d.Id + "', '{literal}'::vector, {createdAt});";
                                            db.Database.ExecuteSqlRaw(sql);
                                        }
                                        else
                                        {
                                            db.ChatEmbeddings.Add(new Data.ChatEmbedding { ChatMessageId = d.Id, EmbeddingJson = JsonSerializer.Serialize(vec), CreatedAt = createdAt });
                                            db.SaveChanges();
                                        }
                                    }
                                    docTexts.Add(Normalize(d.Content));
                                }

                                // build vocabulary and token frequencies from normalized texts
                                var docsNorm = docTexts.ToList();
                                var vocab = new Dictionary<string, int>();
                                var docTokens = new List<Dictionary<int, double>>();
                                for (int idx = 0; idx < docs.Count; idx++)
                                {
                                    var text = docsNorm[idx];
                                    var tokens = Tokenize(text);
                                    var freq = new Dictionary<int, double>();
                                    foreach (var tk in tokens)
                                    {
                                        if (!vocab.TryGetValue(tk, out var vidx))
                                        {
                                            vidx = vocab.Count;
                                            vocab[tk] = vidx;
                                        }
                                        if (!freq.ContainsKey(vidx)) freq[vidx] = 0;
                                        freq[vidx] += 1;
                                    }
                                    docTokens.Add(freq);
                                }

                                // convert to dense vectors when computing dot-products
                                int n = docTokens.Count;
                                var norms = new double[n];
                                for (int i = 0; i < n; i++)
                                {
                                    double s = 0;
                                    foreach (var kv in docTokens[i]) s += kv.Value * kv.Value;
                                    norms[i] = Math.Sqrt(s);
                                }

                                // compute nearest neighbour similarity for each doc
                                var similarities = new double[n];
                                for (int i = 0; i < n; i++)
                                {
                                    double maxSim = 0.0;
                                    for (int j = 0; j < n; j++)
                                    {
                                        if (i == j) continue;
                                        double dot = 0;
                                        // iterate over smaller dictionary
                                        var a = docTokens[i];
                                        var b = docTokens[j];
                                        if (a.Count < b.Count)
                                        {
                                            foreach (var kv in a)
                                            {
                                                if (b.TryGetValue(kv.Key, out var bv)) dot += kv.Value * bv;
                                            }
                                        }
                                        else
                                        {
                                            foreach (var kv in b)
                                            {
                                                if (a.TryGetValue(kv.Key, out var av)) dot += kv.Value * av;
                                            }
                                        }
                                        double denom = norms[i] * norms[j];
                                        double sim = denom == 0 ? 0 : dot / denom;
                                        if (sim > maxSim) maxSim = sim;
                                    }
                                    similarities[i] = n <= 1 ? 1.0 : maxSim;
                                }

                                // decide risk level thresholds
                                int highRisk = 0, mediumRisk = 0, lowRisk = 0;
                                for (int i = 0; i < n; i++)
                                {
                                    double sim = similarities[i];
                                    int risk = sim < 0.3 ? 2 : (sim < 0.6 ? 1 : 0);
                                    if (risk == 2) highRisk++; else if (risk == 1) mediumRisk++; else lowRisk++;

                                    db.AnalysisDetails.Add(new Data.AnalysisDetail
                                    {
                                        JobId = job.Id,
                                        CompletionId = docs[i].Id, // store ChatMessage.Id here
                                        Similarity = sim,
                                        RiskLevel = risk,
                                        Flags = risk == 2 ? "high" : (risk == 1 ? "medium" : "low")
                                    });
                                }

                                // success rate from Completions
                                var total = db.Completions.Count();
                                var success = db.Completions.Count(c => c.Complate);
                                var successRate = total == 0 ? 0.0 : (double)success / total;

                                job.Progress = 100;
                                job.Status = "Completed";
                                job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                job.ResultSummary = $"successRate={successRate:0.000};high={highRisk};med={mediumRisk};low={lowRisk};docs={n}";
                                db.SaveChanges();
                            }
                            catch (Exception ex)
                            {
                                job.Status = "Failed";
                                job.Error = ex.Message;
                                job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                db.SaveChanges();
                            }
                        }
                    }
                }
                catch
                {
                    // swallow and retry
                }
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.ToLowerInvariant();
            s = s.Replace('\r', ' ').Replace('\n', ' ');
            return s;
        }

        static async Task<double[]> ComputeEmbeddingAsync(string text, int dim, string? apiKey, IHttpClientFactory? httpFactory)
        {
            if (string.IsNullOrEmpty(text)) return new double[dim];
            if (!string.IsNullOrEmpty(apiKey) && httpFactory != null)
            {
                var client = httpFactory.CreateClient("OpenAI");
                // set per-request headers
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
                            // retry on rate limit or server errors
                            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
                            {
                                var jitter = rnd.Next(0, 100);
                                var delayMs = (int)(Math.Pow(2, attempt - 1) * 500) + jitter;
                                await Task.Delay(delayMs);
                                continue;
                            }
                            // non-transient error -> break and fall back
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
            foreach (var tk in Tokenize(text))
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

        static IEnumerable<string> Tokenize(string s)
        {
            if (string.IsNullOrEmpty(s)) yield break;
            foreach (var tk in Regex.Split(s, "\\W+"))
            {
                if (string.IsNullOrWhiteSpace(tk)) continue;
                yield return tk;
            }
        }
    }
}
