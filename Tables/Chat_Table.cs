using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPIChatAI.Tables
{
    [Table("chats")]
    public class Chat_Table
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("userid")]
        public int UserId { get; set; }
        [Column("title")]
        public string Title { get; set; } = "";
        [Column("createdAt")]
        public DateTime CreatedAt { get; set; }
        [Column("IsIncognito")]
        public bool IsIncognito { get; set; } = false;

        public virtual User_Table User { get; set; } = null!;
        public virtual List<Message_Table> Messages { get; set; } = new();
    }
}
