using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class RouteLoggingService : IRouteLoggingService
    {
        private readonly ARCompletionsContext _db;

        public RouteLoggingService(ARCompletionsContext db)
        {
            _db = db;
        }

        public Task LogRouteAsync(BotMessageRoute route, bool persist)
        {
            if (!persist) return Task.CompletedTask;
            // Add to context; do not SaveChanges here to allow callers to batch commits.
            _db.BotMessageRoutes.Add(route);
            return Task.CompletedTask;
        }
    }
}
