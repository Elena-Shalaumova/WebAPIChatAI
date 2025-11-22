using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPIChatAI.Tables
{
    [Table("users")]
    public class User_Table
    {
        [Key]
        [Column("id")]
        public int ID { get; set; }
        [Column("login")]
        public string Login { get; set; }
        [Column("password")]
        public string? Password { get; set; }

        [Column("password_hash")]
        public string? Password_Hash { get; set; }

        [Column("salt")]
        public string? Salt { get; set; }

        [Column("iterations")]
        public int? Iterations { get; set; }

        [Column("is_hashed")]
        public bool? Is_Hashed { get; set; } // 1 = хэширован, 0 = нет

        [Column("created_at")]
        public DateTime Created_At { get; set; } = DateTime.Now;
    }
}
