namespace ARCompletions.Dto
{
    public class ChatMessageRequestDto
    {
        public string userId { get; set; } = string.Empty;
        public string role { get; set; } = "user"; // "user" | "ai" | "system"
        public string content { get; set; } = string.Empty;
        public string meta { get; set; } = string.Empty;
    }
}
