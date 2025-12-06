namespace WebAPIChatAI.Models
{
    public class SettingsDto
    {
        public int Id { get; set; }          // id строки в settings
        public int UserId { get; set; }      // пользователь
        public bool Stream { get; set; }     // включён ли стрим
        public string Model { get; set; } = string.Empty;

        public double? Temperature { get; set; }  // 🔥 темп
        public int? MaxTokens { get; set; }       // 📏 макс. токены
    }
}
