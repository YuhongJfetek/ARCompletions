using System;
using System.Threading.Tasks;
using ARCompletions.Data;

namespace ARCompletions.Services
{
    public class AliasMatchResult
    {
        public string? AliasTerm { get; set; }
        public string? Mode { get; set; }
        public string[] FaqIds { get; set; } = Array.Empty<string>();
    }

    public interface IAliasService
    {
        Task<AliasMatchResult?> MatchAliasAsync(string normalizedText, ARCompletionsContext? db = null);
    }
}
