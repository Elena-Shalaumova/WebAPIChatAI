namespace WebAPIChatAI.Models
{
    public class MessageDto
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int Role { get; set; }

        public string Text { get; set; } = string.Empty;  // всегда не null
        public string Type { get; set; } = "text";        // дефолт "text"

        public List<string> Images { get; set; } = new(); // всегда есть список (может быть пустой, но не null)

        public DateTime CreatedAt { get; set; }
    }

}
