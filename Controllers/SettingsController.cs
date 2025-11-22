using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPIChatAI.Models;

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
                return NotFound(new { error = "Settings not found" });

            return new SettingsDto
            {
                Id = settings.Id,
                UserId = settings.UserId,
                Stream = settings.Stream,
                Model = settings.Model
            };
        }

        // POST: api/Settings/save
        [HttpPost("save")]
        public async Task<ActionResult<SettingsDto>> SaveSettings([FromBody] SettingsRequest request)
        {
            // 1. Проверяем, что пользователь существует
            var user = await _context.users.FindAsync(request.Id);
            if (user == null)
                return NotFound(new { error = "User not found" });

            // 2. Ищем настройки для этого пользователя
            var settings = await _context.Settings
                .FirstOrDefaultAsync(s => s.UserId == request.Id);

            if (settings == null)
            {
                // создаём первую запись
                settings = new Settings_Table
                {
                    UserId = request.Id,
                    Stream = request.Stream,
                    Model = request.Model
                };
                _context.Settings.Add(settings);
            }
            else
            {
                // обновляем существующую
                settings.Stream = request.Stream;
                settings.Model = request.Model;
            }

            await _context.SaveChangesAsync();

            var dto = new SettingsDto
            {
                Id = settings.Id,
                UserId = settings.UserId,
                Stream = settings.Stream,
                Model = settings.Model
            };

            return Ok(dto);
        }
    }
}
