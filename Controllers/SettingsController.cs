using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPIChatAI.Models;
using WebAPIChatAI.Models.Dtos;
using WebAPIChatAI.Tables;



namespace WebAPIChatAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ChatAIDB _context;

        public SettingsController(ChatAIDB context)
        {
            _context = context;
        }

        // GET: api/Settings/{userId}
        [HttpGet("{userId}")]
        public async Task<ActionResult<SettingsDto>> GetSettings(int userId)
        {
            var settings = await _context.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings == null)
                return NotFound();

            return new SettingsDto
            {
                Id = settings.Id,
                UserId = settings.UserId,
                Stream = settings.Stream,
                Model = settings.Model,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens
            };
        }

        // POST: api/Settings/save
        [HttpPost("save")]
        public async Task<ActionResult<SettingsDto>> SaveSettings([FromBody] SettingsRequest request)
        {
            // Проверяем пользователя
            var user = await _context.users.FindAsync(request.Id);
            if (user == null)
                return NotFound(new { error = "User not found" });

            // Ищем настройки
            var settings = await _context.Settings
                .FirstOrDefaultAsync(s => s.UserId == request.Id);

            if (settings == null)
            {
                settings = new Settings_Table
                {
                    UserId = request.Id,
                    Stream = request.Stream,
                    Model = request.Model,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens
                };

                _context.Settings.Add(settings);
            }
            else
            {
                settings.Stream = request.Stream;
                settings.Model = request.Model;
                settings.Temperature = request.Temperature;
                settings.MaxTokens = request.MaxTokens;
            }

            await _context.SaveChangesAsync();

            return Ok(new SettingsDto
            {
                Id = settings.Id,
                UserId = settings.UserId,
                Stream = settings.Stream,
                Model = settings.Model,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens
            });
        }
    }
}
