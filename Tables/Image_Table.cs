using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPIChatAI.Tables
{
    [Table("images")]   
    public class Image_Table
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // внешний ключ на messages.id
        [Column("messageId")]
        public int MessageId { get; set; }

        [Column("imageBlob")]
        public byte[] ImageBlob { get; set; } = null!;

        // навигация обратно к сообщению
        public virtual Message_Table Message { get; set; } = null!;
    }
}
