namespace WebAPIChatAI.Models.Dtos
{
    public class SettingsRequest
    {
        public int Id { get; set; }          // userId
        public bool Stream { get; set; }     // true/false
        public string Model { get; set; }    // название AI модели
    }
}
