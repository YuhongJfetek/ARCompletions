namespace ARCompletions.Dto;

public class InternalBotResponseDto
{
    public string Action { get; set; } = "none"; // e.g. 'faq', 'route', 'handoff', 'none'
    public string? Message { get; set; }
    public string? MatchedFaqId { get; set; }
    public double? Score { get; set; }
    public bool NeedsHandoff { get; set; }
    public string? Route { get; set; }
}
