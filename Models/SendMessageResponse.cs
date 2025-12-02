namespace WebAPIChatAI.Models
{
    public class SendMessageResponse
    {
        public MessageDto UserMessage { get; set; } = null!;
        public MessageDto AiMessage { get; set; } = null!;

    }
}