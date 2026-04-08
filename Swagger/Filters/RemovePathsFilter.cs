using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ARCompletions.Swagger.Filters;

public class RemovePathsFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc?.Paths == null) return;

        var toRemove = swaggerDoc.Paths
            .Where(p => p.Key.StartsWith("/internal/v1/events", System.StringComparison.OrdinalIgnoreCase)
                     || p.Key.StartsWith("/internal/v1/routes", System.StringComparison.OrdinalIgnoreCase)
                     || p.Key.StartsWith("/internal/v1/llm-logs", System.StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            swaggerDoc.Paths.Remove(key);
        }
    }
}
