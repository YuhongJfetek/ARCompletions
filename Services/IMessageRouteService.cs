using System.Threading.Tasks;
using ARCompletions.Dtos;

namespace ARCompletions.Services
{
    public interface IMessageRouteService
    {
        Task<MessageRouteResponseDto> PersistRouteAsync(MessageRouteCreateDto req);
    }
}
