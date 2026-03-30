using System;
using System.Collections.Generic;

namespace ARCompletions.Dtos
{
    public class MessageResultRequestDto
    {
        // new: vendor id to associate results with a vendor
        public string? VendorId { get; set; }
        public string TraceId { get; set; } = "";

        public MessageContextDto MessageContext { get; set; } = new();
        public AnalysisResultDto Analysis { get; set; } = new();
        public NodeExecutionResultDto NodeResult { get; set; } = new();
        public MessageSideEffectsDto SideEffects { get; set; } = new();
    }

    public class MessageContextDto
    {
        public string? LineGroupId { get; set; }
        public string? LineUserId { get; set; }
        public string SourceType { get; set; } = "";
        public string MessageType { get; set; } = "";
        public string? UserMessage { get; set; }
        public DateTime MessageTimestamp { get; set; }
        public string Language { get; set; } = "zh";
        public string? SessionId { get; set; }
        public List<AttachmentDto> Attachments { get; set; } = new();
    }

    public class AnalysisResultDto
    {
        public string Route { get; set; } = "";
        public string? MatchedFaqId { get; set; }
        public decimal? BestScore { get; set; }
        public string? ReasonCode { get; set; }
        public List<FaqCandidateDto> TopCandidates { get; set; } = new();
        public string? PersonaApplied { get; set; }
    }

    public class NodeExecutionResultDto
    {
        public string ReplyStatus { get; set; } = "";     // success / failed / skipped
        public string? BotReply { get; set; }
        public bool FallbackUsed { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? BotResponseId { get; set; }
    }

    public class AttachmentDto
    {
        public string? Type { get; set; }
        public string? Url { get; set; }
        public string? Name { get; set; }
    }

    public class MessageSideEffectsDto
    {
        public FeedbackResultDto? Feedback { get; set; }
        public GroupRegisterResultDto? GroupRegister { get; set; }
        public FileValidationResultDto? FileValidation { get; set; }
        public FileArchiveResultDto? FileArchive { get; set; }
        public SummaryJobResultDto? SummaryJob { get; set; }
    }

    public class FeedbackResultDto
    {
        public string? FeedbackType { get; set; }   // helpful / not_helpful
        public string? Comment { get; set; }
    }

    public class GroupRegisterResultDto
    {
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string? Note { get; set; }
    }

    public class FileValidationResultDto
    {
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class FileArchiveResultDto
    {
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string? FileId { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class SummaryJobResultDto
    {
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string? JobId { get; set; }
        public string? ErrorCode { get; set; }
    }
}
