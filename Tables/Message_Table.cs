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
        [Column("Role")]
        public int Role { get; set; }

        [Column("text")]
        public string Text { get; set; } = "";

        // тип сообщения — text или image
        [Column("type")]
        public string Type { get; set; } = "text";

        // картинка в бинарном виде (MEDIUMBLOB)
        [Column("imageBlob")]
        public byte[]? ImageBlob { get; set; }

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигация к чату
        public virtual Chat_Table Chat { get; set; } = null!;
    }

}
