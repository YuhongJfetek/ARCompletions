using System;
namespace ARCompletions.Services
{
    public interface IQueryHintsService
    {
        // Return an array of preferred category keys detected from the normalized query text
        string[]? DetectPreferredCategoryKeys(string normalizedText);
    }
}
