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

        public async Task LogRouteAsync(BotMessageRoute route, bool persist)
        {
            if (!persist) return;
            _db.BotMessageRoutes.Add(route);
            await _db.SaveChangesAsync();
        }
    }
}
