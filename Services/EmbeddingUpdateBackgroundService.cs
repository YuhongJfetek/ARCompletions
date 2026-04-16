using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    public class EmbeddingUpdateBackgroundService : BackgroundService
    {
        private readonly IEmbeddingUpdateQueue _queue;
        private readonly IServiceProvider _services;
        private readonly IDbLogger _logger;

        public EmbeddingUpdateBackgroundService(IEmbeddingUpdateQueue queue, IServiceProvider services, IDbLogger logger)
        {
            _queue = queue;
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enumerator = ((EmbeddingUpdateQueue)_queue).ReadAllAsync().GetAsyncEnumerator();
            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().AsTask().WaitAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (!moved) break;

                    var req = enumerator.Current;
                    try
                    {
                        using var scope = _services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
                        var retrieval = scope.ServiceProvider.GetRequiredService<IEmbeddingRetrievalService>();
                        var now = DateTimeOffset.UtcNow;

                        await _logger.LogAsync("Debug", "EmbeddingUpdateBackgroundService dequeued request", new { req.FaqId, req.Provider });

                        // attempt to get or create embedding from provider
                        var vec = await retrieval.GetOrCreateEmbeddingAsync(req.Text, req.Model ?? string.Empty, req.Provider, stoppingToken);
                        if (vec == null || vec.Length == 0)
                        {
                            await _logger.LogAsync("Warning", "Embedding update produced no vector", new { req.FaqId, req.Provider });
                            continue;
                        }

                        // persist: mark existing embeddings for faq+provider IsActive = false and insert new one marked active
                        try
                        {
                            var existing = await db.BotFaqEmbeddings.Where(e => e.FaqId == req.FaqId && e.EmbeddingProvider == req.Provider).ToListAsync(stoppingToken);
                            foreach (var e in existing) e.IsActive = false;

                            var entity = new ARCompletions.Domain.BotFaqEmbedding
                            {
                                EmbeddingId = Guid.NewGuid(),
                                FaqId = req.FaqId,
                                Question = null,
                                SearchText = req.Text,
                                EmbeddingProvider = req.Provider,
                                EmbeddingModel = req.Model ?? string.Empty,
                                VectorDim = vec.Length,
                                Embedding = vec,
                                IsActive = true,
                                CreatedAt = now,
                                RebuiltAt = now
                            };

                            db.BotFaqEmbeddings.Add(entity);
                            await db.SaveChangesAsync(stoppingToken);
                            await _logger.LogAsync("Information", "Embedding updated for FAQ", new { req.FaqId, Provider = req.Provider, Len = vec.Length });
                        }
                        catch (Exception ex)
                        {
                            await _logger.LogAsync("Error", "Failed to persist embedding update", new { req.FaqId, req.Provider }, ex);
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        try { await _logger.LogAsync("Error", "Embedding update worker failed", null, ex); } catch { }
                    }
                }
            }
            finally
            {
                try { await enumerator.DisposeAsync(); } catch { }
            }
        }
    }
}
