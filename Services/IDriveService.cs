using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public class DriveUploadResult
    {
        public string FileId { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public interface IDriveService
    {
        Task<DriveUploadResult> UploadAsync(IFormFile file, string groupId, string messageId, string messageType);
    }
}
