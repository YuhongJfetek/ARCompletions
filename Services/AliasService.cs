using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    public class AliasService : IAliasService
    {
        private readonly ARCompletionsContext _db;

        public AliasService(ARCompletionsContext db)
        {
            _db = db;
        }

        public async Task<AliasMatchResult?> MatchAliasAsync(string normalizedText)
        {
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

        private static string Normalize(string s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToLowerInvariant();
        private static string[] ParseIds(string? json) => string.IsNullOrWhiteSpace(json) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }
}
