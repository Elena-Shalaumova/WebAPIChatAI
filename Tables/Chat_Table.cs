namespace WebAPIChatAI.Tables
{
    public class Chat_Table
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public virtual User_Table User { get; set; } = null!;
        public virtual List<Message_Table> Messages { get; set; } = new();
        public bool IsIncognito { get; set; } = false;
    }
}
