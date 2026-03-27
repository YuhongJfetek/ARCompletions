using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("api/linebot")]
public class LineBotController : ControllerBase
{
    private readonly ARCompletionsContext _db;

    public LineBotController(ARCompletionsContext db)
    {
        _db = db;
    }

    // 1. 接收 Line Bot 輸入資料
    [HttpPost("input")]
    public async Task<IActionResult> Input([FromBody] LineBotInputRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }

        if (string.IsNullOrWhiteSpace(request.VendorId))
        {
            return BadRequest(new { success = false, error = new { code = "MissingVendorId", message = "缺少 VendorId" } });
        }

        var vendor = await _db.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId && v.IsActive);
        if (vendor == null)
        {
            return BadRequest(new { success = false, error = new { code = "InvalidVendor", message = "找不到對應的廠商或已停用" } });
        }

        var receivedAtUnix = request.ReceivedAt == default
            ? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            : new DateTimeOffset(request.ReceivedAt, TimeSpan.Zero).ToUnixTimeSeconds();

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.VendorId == vendor.Id
                                       && c.Platform == request.Platform
                                       && c.Channel == request.Channel
                                       && c.ExternalUserId == request.ExternalUserId
                                       && c.Status != "closed");

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorId = vendor.Id,
                Platform = request.Platform,
                Channel = request.Channel,
                ExternalUserId = request.ExternalUserId,
                DisplayName = request.DisplayName,
                StartedAt = receivedAtUnix,
                EndedAt = null,
                MessageCount = 0,
                LastMessageAt = receivedAtUnix,
                Status = "open",
                CreatedAt = nowUnix,
                UpdatedAt = nowUnix
            };
            _db.Conversations.Add(conversation);
        }
        else
        {
            conversation.DisplayName ??= request.DisplayName;
            conversation.LastMessageAt = receivedAtUnix;
            conversation.UpdatedAt = nowUnix;
        }

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversation.Id,
            VendorId = vendor.Id,
            Direction = "in",
            MessageType = request.MessageType,
            MessageText = request.MessageText,
            RawJson = request.RawJson,
            ExternalMessageId = request.ExternalMessageId,
            ReplyToken = request.ReplyToken,
            SenderId = request.ExternalUserId,
            SenderName = request.DisplayName,
            CreatedAt = receivedAtUnix
        };

        var inputLog = new LineBotInputLog
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendor.Id,
            ConversationId = conversation.Id,
            ExternalUserId = request.ExternalUserId,
            DisplayName = request.DisplayName,
            MessageType = request.MessageType,
            MessageText = request.MessageText,
            ExternalMessageId = request.ExternalMessageId,
            ReplyToken = request.ReplyToken,
            RawJson = request.RawJson,
            ReceivedAt = receivedAtUnix,
            CreatedAt = nowUnix
        };

        conversation.MessageCount += 1;

        _db.ConversationMessages.Add(message);
        _db.LineBotInputLogs.Add(inputLog);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            conversationId = conversation.Id,
            messageId = message.Id
        });
    }

    // 2. 接收 Line Bot 回覆資料
    [HttpPost("output")]
    public async Task<IActionResult> Output([FromBody] LineBotOutputRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }

        if (string.IsNullOrWhiteSpace(request.VendorId))
        {
            return BadRequest(new { success = false, error = new { code = "MissingVendorId", message = "缺少 VendorId" } });
        }

        var vendor = await _db.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId && v.IsActive);
        if (vendor == null)
        {
            return BadRequest(new { success = false, error = new { code = "InvalidVendor", message = "找不到對應的廠商或已停用" } });
        }

        Conversation? conversation = null;

        if (!string.IsNullOrWhiteSpace(request.ConversationId))
        {
            conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.VendorId == vendor.Id);
        }

        if (conversation == null)
        {
            conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.VendorId == vendor.Id
                                           && c.ExternalUserId == request.ExternalUserId
                                           && c.Status != "closed");
        }

        var sentAtUnix = request.SentAt == default
            ? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            : new DateTimeOffset(request.SentAt, TimeSpan.Zero).ToUnixTimeSeconds();

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorId = vendor.Id,
                Platform = request.Platform ?? "LINE",
                Channel = request.Channel ?? "linebot",
                ExternalUserId = request.ExternalUserId,
                DisplayName = null,
                StartedAt = sentAtUnix,
                EndedAt = null,
                MessageCount = 0,
                LastMessageAt = sentAtUnix,
                Status = "open",
                CreatedAt = nowUnix,
                UpdatedAt = nowUnix
            };
            _db.Conversations.Add(conversation);
        }
        else
        {
            conversation.LastMessageAt = sentAtUnix;
            conversation.UpdatedAt = nowUnix;
        }

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversation.Id,
            VendorId = vendor.Id,
            Direction = "out",
            MessageType = request.MessageType,
            MessageText = request.MessageText,
            RawJson = request.RawJson,
            ExternalMessageId = null,
            ReplyToken = null,
            SenderId = null,
            SenderName = null,
            SourceFaqId = request.SourceFaqId,
            SourceType = request.SourceType,
            ConfidenceScore = request.ConfidenceScore,
            ModelName = request.ModelName,
            PromptVersion = request.PromptVersion,
            CreatedAt = sentAtUnix
        };

        var outputLog = new LineBotOutputLog
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendor.Id,
            ConversationId = conversation.Id,
            ExternalUserId = request.ExternalUserId,
            MessageType = request.MessageType,
            MessageText = request.MessageText,
            SourceType = request.SourceType,
            SourceFaqId = request.SourceFaqId,
            ConfidenceScore = request.ConfidenceScore,
            ModelName = request.ModelName,
            PromptVersion = request.PromptVersion,
            RawJson = request.RawJson,
            SentAt = sentAtUnix,
            CreatedAt = nowUnix
        };

        conversation.MessageCount += 1;

        _db.ConversationMessages.Add(message);
        _db.LineBotOutputLogs.Add(outputLog);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            conversationId = conversation.Id,
            messageId = message.Id
        });
    }
}

public class LineBotInputRequest
{
    public string VendorId { get; set; } = string.Empty;
    public string Platform { get; set; } = "LINE";
    public string Channel { get; set; } = "linebot";
    public string ExternalUserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? MessageText { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? ReplyToken { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}

public class LineBotOutputRequest
{
    public string VendorId { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Channel { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? MessageText { get; set; }
    public string? SourceType { get; set; }
    public string? SourceFaqId { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ModelName { get; set; }
    public string? PromptVersion { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
