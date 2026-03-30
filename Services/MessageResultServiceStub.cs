using System.Threading.Tasks;
using ARCompletions.Dtos;

namespace ARCompletions.Services
{
    // Stub that simulates persisting result without touching DB.
    public class MessageResultServiceStub : IMessageResultService
    {
        public Task<MessageResultResponseDto> PersistResultAsync(MessageResultRequestDto req)
        {
            var resp = new MessageResultResponseDto
            {
                Success = true,
                TraceId = req.TraceId,
                ConversationLogId = null,
                FaqQueryLogId = null,
                Saved = new SaveStateDto
                {
                    ConversationLog = false,
                    FaqQueryLog = false,
                    GroupState = false,
                    FileState = false,
                    Feedback = false,
                    AuditLog = false
                }
            };
            return Task.FromResult(resp);
        }
    }
}
