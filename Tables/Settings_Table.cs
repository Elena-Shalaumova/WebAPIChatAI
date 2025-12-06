using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace WebAPIChatAI.Tables
{
    [Table("settings")]
    public class Settings_Table
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("userId")]
        public int UserId { get; set; }

        // tinyint(1) в MySQL нормально мапится в bool
        [Column("stream")]
        public bool Stream { get; set; } = true;

        [Column("model")]
        public string Model { get; set; } = "qwen2.5";

        // связь с пользователем (не обязательно, но полезно)
        public virtual User_Table? User { get; set; }

        public double? Temperature { get; set; } 
        public int? MaxTokens { get; set; }
    }
}
