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



                                // 使用 OpenAI 分析（若之前未成功或未設定，現在要求必須有 openai key）
                                var conf2 = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                                var openAiKey2 = conf2["OpenAI:ApiKey"];
                                if (string.IsNullOrWhiteSpace(openAiKey2)) openAiKey2 = conf2["Embedding:OpenAiApiKey"]; // backwards-compat
                                if (string.IsNullOrWhiteSpace(openAiKey2)) openAiKey2 = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

                                if (string.IsNullOrWhiteSpace(openAiKey2))
                                {
                                    job.Status = "Failed";
                                    job.Error = "OpenAI API key not configured; analysis requires OpenAI.";
                                    job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    db.SaveChanges();
                                    continue;
                                }

                                var ok = await TryAnalyzeWithOpenAIAsync(scope.ServiceProvider, db, job, docs, openAiKey2);
                                if (!ok)
                                {
                                    job.Status = "Failed";
                                    job.Error = "OpenAI analysis failed";
                                    job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    db.SaveChanges();
                                    continue;
                                }

                                // notify user(s) that analysis finished
                                try
                                {
                                    await NotifyUserAsync(scope.ServiceProvider, db, job);
                                }
                                catch
                                {
                                    // swallow notification errors; job already marked completed
                                }
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

        private async Task NotifyUserAsync(IServiceProvider provider, Data.ARCompletionsContext db, Data.AnalysisJob job)
        {
            if (job == null) return;

            string? targetUserId = null;

            try
            {
                if (!string.IsNullOrEmpty(job.Params))
                {
                    using var doc = JsonDocument.Parse(job.Params);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("lineUserId", out var lu) && lu.ValueKind == JsonValueKind.String)
                        targetUserId = lu.GetString();
                    else if (root.TryGetProperty("userId", out var u) && u.ValueKind == JsonValueKind.String)
                        targetUserId = u.GetString();
                    else if (root.TryGetProperty("targetUserId", out var tu) && tu.ValueKind == JsonValueKind.String)
                        targetUserId = tu.GetString();
                }
            }
            catch { /* ignore parse errors */ }

            // if not found in params, try to locate a recent LineEventLog within job time window
            if (string.IsNullOrEmpty(targetUserId))
            {
                // attempt to extract from Params 'from'/'to'
                long from = 0; long to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                try
                {
                    if (!string.IsNullOrEmpty(job.Params))
                    {
                        using var doc2 = JsonDocument.Parse(job.Params);
                        var root2 = doc2.RootElement;
                        if (root2.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.Number) from = f.GetInt64();
                        if (root2.TryGetProperty("to", out var t) && t.ValueKind == JsonValueKind.Number) to = t.GetInt64();
                    }
                }
                catch { }

                if (from == 0) from = to - 7 * 24 * 3600;

                var fromDt = DateTimeOffset.FromUnixTimeSeconds(from).UtcDateTime;
                var toDt = DateTimeOffset.FromUnixTimeSeconds(to).UtcDateTime;

                var ev = db.LineEventLogs.Where(l => l.CreatedAt >= fromDt && l.CreatedAt <= toDt).OrderByDescending(l => l.CreatedAt).FirstOrDefault();
                if (ev != null) targetUserId = ev.LineUserId;
            }

            if (string.IsNullOrEmpty(targetUserId)) return;

            // resolve LineBotService
            var line = provider.GetService<LineBotService>();
            if (line == null) return;

            var text = $"分析已完成：{job.ResultSummary}";
            try
            {
                await line.PushText(targetUserId, text);
                job.ResultSummary = (job.ResultSummary ?? "") + ";notified=true";
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                job.ResultSummary = (job.ResultSummary ?? "") + $";notify_error={ex.Message}";
                db.SaveChanges();
            }
        }

        private async Task<bool> TryAnalyzeWithOpenAIAsync(IServiceProvider provider, Data.ARCompletionsContext db, Data.AnalysisJob job, System.Collections.Generic.IEnumerable<object> docs, string apiKey)
        {
            try
            {
            if (docs == null || !docs.Any()) return false;

                var httpFactory = provider.GetService<IHttpClientFactory>();
                if (httpFactory == null) return false;

                var client = httpFactory.CreateClient("OpenAI");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.Remove("User-Agent");
                client.DefaultRequestHeaders.Add("User-Agent", "ARCompletions-Worker/1.0");

                // limit number of docs and build a prompt
                int maxDocs = 80;
                var docsList = docs.ToList();
                var take = docsList.Count > maxDocs ? docsList.Skip(docsList.Count - maxDocs).ToList() : docsList;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < take.Count; i++)
                {
                    var d = take[i];
                    // d has properties Id and Content
                    var idProp = d.GetType().GetProperty("Id");
                    var contentProp = d.GetType().GetProperty("Content");
                    var cid = idProp?.GetValue(d)?.ToString() ?? "";
                    var text = contentProp?.GetValue(d)?.ToString() ?? "";
                    sb.AppendLine($"---DOC {cid}---");
                    sb.AppendLine(text);
                }
                    var systemPrompt = "你是一個分析系統，輸入是一組 AI 回覆（文字），請回傳 JSON，格式如下：{\"successRate\":0.0,\"high\":0,\"med\":0,\"low\":0,\"docs\":N,\"details\":[{\"completionId\":\"...\",\"similarity\":0.0,\"riskLevel\":0,\"flags\":\"high|medium|low\"}],\"summaryText\":\"...\"}." + System.Environment.NewLine +
                        "風險分級規則可依內容相似性（或主題危險性）判定，請儘量以數字與標準化欄位回傳，不要包含多餘的說明文字.";

                var userContent = "以下為要分析的 AI 回覆（每筆以 ---DOC id--- 開頭）：\n" + sb.ToString();

                var payload = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new object[] {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userContent }
                    },
                    temperature = 0.0,
                    max_tokens = 1500
                };

                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(payload));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var resp = await client.PostAsync("v1/chat/completions", content);
                if (!resp.IsSuccessStatusCode) return false;

                var respText = await resp.Content.ReadAsStringAsync();
                using var docRoot = System.Text.Json.JsonDocument.Parse(respText);
                var root = docRoot.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) return false;
                var msg = choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

                // 尋找第一個 JSON 物件並解析
                var first = msg.IndexOf('{');
                var last = msg.LastIndexOf('}');
                if (first < 0 || last < 0 || last <= first) return false;
                var json = msg.Substring(first, last - first + 1);

                using var parsed = System.Text.Json.JsonDocument.Parse(json);
                var prow = parsed.RootElement;

                // optional: remove existing details for this job
                var old = db.AnalysisDetails.Where(d => d.JobId == job.Id).ToList();
                if (old.Count > 0)
                {
                    db.AnalysisDetails.RemoveRange(old);
                    db.SaveChanges();
                }

                int docsCount = 0; double successRate = 0; int high = 0, med = 0, low = 0;
                if (prow.TryGetProperty("docs", out var pjDocs) && pjDocs.ValueKind == System.Text.Json.JsonValueKind.Number) docsCount = pjDocs.GetInt32();
                if (prow.TryGetProperty("successRate", out var pjSr) && pjSr.ValueKind == System.Text.Json.JsonValueKind.Number) successRate = pjSr.GetDouble();
                if (prow.TryGetProperty("high", out var pjHigh) && pjHigh.ValueKind == System.Text.Json.JsonValueKind.Number) high = pjHigh.GetInt32();
                if (prow.TryGetProperty("med", out var pjMed) && pjMed.ValueKind == System.Text.Json.JsonValueKind.Number) med = pjMed.GetInt32();
                if (prow.TryGetProperty("low", out var pjLow) && pjLow.ValueKind == System.Text.Json.JsonValueKind.Number) low = pjLow.GetInt32();

                if (prow.TryGetProperty("details", out var pjDetails) && pjDetails.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var it in pjDetails.EnumerateArray())
                    {
                        var cid = it.TryGetProperty("completionId", out var pc) && pc.ValueKind == System.Text.Json.JsonValueKind.String ? pc.GetString() : null;
                        var sim = it.TryGetProperty("similarity", out var ps) && ps.ValueKind == System.Text.Json.JsonValueKind.Number ? ps.GetDouble() : 0.0;
                        var rl = it.TryGetProperty("riskLevel", out var pr) && pr.ValueKind == System.Text.Json.JsonValueKind.Number ? pr.GetInt32() : 0;
                        var flags = it.TryGetProperty("flags", out var pf) && pf.ValueKind == System.Text.Json.JsonValueKind.String ? pf.GetString() : null;

                        db.AnalysisDetails.Add(new Data.AnalysisDetail
                        {
                            JobId = job.Id,
                            CompletionId = cid ?? string.Empty,
                            Similarity = sim,
                            RiskLevel = rl,
                            Flags = flags
                        });
                    }
                }

                // build ResultSummary
                job.Progress = 100;
                job.Status = "Completed";
                job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                job.ResultSummary = $"successRate={successRate:0.000};high={high};med={med};low={low};docs={docsCount}";

                // also store assistant summary text (escaped) if exists
                if (prow.TryGetProperty("summaryText", out var pSummary) && pSummary.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var summaryText = pSummary.GetString() ?? string.Empty;
                    // append escaped summary to ResultSummary for retrieval (URL-encoded)
                    job.ResultSummary += ";summary=" + System.Uri.EscapeDataString(summaryText);
                }

                db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
