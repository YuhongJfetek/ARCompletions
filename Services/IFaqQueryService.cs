using System.Threading.Tasks;
using ARCompletions.Dtos;

namespace ARCompletions.Services
{
    public interface IFaqQueryService
    {
        Task<MessageAnalyzeResponseDto> AnalyzeAsync(MessageAnalyzeRequestDto req);
    }
}
