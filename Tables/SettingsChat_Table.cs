using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPIChatAI.Tables
{
    [Table("settings_chat")]
    public class SettingsChat_Table
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // 🔗 связь с Chat_Table
        [Column("chatId")]
        public int ChatId { get; set; }

        [Column("model")]
        public string Model { get; set; } = "qwen3-vl:2b";

        [Column("Temperature")]
        public double? Temperature { get; set; }

        [Column("MaxTokens")]
        public int? MaxTokens { get; set; }

        // навигационное свойство (опционально, но полезно)
        public virtual Chat_Table? Chat { get; set; }
    }
}
