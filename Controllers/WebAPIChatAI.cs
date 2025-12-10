using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;              // PasswordHasher
using WebAPIChatAI.Models;                        // RegisterDto, LoginDto, LastMessageDto
using WebAPIChatAI.Tables;                        // User_Table
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;


namespace WebAPIChatAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebAPIChatAI : ControllerBase
    {
        private readonly ChatAIDB _context;
        private readonly PasswordHasher<object> _hasher = new();
        private readonly HttpClient _ollamaHttp;

        // флаг для клиента: изменилась ли модель при логине
        private bool _modelChanged;
        private string? _currentModelName;


        public WebAPIChatAI(ChatAIDB context, IHttpClientFactory httpClientFactory)
        {
            _context = context;

            _ollamaHttp = httpClientFactory.CreateClient();
            _ollamaHttp.BaseAddress = new Uri("http://ollama:11434"); // где крутится Ollama
        }

        // =========================
        //        USERS: READ
        // =========================
        [HttpGet("GetAllUsers")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            // На всякий случай не отдаём открытые пароли
            // (если свойство Password ещё есть в модели)

            var list = await _context.users
                .AsNoTracking()
                .Select(u => new UserDto(u.ID, u.Login, u.Created_At))
                .ToListAsync();

            return list;
        }

        // =========================
        //        REGISTER
        // =========================
        // Было: [HttpPost("AddUser")] с User_Table
        // Стало: принимаем RegisterDto (login + password), хэшируем, сохраняем
        [HttpPost("AddUser")]
        public async Task<IActionResult> AddUser([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Login and password required" });

            var exists = await _context.users.AnyAsync(u => u.Login == dto.Login);
            if (exists)
                return Conflict(new { error = "User already exists" });

            var hash = _hasher.HashPassword(null, dto.Password);

            var user = new User_Table
            {
                Login = dto.Login,
                Password = null,       // чистый пароль не храним
                Password_Hash = hash,  // только хэш
                Is_Hashed = true,
                Created_At = DateTime.UtcNow
            };

            // 1️⃣ — создаём пользователя
            _context.users.Add(user);
            await _context.SaveChangesAsync();  // тут появляется user.ID

            // 2️⃣ — создаём настройки по умолчанию
            // перед проверкой настроек сбрасываем флаг
            _modelChanged = false;
            _currentModelName = null;

            // 🔹 ВАЖНО: гарантируем валидные настройки перед возвратом OK
            await EnsureValidSettingsAsync(user.ID);

            return Ok(new
            {
                id = user.ID,
                login = user.Login,
                createdAt = user.Created_At,
                modelChanged = _modelChanged,
                model = _currentModelName
            });
        }


        // =========================
        //          LOGIN
        // =========================
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Login == dto.Login);

            if (user is null || string.IsNullOrEmpty(user.Password_Hash))
                return Unauthorized(new { error = "Invalid login or password" });

            var result = _hasher.VerifyHashedPassword(null, user.Password_Hash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { error = "Invalid login or password" });

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var tracked = await _context.users.SingleAsync(u => u.ID == user.ID);
                tracked.Password_Hash = _hasher.HashPassword(null, dto.Password);
                await _context.SaveChangesAsync();
            }

            // перед проверкой настроек сбрасываем флаг
            _modelChanged = false;
            _currentModelName = null;

            // 🔹 ВАЖНО: гарантируем валидные настройки перед возвратом OK
            await EnsureValidSettingsAsync(user.ID);

            return Ok(new
            {
                id = user.ID,
                login = user.Login,
                createdAt = user.Created_At,
                modelChanged = _modelChanged,
                model = _currentModelName
            });

        }


        // =========================
        //         UPDATE
        // =========================
        [HttpPut("UpdateUser/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] User_Table user)
        {
            if (id != user.ID) return BadRequest();

            // Запрещаем случайно перезатереть хэш открытым паролем
            _context.Entry(user).Property(x => x.Password).IsModified = false;

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                var exists = await _context.users.AnyAsync(u => u.ID == id);
                if (!exists) return NotFound();
                throw;
            }

            return NoContent();
        }

        // =========================
        //         DELETE
        // =========================
        [HttpDelete("DeleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.users.FindAsync(id);
            if (user == null) return NotFound();

            _context.users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================
        //   LAST MESSAGES BY USER
        // =========================
        // GET: api/WebAPIChatAI/user/5/last-messages
        [HttpGet("user/{userId}/last-messages")]
        public async Task<IActionResult> GetLastMessagesForUser(int userId)
        {
            // Берём все сообщения, у которых чат принадлежит нужному пользователю,
            // группируем по chatId и берём из каждой группы самое "свежее"
            var lastMessages = await _context.Messages
                .Where(m => m.Chat.UserId == userId)
                .GroupBy(m => m.ChatId)
                .Select(g => g
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new LastMessageDto
                    {
                        ChatId = m.ChatId,
                        MessageId = m.Id,
                        Text = m.Text,
                        Role = m.Role,
                        Type = m.Type,
                        CreatedAt = m.CreatedAt
                    })
                    .FirstOrDefault()
                )
                .ToListAsync();

            return Ok(lastMessages);
        }

        // DTO под ответ /api/tags от Ollama
        public class OllamaTagsResponse
        {
            public List<OllamaModelInfo> Models { get; set; } = new();
        }

        public class OllamaModelInfo
        {
            public string Name { get; set; } = string.Empty;
        }

        // Проверка/создание настроек при входе
        private async Task<IActionResult> EnsureValidSettingsAsync(int userId)
        {
            // 1. Тянем список моделей из Ollama
            List<string> availableModels;
            try
            {
                var tags = await _ollamaHttp.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
                availableModels = tags?.Models.Select(m => m.Name).ToList() ?? new List<string>();
            }
            catch
            {
                // если Ollama недоступна — просто выходим, настройки не трогаем
                return StatusCode(503, "Ollama недоступна");
            }

            if (!availableModels.Any())
                return BadRequest("Нет доступных моделей");

            var defaultModel = availableModels.First();   // возьмём первую доступную модель

            // 2. Ищем текущие настройки пользователя
            var settings = await _context.Settings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings == null)
            {
                // Настроек нет — создаём с дефолтами
                settings = new Settings_Table
                {
                    UserId = userId,
                    Stream = true,           // по умолчанию включено
                    Model = defaultModel
                };

                _context.Settings.Add(settings);

                // 👉 модель установлена впервые — считаем, что "автоматически выбрана"
                _modelChanged = true;
                _currentModelName = defaultModel;
            }
            else
            {
                // Настройки есть — проверяем, валидна ли модель
                if (string.IsNullOrWhiteSpace(settings.Model) ||
                    !availableModels.Contains(settings.Model))
                {
                    // если модель не найдена в /api/tags → ставим рабочую
                    settings.Model = defaultModel;

                    // 👉 модель была невалидна и заменена — автосмена
                    _modelChanged = true;
                    _currentModelName = defaultModel;
                }
                else
                {
                    // 👉 модель валидная, не меняли — просто запомнили имя
                    _modelChanged = false;
                    //_currentModelName = settings.Model;
                    _currentModelName = null;

                }
            }

            await _context.SaveChangesAsync();

            var user = await _context.users
                .AsNoTracking()
                .Where(u => u.ID == userId)
                .Select(u => new UserDto(u.ID, u.Login, u.Created_At))
                .FirstOrDefaultAsync();

            return Ok(new
            {
                id = user.ID,
                login = user.Login,
                createdAt = user.Created_At,
                modelChanged = _modelChanged,
                model = _currentModelName
            });
        }

    }
}















































