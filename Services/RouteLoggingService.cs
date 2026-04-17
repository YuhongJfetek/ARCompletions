using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class RouteLoggingService : IRouteLoggingService
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;

        public RouteLoggingService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task LogRouteAsync(BotMessageRoute route, bool persist, ARCompletionsContext? db = null)
        {
            if (!persist) return;
            if (db != null)
            {
                db.BotMessageRoutes.Add(route);
                return;
            }
            using var _db = _dbFactory.CreateDbContext();
            _db.BotMessageRoutes.Add(route);
            await _db.SaveChangesAsync();
        }
    }
}
