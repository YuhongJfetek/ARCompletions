using System.Text.Json;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Data;

public static class BotJsonSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static async Task SeedAsync(ARCompletionsContext db, string dataRoot, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(dataRoot))
        {
            throw new DirectoryNotFoundException($"Data root not found: {dataRoot}");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await SeedFaqItemsAsync(db, dataRoot, cancellationToken);
        await SeedFaqAliasesAsync(db, dataRoot, cancellationToken);
        await SeedFaqEmbeddingsAsync(db, dataRoot, cancellationToken);
        await SeedStaffUsersAsync(db, dataRoot, cancellationToken);
        await SeedSystemPromptsAsync(db, dataRoot, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedFaqItemsAsync(ARCompletionsContext db, string dataRoot, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataRoot, "faq.json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var items = JsonSerializer.Deserialize<List<FaqJson>>(json, JsonOptions) ?? new List<FaqJson>();

        var existing = await db.BotFaqItems.AsTracking().ToDictionaryAsync(x => x.FaqId, cancellationToken);

        foreach (var src in items)
        {
            if (string.IsNullOrWhiteSpace(src.id))
            {
                continue;
            }

            if (!existing.TryGetValue(src.id, out var entity))
            {
                entity = new BotFaqItem
                {
                    FaqId = src.id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.BotFaqItems.Add(entity);
                existing[src.id] = entity;
            }

            entity.Question = src.question ?? string.Empty;
            entity.Answer = src.answer ?? string.Empty;
            entity.Category = src.category;
            entity.CategoryKey = src.categoryKey;
            entity.Subcategory = src.subcategory;
            entity.Keywords = SerializeJsonArray(src.keywords);
            entity.QueryExamples = SerializeJsonArray(src.queryExamples);
            entity.AliasTerms = SerializeJsonArray(src.aliasTerms);
            entity.Sources = SerializeJsonArray(src.sources);
            entity.NeedsHumanHandoff = src.needsHumanHandoff;
            entity.Enabled = src.enabled;
            entity.SearchTextCache = BuildSearchText(src);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedBy = "seed-json";
        }
    }

    private static async Task SeedFaqAliasesAsync(ARCompletionsContext db, string dataRoot, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataRoot, "faq-aliases.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var items = JsonSerializer.Deserialize<List<FaqAliasJson>>(json, JsonOptions) ?? new List<FaqAliasJson>();

        var existing = await db.BotFaqAliases.AsTracking().ToDictionaryAsync(x => x.Term, cancellationToken);

        foreach (var src in items)
        {
            if (string.IsNullOrWhiteSpace(src.term))
            {
                continue;
            }

            if (!existing.TryGetValue(src.term, out var entity))
            {
                entity = new BotFaqAlias
                {
                    Term = src.term,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.BotFaqAliases.Add(entity);
                existing[src.term] = entity;
            }

            entity.Mode = string.IsNullOrWhiteSpace(src.mode) ? "disambiguation" : src.mode;
            entity.Synonyms = SerializeJsonArray(src.synonyms);
            entity.FaqIds = SerializeJsonArray(src.faqIds);
            entity.Enabled = true;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task SeedFaqEmbeddingsAsync(ARCompletionsContext db, string dataRoot, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataRoot, "embeddings.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var items = JsonSerializer.Deserialize<List<EmbeddingJson>>(json, JsonOptions) ?? new List<EmbeddingJson>();

        var existing = await db.BotFaqEmbeddings.AsTracking().ToDictionaryAsync(x => x.FaqId, cancellationToken);

        foreach (var src in items)
        {
            if (string.IsNullOrWhiteSpace(src.id))
            {
                continue;
            }

            if (!existing.TryGetValue(src.id, out var entity))
            {
                entity = new BotFaqEmbedding
                {
                    FaqId = src.id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.BotFaqEmbeddings.Add(entity);
                existing[src.id] = entity;
            }

            entity.Question = src.question;
            entity.SearchText = src.text;
            entity.CategoryKey = src.categoryKey;
            entity.EmbeddingProvider = "local_hash";
            entity.EmbeddingModel = "legacy_hash64";
            entity.VectorDim = src.embedding?.Length ?? 0;
            entity.Embedding = src.embedding ?? Array.Empty<double>();
            entity.IsActive = true;
            entity.RebuiltAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task SeedStaffUsersAsync(ARCompletionsContext db, string dataRoot, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataRoot, "staff-users.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var items = JsonSerializer.Deserialize<List<StaffUserJson>>(json, JsonOptions) ?? new List<StaffUserJson>();

        var existing = await db.BotStaffUsers.AsTracking().ToDictionaryAsync(x => x.UserId, cancellationToken);

        foreach (var src in items)
        {
            if (string.IsNullOrWhiteSpace(src.userId))
            {
                continue;
            }

            if (!existing.TryGetValue(src.userId, out var entity))
            {
                entity = new BotStaffUser
                {
                    UserId = src.userId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.BotStaffUsers.Add(entity);
                existing[src.userId] = entity;
            }

            entity.Name = src.name;
            entity.Role = src.role;
            entity.Enabled = src.enabled;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedBy = "seed-json";
        }
    }

    private static async Task SeedSystemPromptsAsync(ARCompletionsContext db, string dataRoot, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataRoot, "system-prompts.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new Dictionary<string, string>();

        var existing = await db.BotSystemPrompts.AsTracking().ToDictionaryAsync(x => x.PromptKey, cancellationToken);

        foreach (var kvp in dict)
        {
            var key = kvp.Key;
            var text = kvp.Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!existing.TryGetValue(key, out var entity))
            {
                entity = new BotSystemPrompt
                {
                    PromptKey = key
                };
                db.BotSystemPrompts.Add(entity);
                existing[key] = entity;
            }

            entity.PromptText = text ?? string.Empty;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedBy = "seed-json";
        }
    }

    private static string? SerializeJsonArray(string[]? values)
    {
        if (values == null || values.Length == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static string? BuildSearchText(FaqJson src)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(src.question)) parts.Add(src.question);
        if (!string.IsNullOrWhiteSpace(src.answer)) parts.Add(src.answer);
        if (src.keywords != null) parts.AddRange(src.keywords);
        if (src.queryExamples != null) parts.AddRange(src.queryExamples);
        if (src.aliasTerms != null) parts.AddRange(src.aliasTerms);
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private sealed class FaqJson
    {
        public string id { get; set; } = string.Empty;
        public string? question { get; set; }
        public string? answer { get; set; }
        public string? category { get; set; }
        public string? categoryKey { get; set; }
        public string? subcategory { get; set; }
        public string[]? keywords { get; set; }
        public string[]? queryExamples { get; set; }
        public string[]? aliasTerms { get; set; }
        public string[]? sources { get; set; }
        public bool needsHumanHandoff { get; set; }
        public bool enabled { get; set; }
    }

    private sealed class FaqAliasJson
    {
        public string term { get; set; } = string.Empty;
        public string? mode { get; set; }
        public string[]? faqIds { get; set; }
        public string[]? synonyms { get; set; }
    }

    private sealed class EmbeddingJson
    {
        public string id { get; set; } = string.Empty;
        public string? categoryKey { get; set; }
        public string? question { get; set; }
        public string? text { get; set; }
        public double[]? embedding { get; set; }
    }

    private sealed class StaffUserJson
    {
        public string userId { get; set; } = string.Empty;
        public string? name { get; set; }
        public string? role { get; set; }
        public bool enabled { get; set; }
    }
}
