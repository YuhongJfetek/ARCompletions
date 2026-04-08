using System;
using System.Threading.Tasks;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public interface IRouteLoggingService
    {
        Task LogRouteAsync(BotMessageRoute route, bool persist);
    }
}
