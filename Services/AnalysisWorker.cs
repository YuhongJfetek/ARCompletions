using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    // Background worker that claims pending AiFaqAnalysisJobs and invokes IAnalysisService
    public class AnalysisWorker : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IAnalysisService _analysisService;
        private readonly IBackgroundJobQueue _jobQueue;
        private readonly ILogger<AnalysisWorker> _logger;

        public AnalysisWorker(IServiceProvider services, IAnalysisService analysisService, IBackgroundJobQueue jobQueue, ILogger<AnalysisWorker> logger)
        {
            _services = services;
            _analysisService = analysisService;
            _jobQueue = jobQueue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AnalysisWorker started");

            var reader = _jobQueue.Reader;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // try to read from in-memory queue first
                    var jobId = await reader.ReadAsync(stoppingToken);
                    if (!string.IsNullOrWhiteSpace(jobId))
                    {
                        await ProcessJobAsync(jobId, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading from analysis job queue");
                    await Task.Delay(2000, stoppingToken);
                }

                // Poll DB for pending jobs in case the queue missed any
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
                    var pending = await db.AiFaqAnalysisJobs
                        .Where(j => j.Status == "pending")
                        .OrderBy(j => j.CreatedAt)
                        .Select(j => j.Id)
                        .Take(10)
                        .ToListAsync(stoppingToken);

                    foreach (var id in pending)
                    {
                        // claim atomically
                        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"AiFaqAnalysisJobs\" SET \"Status\" = {"processing"}, \"StartedAt\" = {now} WHERE \"Id\" = {id} AND \"Status\" = {"pending"}", stoppingToken);
                        if (claimed > 0)
                        {
                            await ProcessJobAsync(id, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling DB for pending analysis jobs");
                }

                await Task.Delay(5000, stoppingToken);
            }

            _logger.LogInformation("AnalysisWorker stopping");
        }

        private async Task ProcessJobAsync(string jobId, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Processing AiFaqAnalysisJob {JobId}", jobId);
                await _analysisService.ProcessAnalysisJobAsync(jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analysis service failed for job {JobId}", jobId);
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
                    var job = await db.AiFaqAnalysisJobs.FindAsync(new object[] { jobId }, ct);
                    if (job != null)
                    {
                        job.Status = "failed";
                        job.ErrorMessage = ex.Message;
                        job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        await db.SaveChangesAsync(ct);
                    }
                }
                catch (Exception e2)
                {
                    _logger.LogError(e2, "Failed to mark job failed in DB for {JobId}", jobId);
                }
            }
        }
    }
}
