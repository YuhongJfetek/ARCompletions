namespace ARCompletions.Dto
{
    public class ChatMessageRequestDto
    {
        public string userId { get; set; }
        public string role { get; set; } // "user" | "ai" | "system"
        public string content { get; set; }
        public string meta { get; set; }
    }
}
