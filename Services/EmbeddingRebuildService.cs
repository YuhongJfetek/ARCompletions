using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services;

public class EmbeddingRebuildService : IEmbeddingRebuildService
{
    private readonly ARCompletionsContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEmbeddingRetrievalService _embeddingRetrievalService;
    private readonly IDbLogger _dbLogger;
    private readonly IServiceProvider _serviceProvider;

    public EmbeddingRebuildService(ARCompletionsContext db, IEmbeddingService embeddingService, IEmbeddingRetrievalService embeddingRetrievalService, IDbLogger dbLogger, IServiceProvider serviceProvider)
    {
        _db = db;
        _embeddingService = embeddingService;
        _embeddingRetrievalService = embeddingRetrievalService;
        _dbLogger = dbLogger;
        _serviceProvider = serviceProvider;
    }

    public async Task<BotEmbeddingJob> RebuildAsync(string provider, string? model, string scope, string? faqId, string triggeredBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scope)) scope = "all";
        scope = scope.ToLowerInvariant();
        if (scope != "all" && scope != "single")
        {
            throw new ArgumentException("scope must be 'all' or 'single'", nameof(scope));
        }

        if (scope == "single" && string.IsNullOrWhiteSpace(faqId))
        {
            throw new ArgumentException("faqId is required when scope = 'single'", nameof(faqId));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = "openai";
        }

        await _dbLogger.LogAsync("Information", "RebuildAsync called", new { Provider = provider, Model = model, Scope = scope, TargetFaqId = faqId, TriggeredBy = triggeredBy });
        var resolvedModel = await ResolveModelAsync(model, cancellationToken);

        var faqQuery = _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled);
        if (scope == "single" && !string.IsNullOrWhiteSpace(faqId))
        {
            faqQuery = faqQuery.Where(f => f.FaqId == faqId);
        }

        var faqs = await faqQuery.ToListAsync(cancellationToken);

        var job = new BotEmbeddingJob
        {
            JobId = Guid.NewGuid(),
            Provider = provider,
            Model = resolvedModel,
            Scope = scope,
            TargetFaqId = scope == "single" ? faqId : null,
            Status = "pending",
            TotalCount = faqs.Count,
            CompletedCount = 0,
            FailedCount = 0,
            TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "system" : triggeredBy,
            StartedAt = null,
            FinishedAt = null,
            ErrorMessage = null
        };

        if (faqs.Count == 0)
        {
            job.Status = "failed";
            job.ErrorMessage = "沒有符合條件的 FAQ 可重建";
            _db.BotEmbeddingJobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);
            await _dbLogger.LogAsync("Warning", "Rebuild job has no FAQs", new { JobId = job.JobId, Provider = job.Provider, Model = job.Model, Scope = job.Scope });
            return job;
        }

        // persist job as running
        job.Status = "running";
        job.StartedAt = DateTimeOffset.UtcNow;
        _db.BotEmbeddingJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        await _dbLogger.LogAsync("Information", "Rebuild job started", new { JobId = job.JobId, Provider = job.Provider, Model = job.Model, Total = job.TotalCount });

        // process synchronously
        await ProcessJobAsync(job, faqs, resolvedModel, cancellationToken);
        return job;
    }

    public async Task<BotEmbeddingJob> StartRebuildAsync(string provider, string? model, string scope, string? faqId, string triggeredBy)
    {
        if (string.IsNullOrWhiteSpace(scope)) scope = "all";
        scope = scope.ToLowerInvariant();
        if (scope != "all" && scope != "single") throw new ArgumentException("scope must be 'all' or 'single'", nameof(scope));
        if (scope == "single" && string.IsNullOrWhiteSpace(faqId)) throw new ArgumentException("faqId is required when scope = 'single'", nameof(faqId));
        if (string.IsNullOrWhiteSpace(provider)) provider = "openai";

        await _dbLogger.LogAsync("Information", "StartRebuildAsync queued", new { Provider = provider, Model = model, Scope = scope, TargetFaqId = faqId, TriggeredBy = triggeredBy });
        var resolvedModel = await ResolveModelAsync(model, CancellationToken.None);

        var faqQuery = _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled);
        if (scope == "single" && !string.IsNullOrWhiteSpace(faqId)) faqQuery = faqQuery.Where(f => f.FaqId == faqId);
        var faqs = await faqQuery.ToListAsync();

        var job = new BotEmbeddingJob
        {
            JobId = Guid.NewGuid(),
            Provider = provider,
            Model = resolvedModel,
            Scope = scope,
            TargetFaqId = scope == "single" ? faqId : null,
            Status = "pending",
            TotalCount = faqs.Count,
            CompletedCount = 0,
            FailedCount = 0,
            TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "system" : triggeredBy,
            StartedAt = null,
            FinishedAt = null,
            ErrorMessage = null
        };

        if (faqs.Count == 0)
        {
            job.Status = "failed";
            job.ErrorMessage = "沒有符合條件的 FAQ 可重建";
            _db.BotEmbeddingJobs.Add(job);
            await _db.SaveChangesAsync();
            await _dbLogger.LogAsync("Warning", "StartRebuildAsync created empty job", new { JobId = job.JobId, Provider = job.Provider });
            return job;
        }

        job.Status = "running";
        job.StartedAt = DateTimeOffset.UtcNow;
        _db.BotEmbeddingJobs.Add(job);
        await _db.SaveChangesAsync();
        await _dbLogger.LogAsync("Information", "StartRebuildAsync job persisted", new { JobId = job.JobId, Provider = job.Provider, Total = job.TotalCount });

        // fire-and-forget: create a fresh DI scope and process the job there to get a fresh DbContext
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IEmbeddingRebuildService>();
                await svc.ProcessExistingJobAsync(job.JobId);
            }
            catch (Exception ex)
            {
                try
                {
                    // Use a fresh scope and DbContext to persist the job failure safely
                    using var errorScope = _serviceProvider.CreateScope();
                    var scopedDb = errorScope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
                    var existing = await scopedDb.BotEmbeddingJobs.FirstOrDefaultAsync(j => j.JobId == job.JobId);
                    if (existing != null)
                    {
                        existing.Status = "failed";
                        existing.ErrorMessage = ex.Message;
                        existing.FinishedAt = DateTimeOffset.UtcNow;
                        scopedDb.BotEmbeddingJobs.Update(existing);
                        await scopedDb.SaveChangesAsync();
                    }
                }
                catch { }
            }
        });

        return job;
    }

    private async Task ProcessJobAsync(BotEmbeddingJob job, List<BotFaqItem> faqs, string resolvedModel, CancellationToken cancellationToken)
    {
        var provider = job.Provider;
        var newEmbeddings = new List<BotFaqEmbedding>();
        var errors = new List<string>();

        var faqIdsForScope = faqs.Select(f => f.FaqId).ToList();
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbLogger.LogAsync("Information", "Processing rebuild job", new { JobId = job.JobId, Provider = provider, Model = resolvedModel, Count = faqs.Count });
            foreach (var faq in faqs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                // Ensure any existing embeddings for this FAQ are removed before creating a new one
                try
                {
                    // Only remove embeddings for the same provider to avoid deleting other providers' embeddings
                    var existingForFaq = await _db.BotFaqEmbeddings
                        .Where(e => e.FaqId == faq.FaqId && e.EmbeddingProvider == provider)
                        .ToListAsync(cancellationToken);
                    if (existingForFaq.Count > 0)
                    {
                        _db.BotFaqEmbeddings.RemoveRange(existingForFaq);
                        await _db.SaveChangesAsync(cancellationToken);
                        await _dbLogger.LogAsync("Debug", "Removed existing embeddings for FAQ", new { JobId = job.JobId, FaqId = faq.FaqId, Provider = provider, Removed = existingForFaq.Count });
                    }
                }
                catch
                {
                    // swallowing to avoid aborting the whole job on cleanup failure
                }

                try
                {
                    var text = BuildEmbeddingText(faq);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        job.FailedCount++;
                        errors.Add($"FAQ {faq.FaqId}: 無可用文字產生向量");
                        continue;
                    }

                    await _dbLogger.LogAsync("Debug", "Creating embedding for FAQ", new { JobId = job.JobId, FaqId = faq.FaqId, Provider = provider });
                    var vector = await _embeddingRetrievalService.GetOrCreateEmbeddingAsync(text, resolvedModel, provider, cancellationToken);
                    if (vector == null || vector.Length == 0)
                    {
                        job.FailedCount++;
                        errors.Add($"FAQ {faq.FaqId}: 取得向量失敗");
                        continue;
                    }

                    await _dbLogger.LogAsync("Debug", "Embedding created", new { JobId = job.JobId, FaqId = faq.FaqId, Len = vector.Length, Provider = provider });

                    var now = DateTimeOffset.UtcNow;
                    var entity = new BotFaqEmbedding
                    {
                        FaqId = faq.FaqId,
                        Question = faq.Question,
                        SearchText = faq.SearchTextCache ?? faq.Question,
                        CategoryKey = faq.CategoryKey,
                        EmbeddingProvider = provider,
                        EmbeddingModel = resolvedModel,
                        VectorDim = vector.Length,
                        Embedding = vector,
                        IsActive = false,
                        CreatedAt = now,
                        RebuiltAt = now
                    };

                    _db.BotFaqEmbeddings.Add(entity);
                    newEmbeddings.Add(entity);
                    job.CompletedCount++;
                }
                catch (Exception ex)
                {
                    job.FailedCount++;
                    errors.Add($"FAQ {faq.FaqId}: {ex.Message}");
                    await _dbLogger.LogAsync("Error", "Embedding rebuild failed for FAQ", new { FaqId = faq.FaqId, JobId = job.JobId }, ex);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _dbLogger.LogAsync("Information", "Saved new embeddings batch", new { JobId = job.JobId, Count = newEmbeddings.Count });

            if (newEmbeddings.Count > 0)
            {
                var targetFaqIds = newEmbeddings.Select(e => e.FaqId).Distinct().ToList();
                var newIds = newEmbeddings.Select(e => e.EmbeddingId).ToList();

                var allForScope = await _db.BotFaqEmbeddings
                    .Where(e => e.EmbeddingProvider == provider && e.EmbeddingModel == resolvedModel && targetFaqIds.Contains(e.FaqId))
                    .ToListAsync(cancellationToken);

                foreach (var emb in allForScope)
                {
                    emb.IsActive = newIds.Contains(emb.EmbeddingId);
                }

                await _db.SaveChangesAsync(cancellationToken);
                await _dbLogger.LogAsync("Information", "Updated IsActive flags for provider scope", new { JobId = job.JobId, Provider = provider, Updated = allForScope.Count });
            }

            job.FinishedAt = DateTimeOffset.UtcNow;
            job.Status = job.CompletedCount > 0 ? "completed" : "failed";
            if (errors.Count > 0)
            {
                job.ErrorMessage = string.Join("; ", errors.Take(10));
            }

            _db.BotEmbeddingJobs.Update(job);
            await _db.SaveChangesAsync(cancellationToken);
            await _dbLogger.LogAsync("Information", "Rebuild job finished", new { JobId = job.JobId, Status = job.Status, Completed = job.CompletedCount, Failed = job.FailedCount });
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            try { await tx.RollbackAsync(cancellationToken); } catch { }
            throw;
        }
    }

    public async Task ProcessExistingJobAsync(Guid jobId)
    {
        // This method runs inside a fresh scope with its own DbContext.
        var job = await GetJobAsync(jobId);
        if (job == null) return;

        // load faqs according to job scope
        var faqQuery = _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled);
        if (job.Scope == "single" && !string.IsNullOrWhiteSpace(job.TargetFaqId)) faqQuery = faqQuery.Where(f => f.FaqId == job.TargetFaqId);
        var faqs = await faqQuery.ToListAsync();

        await ProcessJobAsync(job, faqs, job.Model, CancellationToken.None);
    }

    public async Task<BotEmbeddingJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _db.BotEmbeddingJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
    }

    private async Task<string> ResolveModelAsync(string? requestedModel, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            return requestedModel;
        }

        var setting = await _db.BotConstantsConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == "bot.embedding.model", cancellationToken);

        if (!string.IsNullOrWhiteSpace(setting?.ConfigValue))
        {
            return setting.ConfigValue!;
        }

        return "text-embedding-3-small";
    }

    private static string BuildEmbeddingText(BotFaqItem faq)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(faq.Question)) parts.Add(faq.Question);
        if (!string.IsNullOrWhiteSpace(faq.Answer)) parts.Add(faq.Answer);
        if (!string.IsNullOrWhiteSpace(faq.SearchTextCache)) parts.Add(faq.SearchTextCache);
        if (!string.IsNullOrWhiteSpace(faq.Keywords)) parts.Add(faq.Keywords);
        if (!string.IsNullOrWhiteSpace(faq.QueryExamples)) parts.Add(faq.QueryExamples);
        if (!string.IsNullOrWhiteSpace(faq.AliasTerms)) parts.Add(faq.AliasTerms);
        if (!string.IsNullOrWhiteSpace(faq.Sources)) parts.Add(faq.Sources);
        return string.Join(" ", parts);
    }

    private async Task<double[]?> CreateEmbeddingVectorAsync(string text, string model, CancellationToken cancellationToken)
    {
        var json = await _embeddingService.GetEmbeddingJsonAsync(text, model);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataElem) || dataElem.GetArrayLength() == 0)
            {
                return null;
            }

            var embElem = dataElem[0].GetProperty("embedding");
            var list = new List<double>();
            foreach (var v in embElem.EnumerateArray())
            {
                list.Add(v.GetDouble());
            }

            return list.Count > 0 ? list.ToArray() : null;
        }
        catch (Exception ex)
        {
            await _dbLogger.LogAsync("Error", "Failed to parse embedding JSON", null, ex);
            return null;
        }
    }
}
