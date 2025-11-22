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

    // DTO одного сообщения
    public class MessageDto
    {
        public MessageDto(int id, string text, int role)
        {
            Id = id;
            Text = text;
            Role = role;
        }

        public MessageDto(int id, int chatId, string text, int role)
        {
            Id = id;
            ChatId = chatId;
            Text = text;
            Role = role;
        }

        public MessageDto()
        {
        }

        public int Id { get; set; }
        public int ChatId { get; set; }

        /// <summary>
        /// Сам текст сообщения (если это текст).
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 1 – пользователь, 0 – модель
        /// </summary>
        public int Role { get; set; }

        /// <summary>
        /// NEW: тип сообщения: "text" или "image".
        /// Для существующего кода по умолчанию будет "text".
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>
        /// NEW: картинка в Base64, если Type == "image".
        /// Для текстовых сообщений — null.
        /// </summary>
        public string? Base64Image { get; set; }

        /// <summary>
        /// NEW: время создания сообщения.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    //public class SendMessageResponse
    //{
    //    public MessageDto UserMessage { get; set; } = default!;
    //    public MessageDto AiMessage { get; set; } = default!;
    //}

    ///// <summary>
    ///// NEW: запрос на отправку сообщения с картинкой с клиента (Android).
    ///// </summary>
    //public class SendMessageRequest
    //{
    //    public int ChatId { get; set; }
    //    public int UserId { get; set; }

    //    /// <summary>
    //    /// Текстовый промпт вокруг картинки (например: "Что на картинке?").
    //    /// Может быть null.
    //    /// </summary>
    //    public string? Prompt { get; set; }

    //    /// <summary>
    //    /// Картинка в Base64 без префикса "data:image/jpeg;base64,".
    //    /// </summary>
    //    public string Base64Image { get; set; } = string.Empty;
    //}

    public class SendMessageRequest
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }          // кто отправляет
        public string? Text { get; set; }        // текст или подпись к картинке
        public string? Base64Image { get; set; } // null – если сообщение текстовое
    }

    public class SendMessageResponse
    {
        public MessageDto UserMessage { get; set; } = null!;
        public MessageDto AiMessage { get; set; } = null!;
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

