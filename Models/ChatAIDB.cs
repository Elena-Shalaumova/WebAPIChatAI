using Microsoft.EntityFrameworkCore;
using WebAPIChatAI.Tables;

namespace WebAPIChatAI.Models
{
    public class ChatAIDB : DbContext
    {
        public ChatAIDB(DbContextOptions<ChatAIDB> options) : base(options) 
        {

        }

        public DbSet<User_Table> users { get; set; }

        public DbSet<Chat_Table> Chats { get; set; }
        public DbSet<Message_Table> Messages { get; set; }

        public DbSet<Settings_Table> Settings { get; set; }
        
        // таблица для картинок
        public DbSet<Image_Table> Images { get; set; } = null!;

        public DbSet<SettingsChat_Table> SettingChats { get; set; }

    }

}
