namespace WebAPIChatAI.Models
{
    public class LastMessageDto
    {
        public int ChatId { get; set; }
        public int MessageId { get; set; }

        public string Text { get; set; } = "";
        public int Role { get; set; }          // 0 = assistant, 1 = user
        public string Type { get; set; } = "text";
        public DateTime CreatedAt { get; set; }
    }
}
