using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services;

public class BulkJobWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IBulkJobQueue _bulkQueue;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly ILogger<BulkJobWorker> _logger;

    public BulkJobWorker(IServiceProvider services, IBulkJobQueue bulkQueue, IBackgroundJobQueue jobQueue, ILogger<BulkJobWorker> logger)
    {
        _services = services;
        _bulkQueue = bulkQueue;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BulkJobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string bulkId = null!;
                if (_bulkQueue.Reader.TryRead(out var id))
                {
                    bulkId = id;
                }
                else
                {
                    // wait a bit
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                if (string.IsNullOrEmpty(bulkId)) continue;

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();

                var job = await db.BulkJobs.FindAsync(new object[] { bulkId }, stoppingToken);
                if (job == null) continue;

                // try to claim
                job.Status = "processing";
                job.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await db.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Processing BulkJob {Id} action={Action}", job.Id, job.Action);

                // parse filter
                string? vendorId = null;
                string? status = null;
                string? vectorVersion = null;
                try
                {
                    if (!string.IsNullOrEmpty(job.FilterJson))
                    {
                        var doc = JsonDocument.Parse(job.FilterJson);
                        if (doc.RootElement.TryGetProperty("vendorId", out var v)) vendorId = v.GetString();
                        if (doc.RootElement.TryGetProperty("status", out var s)) status = s.GetString();
                        if (doc.RootElement.TryGetProperty("vectorVersion", out var vv)) vectorVersion = vv.GetString();
                    }
                }
                catch { }

                // only support EmbeddingJob.BulkRetry for now
                if (job.Action == "EmbeddingJob.BulkRetry")
                {
                    var query = db.EmbeddingJobs.AsQueryable();
                    if (!string.IsNullOrWhiteSpace(vendorId)) query = query.Where(j => j.VendorId == vendorId);
                    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);
                    if (!string.IsNullOrWhiteSpace(vectorVersion)) query = query.Where(j => j.VectorVersion == vectorVersion);

                    const int chunkSize = 500;
                    long processed = 0;
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var ids = await query.OrderBy(j => j.CreatedAt).Select(j => j.Id).Skip((int)processed).Take(chunkSize).ToListAsync(stoppingToken);
                        if (ids == null || ids.Count == 0) break;

                        var jobs = await db.EmbeddingJobs.Where(j => ids.Contains(j.Id)).ToListAsync(stoppingToken);
                        foreach (var ej in jobs)
                        {
                            ej.Status = "pending";
                            ej.ErrorMessage = null;
                            ej.StartedAt = null;
                            ej.FinishedAt = null;
                            try { _jobQueue.Enqueue(ej.Id); } catch { }

                            db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                Actor = job.Initiator,
                                Action = "EmbeddingJob.BulkRetry",
                                TargetId = ej.Id,
                                Payload = null,
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });
                        }

                        processed += ids.Count;
                        job.ProcessedCount = processed;
                        job.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        await db.SaveChangesAsync(stoppingToken);

                        if (ids.Count < chunkSize) break;
                    }

                    job.Status = "done";
                    job.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    await db.SaveChangesAsync(stoppingToken);
                }
                else
                {
                    job.Status = "failed";
                    job.ErrorMessage = "unknown action";
                    job.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BulkJobWorker error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("BulkJobWorker stopped");
    }
}
