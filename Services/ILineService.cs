using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface ILineService
    {
        Task HandleEventAsync(string evType, string replyToken, string userId, string messageType, string text, string postbackData, string rawJson);
    }
}
