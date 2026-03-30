using System.Threading.Tasks;
using ARCompletions.Dtos;

namespace ARCompletions.Services
{
    public interface IMessageResultService
    {
        Task<MessageResultResponseDto> PersistResultAsync(MessageResultRequestDto req);
        Task<MessageResultResponseDto> PersistResultWithRouteAsync(MessageResultRequestDto req, ARCompletions.Dtos.MessageRouteCreateDto routeReq);
    }
}
