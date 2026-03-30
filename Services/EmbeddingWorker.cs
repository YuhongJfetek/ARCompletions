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

    public EmbeddingWorker(IServiceProvider services, IEmbeddingService embeddingService, IBackgroundJobQueue jobQueue, ILogger<EmbeddingWorker> logger)
    {
        _services = services;
        _embeddingService = embeddingService;
        _jobQueue = jobQueue;
        _logger = logger;
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
}
