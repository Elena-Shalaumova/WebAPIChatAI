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

        public ChatDto() { }

        public ChatDto(int id, int userId, string title, string? model, bool isIncognito)
        {
            Id = id;
            UserId = userId;
            Title = title;
            Model = model;
            IsIncognito = isIncognito;
        }

        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public string? Model { get; set; }
        // public DateTime? LastMessageAt { get; set; }
        public bool IsIncognito { get; set; }
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

