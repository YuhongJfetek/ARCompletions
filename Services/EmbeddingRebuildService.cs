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
    private readonly ILogger<EmbeddingRebuildService> _logger;

    public EmbeddingRebuildService(ARCompletionsContext db, IEmbeddingService embeddingService, ILogger<EmbeddingRebuildService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
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

        if (!string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only provider 'openai' is supported for automatic rebuild.");
        }

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
            return job;
        }

        job.Status = "running";
        job.StartedAt = DateTimeOffset.UtcNow;
        _db.BotEmbeddingJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        var newEmbeddings = new List<BotFaqEmbedding>();
        var errors = new List<string>();

        foreach (var faq in faqs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var text = BuildEmbeddingText(faq);
                if (string.IsNullOrWhiteSpace(text))
                {
                    job.FailedCount++;
                    errors.Add($"FAQ {faq.FaqId}: 無可用文字產生向量");
                    continue;
                }

                var vector = await CreateEmbeddingVectorAsync(text, resolvedModel, cancellationToken);
                if (vector == null || vector.Length == 0)
                {
                    job.FailedCount++;
                    errors.Add($"FAQ {faq.FaqId}: 取得向量失敗");
                    continue;
                }

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
                _logger.LogError(ex, "Embedding rebuild failed for FAQ {FaqId} in job {JobId}", faq.FaqId, job.JobId);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

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
        }

        job.FinishedAt = DateTimeOffset.UtcNow;
        job.Status = job.CompletedCount > 0 ? "completed" : "failed";
        if (errors.Count > 0)
        {
            job.ErrorMessage = string.Join("; ", errors.Take(10));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return job;
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
            _logger.LogError(ex, "Failed to parse embedding JSON");
            return null;
        }
    }
}
