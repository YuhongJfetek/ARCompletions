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

        public async Task LogRouteAsync(BotMessageRoute route, bool persist)
        {
            if (!persist) return;
            using var db = _dbFactory.CreateDbContext();
            db.BotMessageRoutes.Add(route);
            await db.SaveChangesAsync();
        }
    }
}
