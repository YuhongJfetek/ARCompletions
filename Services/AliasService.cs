using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    public class AliasService : IAliasService
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;

        public AliasService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<AliasMatchResult?> MatchAliasAsync(string normalizedText, ARCompletionsContext? db = null)
        {
            if (db == null)
            {
                using var _db = _dbFactory.CreateDbContext();
                var aliases = await _db.BotFaqAliases.AsNoTracking().Where(a => a.Enabled).ToListAsync();
                var alias = aliases.FirstOrDefault(a => string.Equals(Normalize(a.Term), normalizedText, StringComparison.OrdinalIgnoreCase));
                if (alias == null) return null;
                return new AliasMatchResult
                {
                    AliasTerm = alias.Term,
                    Mode = (alias.Mode ?? string.Empty).Trim().ToLowerInvariant(),
                    FaqIds = ParseIds(alias.FaqIds)
                };
            }

            var aliases2 = await db.BotFaqAliases.AsNoTracking().Where(a => a.Enabled).ToListAsync();
            var alias2 = aliases2.FirstOrDefault(a => string.Equals(Normalize(a.Term), normalizedText, StringComparison.OrdinalIgnoreCase));
            if (alias2 == null) return null;
            return new AliasMatchResult
            {
                AliasTerm = alias2.Term,
                Mode = (alias2.Mode ?? string.Empty).Trim().ToLowerInvariant(),
                FaqIds = ParseIds(alias2.FaqIds)
            };
        }

        private static string Normalize(string s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToLowerInvariant();
        private static string[] ParseIds(string? json) => string.IsNullOrWhiteSpace(json) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }
}
