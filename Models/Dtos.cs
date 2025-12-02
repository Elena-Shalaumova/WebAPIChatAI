using static System.Net.Mime.MediaTypeNames;
using System.Data;

namespace WebAPIChatAI.Models
{
    public class OllamaVersionDto
    {
        public string Version { get; set; } = string.Empty;
    }

    public class ChatDto
    {
        public ChatDto(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
    }
  
    // Твои DTO настроек (оставляю как есть, только без вложенного неймспейса дублирующего)
    // То, что приходит с клиента (Android)
    public class SettingsRequest
    {
        // Это userId. В Kotlin ты шлёшь поле "id", так что оставим Id.
        public int Id { get; set; }
        public bool Stream { get; set; }
        public string Model { get; set; } = string.Empty;
    }

    // То, что отдаём обратно
    public class SettingsDto
    {
        public int Id { get; set; }          // id строки в settings
        public int UserId { get; set; }      // пользователь
        public bool Stream { get; set; }
        public string Model { get; set; } = string.Empty;
    }

    public class OllamaTagsResponse
    {
        public List<OllamaModelInfo> Models { get; set; } = new();
    }

    public class OllamaModelInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}

