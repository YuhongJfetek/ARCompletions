using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services;

public class EmbeddingUpdateBackgroundService : BackgroundService
{
    private readonly IEmbeddingUpdateQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddingUpdateBackgroundService> _logger;
    private readonly int _maxConcurrency;
    private readonly SemaphoreSlim _semaphore;

        public EmbeddingUpdateBackgroundService(
        IEmbeddingUpdateQueue queue,
            IServiceScopeFactory scopeFactory,
        ILogger<EmbeddingUpdateBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxConcurrency = int.TryParse(
            Environment.GetEnvironmentVariable("EMBEDDING_UPDATE_WORKER_CONCURRENCY"),
            out var c) && c > 0 ? c : 4;
        _logger.LogInformation("EmbeddingUpdateBackgroundService constructed (maxConcurrency={Max})", _maxConcurrency);
        _semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmbeddingUpdateBackgroundService ExecuteAsync starting");
        try
        {
            if (_queue is not EmbeddingUpdateQueue concrete)
            {
                _logger.LogWarning("EmbeddingUpdateBackgroundService: queue 不支援列舉，服務待機中");
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
                return;
            }

            var running = new System.Collections.Generic.List<Task>();

            // ↓ 關鍵修正：把 stoppingToken 傳進 ReadAllAsync
            try
            {
                await foreach (var item in concrete.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    var task = Task.Run(() => ProcessItemAsync(item, stoppingToken), stoppingToken);
                    running.Add(task);

                    if (running.Count > _maxConcurrency * 4)
                        running.RemoveAll(t => t.IsCompleted);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常關機時 Channel 可能在讀取時被取消，視為正常情況
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingUpdateBackgroundService: 列舉隊列時發生未處理的例外");
            }

            // 等待剩餘任務完成
            if (running.Count > 0)
            {
                try { await Task.WhenAll(running).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogWarning(ex, "關機時等待 embedding 任務發生錯誤"); }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常關機，忽略
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmbeddingUpdateBackgroundService 發生嚴重錯誤");
        }
    }

    private async Task ProcessItemAsync(EmbeddingUpdateRequest item, CancellationToken ct)
    {
        try
        {
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            using var scope = _scopeFactory.CreateScope();

            var textProcessing = scope.ServiceProvider.GetRequiredService<ITextProcessingService>();
            var retrieval = scope.ServiceProvider.GetRequiredService<IEmbeddingRetrievalService>();
            var dbFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext>>();
            using var db = dbFactory.CreateDbContext();

            var normalized = textProcessing.Normalize(item.Text) ?? item.Text;
            var model = string.IsNullOrWhiteSpace(item.Model) ? "legacy_hash64" : item.Model;

            // 去重：同 faq/provider/model/searchtext 已有 active embedding 則跳過
            try
            {
                var exists = await db.BotFaqEmbeddings.AsNoTracking()
                    .Where(e => e.FaqId == item.FaqId
                             && e.EmbeddingProvider == item.Provider
                             && e.EmbeddingModel == model
                             && e.IsActive
                             && e.SearchText == normalized)
                    .OrderByDescending(e => e.RebuiltAt)
                    .FirstOrDefaultAsync(ct).ConfigureAwait(false);

                if (exists != null)
                {
                    _logger.LogInformation("跳過 embedding 重建（已是最新）: {FaqId} {Provider} {Model}",
                        item.FaqId, item.Provider, model);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "去重檢查失敗");
            }

            // 取得向量
            double[]? vec = null;
            try
            {
                vec = await retrieval.GetOrCreateEmbeddingAsync(normalized, model, item.Provider, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "取得 embedding 失敗: {FaqId}", item.FaqId);
            }

            if (vec == null || vec.Length == 0)
            {
                _logger.LogInformation("Embedding 回傳為空: {FaqId}", item.FaqId);
                return;
            }

            // 寫入 DB
            try
            {
                var now = DateTimeOffset.UtcNow;
                await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

                var prev = await db.BotFaqEmbeddings
                    .Where(e => e.FaqId == item.FaqId
                             && e.EmbeddingProvider == item.Provider
                             && e.EmbeddingModel == model
                             && e.IsActive)
                    .ToListAsync(ct).ConfigureAwait(false);

                if (prev.Count > 0)
                {
                    foreach (var p in prev) p.IsActive = false;
                    db.BotFaqEmbeddings.UpdateRange(prev);
                }

                await db.BotFaqEmbeddings.AddAsync(new BotFaqEmbedding
                {
                    EmbeddingId = Guid.NewGuid(),
                    FaqId = item.FaqId,
                    Question = null,
                    SearchText = normalized,
                    EmbeddingProvider = item.Provider,
                    EmbeddingModel = model,
                    VectorDim = vec.Length,
                    Embedding = vec,
                    IsActive = true,
                    RebuiltAt = now,
                    CreatedAt = now
                }, ct).ConfigureAwait(false);

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);

                _logger.LogInformation("Embedding 已儲存: {FaqId} ({Provider}/{Model})",
                    item.FaqId, item.Provider, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存 embedding 失敗: {FaqId}", item.FaqId);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常關機
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessItemAsync 未預期錯誤");
        }
        finally
        {
            try { _semaphore.Release(); } catch { }
        }
    }
}
