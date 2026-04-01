using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services;

public class EmbeddingWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IEmbeddingService _embeddingService;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly TimeSpan _loopDelay = TimeSpan.FromSeconds(5);
    private readonly int _maxTokensPerChunk;

    public EmbeddingWorker(IServiceProvider services, IEmbeddingService embeddingService, IBackgroundJobQueue jobQueue, ILogger<EmbeddingWorker> logger)
    {
        _services = services;
        _embeddingService = embeddingService;
        _jobQueue = jobQueue;
        _logger = logger;
        var str = Environment.GetEnvironmentVariable("EMBEDDING_MAX_TOKENS_PER_CHUNK");
        if (!int.TryParse(str, out _maxTokensPerChunk)) _maxTokensPerChunk = 750;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmbeddingWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();


                // first, try to get a jobId from the enqueue queue
                string? queuedJobId = null;
                if (_jobQueue.Reader.TryRead(out var dequeuedId))
                {
                    queuedJobId = dequeuedId;
                }

                string? jobIdToProcess = null;

                if (!string.IsNullOrEmpty(queuedJobId))
                {
                    jobIdToProcess = queuedJobId;
                }
                else
                {
                    // fallback: pick a pending job id first
                    var candidate = await db.EmbeddingJobs
                        .Where(j => j.Status == "pending")
                        .OrderBy(j => j.CreatedAt)
                        .Select(j => new { j.Id })
                        .FirstOrDefaultAsync(stoppingToken);

                    if (candidate == null)
                    {
                        await Task.Delay(_loopDelay, stoppingToken);
                        continue;
                    }

                    jobIdToProcess = candidate.Id;
                }

                if (string.IsNullOrEmpty(jobIdToProcess))
                {
                    await Task.Delay(_loopDelay, stoppingToken);
                    continue;
                }

                // try to atomically claim the job: update status only if still pending
                var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var rows = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"EmbeddingJobs\" SET \"Status\" = 'processing', \"StartedAt\" = {nowSec} WHERE \"Id\" = {jobIdToProcess} AND \"Status\" = 'pending'", stoppingToken);
                if (rows == 0)
                {
                    // someone else claimed it or job not pending; loop again
                    continue;
                }

                // reload job entity
                var job = await db.EmbeddingJobs.FindAsync(new object[] { jobIdToProcess }, stoppingToken);
                if (job == null)
                {
                    continue;
                }

                // If no EmbeddingItems exist for this job, prepare items depending on TriggerType
                var existingCount = await db.EmbeddingItems.CountAsync(i => i.EmbeddingJobId == job.Id, stoppingToken);
                if (existingCount == 0)
                {
                    // Partial already handled earlier; for other trigger types, create items from FAQs
                    if (job.TriggerType != null && job.TriggerType == "partial")
                    {
                        // partial preparation handled above
                    }
                    else
                    {
                        try
                        {
                            // create embedding items for all FAQs under this vendor
                            var faqs = await db.Faqs.Where(f => f.VendorId == job.VendorId && f.IsActive).ToListAsync(stoppingToken);
                            foreach (var f in faqs)
                            {
                                // split into chunks to avoid too-long inputs
                                var content = (f.Question ?? "") + "\n" + (f.Answer ?? "");
                                var chunks = ChunkTextByTokens(content, _maxTokensPerChunk);
                                foreach (var chunk in chunks)
                                {
                                    var item = new EmbeddingItem
                                    {
                                        Id = Guid.NewGuid().ToString("N"),
                                        EmbeddingJobId = job.Id,
                                        VendorId = f.VendorId,
                                        FaqId = f.Id,
                                        ChunkText = chunk,
                                        EmbeddingJson = "",
                                        EmbeddingVector = null,
                                        TokenCount = EstimateTokens(chunk),
                                        Status = "pending",
                                        ErrorMessage = null,
                                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                    };
                                    db.EmbeddingItems.Add(item);
                                }
                            }

                            var count = await db.EmbeddingItems.CountAsync(i => i.EmbeddingJobId == job.Id, stoppingToken);
                            job.TotalFaqCount = (int)count;
                            await db.SaveChangesAsync(stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to prepare full EmbeddingJob items for job {JobId}", job.Id);
                        }
                    }
                }

                // If this is a partial job (contains faqIds in JsonFilePath), create EmbeddingItems for those FAQs
                if (!string.IsNullOrEmpty(job.TriggerType) && job.TriggerType == "partial" && !string.IsNullOrEmpty(job.JsonFilePath))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(job.JsonFilePath);
                        if (doc.RootElement.TryGetProperty("faqIds", out var idsElem) && idsElem.ValueKind == JsonValueKind.Array)
                        {
                            var faqIds = idsElem.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList();
                            if (faqIds.Any())
                            {
                                // load faqs belonging to vendor and in list
                                var faqs = await db.Faqs.Where(f => faqIds.Contains(f.Id) && f.VendorId == job.VendorId).ToListAsync(stoppingToken);

                                foreach (var f in faqs)
                                {
                                        // create one EmbeddingItem per FAQ (no chunking for now)
                                    var exists = await db.EmbeddingItems.AnyAsync(i => i.EmbeddingJobId == job.Id && i.FaqId == f.Id, stoppingToken);
                                    if (exists) continue;

                                    var item = new EmbeddingItem
                                    {
                                        Id = Guid.NewGuid().ToString("N"),
                                        EmbeddingJobId = job.Id,
                                        VendorId = f.VendorId,
                                        FaqId = f.Id,
                                        ChunkText = (f.Question ?? "") + "\n" + (f.Answer ?? ""),
                                        EmbeddingJson = "",
                                        EmbeddingVector = null,
                                        TokenCount = EstimateTokens((f.Question ?? "") + "\n" + (f.Answer ?? "")),
                                        Status = "pending",
                                        ErrorMessage = null,
                                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                    };
                                    db.EmbeddingItems.Add(item);
                                }

                                // update job total count if currently zero
                                var count = await db.EmbeddingItems.CountAsync(i => i.EmbeddingJobId == job.Id, stoppingToken);
                                job.TotalFaqCount = (int)count;
                                await db.SaveChangesAsync(stoppingToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to prepare partial EmbeddingJob items for job {JobId}", job.Id);
                    }
                }

                _logger.LogInformation("Processing EmbeddingJob {JobNo} ({Id})", job.JobNo, job.Id);

                // fetch items for job
                var items = await db.EmbeddingItems
                    .Where(i => i.EmbeddingJobId == job.Id && i.Status != "done")
                    .OrderBy(i => i.CreatedAt)
                    .ToListAsync(stoppingToken);

                foreach (var item in items)
                {
                    try
                    {
                        var respJson = await _embeddingService.GetEmbeddingJsonAsync(item.ChunkText, job.ModelName);
                        if (respJson != null)
                        {
                            item.EmbeddingJson = respJson;
                            // parse embedding vector from OpenAI response: data[0].embedding
                            try
                            {
                                using var doc = JsonDocument.Parse(respJson);
                                if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                                {
                                    var embElem = dataElem[0].GetProperty("embedding");
                                    var vec = embElem.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                                    item.EmbeddingVector = vec;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse embedding vector for item {ItemId}", item.Id);
                            }

                            item.Status = "done";
                            item.ErrorMessage = null;
                            job.SuccessCount += 1;

                            // write per-item log
                            db.EmbeddingLogs.Add(new EmbeddingLog
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                VendorId = item.VendorId,
                                EmbeddingJobId = job.Id,
                                ActionType = "item_done",
                                Message = $"Item {item.Id} embedded",
                                DetailJson = respJson,
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });
                        }
                        else
                        {
                            item.Status = "failed";
                            item.ErrorMessage = "no response";
                            job.FailCount += 1;
                            db.EmbeddingLogs.Add(new EmbeddingLog
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                VendorId = item.VendorId,
                                EmbeddingJobId = job.Id,
                                ActionType = "item_failed",
                                Message = $"Item {item.Id} failed to embed",
                                DetailJson = null,
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error embedding item {ItemId}", item.Id);
                        item.Status = "failed";
                        item.ErrorMessage = ex.Message;
                        job.FailCount += 1;
                        db.EmbeddingLogs.Add(new EmbeddingLog
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            VendorId = item.VendorId,
                            EmbeddingJobId = job.Id,
                            ActionType = "item_exception",
                            Message = ex.Message,
                            DetailJson = ex.ToString(),
                            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        });
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }

                job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                job.Status = job.FailCount == 0 ? "done" : "done_with_errors";
                await db.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Finished EmbeddingJob {JobNo} (success:{Success} fail:{Fail})", job.JobNo, job.SuccessCount, job.FailCount);

                // if too many failures, write an alert log
                if (job.FailCount > Math.Max(5, job.TotalFaqCount / 10))
                {
                    db.EmbeddingLogs.Add(new EmbeddingLog
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        VendorId = job.VendorId,
                        EmbeddingJobId = job.Id,
                        ActionType = "job_failed_alert",
                        Message = $"EmbeddingJob {job.JobNo} experienced {job.FailCount} failures",
                        DetailJson = null,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogCritical("EmbeddingJob {JobNo} had excessive failures ({FailCount})", job.JobNo, job.FailCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingWorker main loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("EmbeddingWorker stopped");
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // word-based heuristic: tokens ~= 1.3 * wordCount
        try
        {
            var words = System.Text.RegularExpressions.Regex.Matches(text, "\\w+", System.Text.RegularExpressions.RegexOptions.Multiline);
            var wc = words.Count;
            if (wc > 0)
            {
                return Math.Max(1, (int)Math.Ceiling(wc * 1.3));
            }
        }
        catch { }

        // fallback to char-based heuristic
        return Math.Max(1, text.Length / 4);
    }

    private static System.Collections.Generic.List<string> ChunkTextByTokens(string text, int maxTokens)
    {
        var result = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        // Split by whitespace into words, accumulate until token estimate exceeds maxTokens
        var words = System.Text.RegularExpressions.Regex.Split(text.Trim(), "(\\s+)");
        var current = new System.Text.StringBuilder();
        var currentTokens = 0;

        foreach (var w in words)
        {
            if (string.IsNullOrWhiteSpace(w))
            {
                current.Append(w);
                continue;
            }

            var add = w;
            var addTokens = EstimateTokens(add);

            if (currentTokens + addTokens > maxTokens && current.Length > 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                currentTokens = 0;
            }

            current.Append(add);
            // append a space if needed
            current.Append(' ');
            currentTokens += addTokens;
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }

    private static System.Collections.Generic.List<string> ChunkText(string text, int maxLen)
    {
        var result = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        var remaining = text.Trim();
        while (!string.IsNullOrEmpty(remaining))
        {
            if (remaining.Length <= maxLen)
            {
                result.Add(remaining);
                break;
            }

            // try to split at last whitespace before maxLen
            var idx = remaining.LastIndexOf(' ', maxLen);
            if (idx <= 0) idx = maxLen; // no whitespace found, hard split

            var part = remaining.Substring(0, idx).Trim();
            result.Add(part);
            remaining = remaining.Substring(idx).Trim();
        }

        return result;
    }
}
