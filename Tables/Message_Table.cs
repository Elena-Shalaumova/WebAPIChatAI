using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPIChatAI.Tables
{
    [Table("messages")]
    public class Message_Table
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("chatId")]
        public int ChatId { get; set; }

        // 0 – assistant (модель), 1 – user
        [Column("role")]
        public int Role { get; set; }

        [Column("text")]
        public string Text { get; set; } = "";

        // тип сообщения — text или image
        [Column("type")]
        public string Type { get; set; } = "text";

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигация к чату
        public virtual Chat_Table Chat { get; set; } = null!;

        // 🔥 Новая навигация: список картинок у сообщения
        public virtual List<Image_Table> Images { get; set; } = new();
    }

}
