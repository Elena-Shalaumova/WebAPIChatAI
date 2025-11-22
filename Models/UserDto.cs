using Microsoft.EntityFrameworkCore.Query;

namespace WebAPIChatAI.Models
{
    public class UserDto
    {
        private DateTime created_At;

        public int ID { get; set; }
        public string Login { get; set; } = string.Empty;
        public DateTime Created_At { get => created_At; set => created_At = value; }

        public UserDto(int iD, string login, DateTime created_At)
        {
            this.ID = iD;
            this.Login = login;
            this.Created_At = created_At;
        
        }
    }
}
