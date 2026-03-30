using System.Threading.Tasks;
using ARCompletions.Dtos;

namespace ARCompletions.Services
{
    // Simple stub implementation that returns a fallback response.
    public class FaqQueryServiceStub : IFaqQueryService
    {
        public Task<MessageAnalyzeResponseDto> AnalyzeAsync(MessageAnalyzeRequestDto req)
        {
            var resp = new MessageAnalyzeResponseDto
            {
                Success = true,
                TraceId = req.TraceId,
                Route = "fallback",
                BestScore = null,
                FeedbackEnabled = false,
            };
            return Task.FromResult(resp);
        }
    }
}
