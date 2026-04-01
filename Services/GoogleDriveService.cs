using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public class GoogleDriveService : IDriveService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GoogleDriveService> _logger;

        private static readonly HashSet<string> AllowedMimeTypes = new()
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "video/mp4", "video/quicktime",
            "audio/mp4", "audio/m4a", "audio/mpeg",
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain"
        };

        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

        public GoogleDriveService(IConfiguration config, ILogger<GoogleDriveService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<DriveUploadResult> UploadAsync(IFormFile file, string groupId, string messageId, string messageType)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("file is empty");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException($"file too large: {file.Length}");

            var mimeType = file.ContentType ?? "application/octet-stream";
            if (!AllowedMimeTypes.Contains(mimeType))
                throw new InvalidOperationException($"unsupported mime type: {mimeType}");

            var driveService = BuildDriveService();

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var ext = Path.GetExtension(file.FileName);
            var safeFileName = $"{groupId}_{messageType}_{timestamp}_{messageId}{ext}";

            var folderId = _config["GOOGLE_DRIVE_FOLDER_ID"];
            if (string.IsNullOrWhiteSpace(folderId))
                throw new InvalidOperationException("GOOGLE_DRIVE_FOLDER_ID not set");

            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = safeFileName,
                Parents = new List<string> { folderId }
            };

            using var stream = file.OpenReadStream();
            var request = driveService.Files.Create(fileMetadata, stream, mimeType);
            request.Fields = "id, name, webViewLink, webContentLink";

            var progress = await request.UploadAsync();
            if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
            {
                _logger.LogError("Drive upload failed: {0}", progress.Exception?.Message);
                throw new Exception("Drive upload failed: " + progress.Exception?.Message);
            }

            var uploaded = request.ResponseBody;
            return new DriveUploadResult
            {
                FileId = uploaded.Id,
                FileUrl = uploaded.WebViewLink ?? uploaded.WebContentLink ?? string.Empty,
                FileName = uploaded.Name
            };
        }

        private DriveService BuildDriveService()
        {
            var base64Key = _config["GOOGLE_SERVICE_ACCOUNT_KEY"];
            if (string.IsNullOrWhiteSpace(base64Key))
                throw new InvalidOperationException("GOOGLE_SERVICE_ACCOUNT_KEY not set");

            var jsonBytes = Convert.FromBase64String(base64Key);
            using var ms = new MemoryStream(jsonBytes);
            var specificCredential = Google.Apis.Auth.OAuth2.CredentialFactory.FromStream<Google.Apis.Auth.OAuth2.ServiceAccountCredential>(ms);
            var googleCredential = specificCredential.ToGoogleCredential().CreateScoped(DriveService.Scope.DriveFile);
            return new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = googleCredential,
                ApplicationName = "ARCompletions"
            });
        }
    }
}
