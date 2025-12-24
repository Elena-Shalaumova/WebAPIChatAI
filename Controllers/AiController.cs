using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;          // ← добавь это
using WebAPIChatAI.Models;
using WebAPIChatAI.Services;
using WebAPIChatAI.Tables;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using MySqlConnector;
using System.Net.Sockets;
using System.Text;

namespace WebAPIChatAI.Controllers
{
    // DTO под ответ /api/tags от Ollama
    public class OllamaTagsResponse
    {
        public List<OllamaModelInfo> Models { get; set; } = new();
    }

    public class OllamaModelInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly ChatAIDB _context;
        private readonly OllamaClient _ollama;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;   // ← поле

        public AiController(
            ChatAIDB context,
            OllamaClient ollama,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)                 // ← параметр добавили
        {
            _context = context;
            _ollama = ollama;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("http://10.16.69.133:11434");
            //_httpClient.BaseAddress = new Uri("http://192.168.3.63:11434");

            _config = config;                      // ← теперь всё ок
        }

        // -------- DTO для запроса / ответа чата --------

        public class AiRequest
        {
            public int ChatId { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public class AiResponse
        {
            public string Answer { get; set; } = string.Empty;
        }

        // POST: /api/Ai/chat
        [HttpPost("chat")]
        public async Task<ActionResult<AiResponse>> Chat([FromBody] AiRequest request)
        {
            // 1. Проверяем, что чат существует
            var chat = await _context.Chats
                .FirstOrDefaultAsync(c => c.Id == request.ChatId);

            if (chat == null)
                return NotFound("Chat not found");

            // 2. Сообщение от пользователя
            var userMessage = new Message_Table
            {
                ChatId = request.ChatId,
                Text = request.Message,
                Role = 1,                 // 1 = user
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(userMessage);
            await _context.SaveChangesAsync();

            // 3. Ответ от модели через Ollama
            var aiText = await _ollama.GenerateAsync(request.Message);

            // 4. Сообщение от модели
            var aiMessage = new Message_Table
            {
                ChatId = request.ChatId,
                Text = aiText,
                Role = 0,                 // 0 = модель
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(aiMessage);
            await _context.SaveChangesAsync();

            // 5. Возвращаем только текст ответа
            var response = new AiResponse
            {
                Answer = aiText
            };

            return Ok(response);
        }

        // POST: /api/Ai/chat-stream — стримит частичный ответ Ollama клиенту
        [HttpPost("chat-stream")]
        public async Task ChatStream([FromBody] AiRequest request)
        {
            var cancellationToken = HttpContext.RequestAborted;

            var chat = await _context.Chats
                .FirstOrDefaultAsync(c => c.Id == request.ChatId, cancellationToken);

            if (chat == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                await Response.WriteAsync("data: Chat not found\n\n", cancellationToken);
                return;
            }

            var userMessage = new Message_Table
            {
                ChatId = request.ChatId,
                Text = request.Message,
                Role = 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(userMessage);
            await _context.SaveChangesAsync(cancellationToken);

            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.ContentType = "text/event-stream";

            var builder = new StringBuilder();

            await foreach (var chunk in _ollama.StreamGenerateAsync(request.Message, cancellationToken))
            {
                builder.Append(chunk);

                var payload = chunk.Replace("\n", "\\n");
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            var aiMessage = new Message_Table
            {
                ChatId = request.ChatId,
                Text = builder.ToString(),
                Role = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(aiMessage);
            await _context.SaveChangesAsync(cancellationToken);

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        // GET: /api/Ai/ollama-version
        [HttpGet("ollama-version")]
        public async Task<ActionResult<OllamaVersionDto>> GetOllamaVersion()
        {
            try
            {
                var resp = await _httpClient.GetFromJsonAsync<OllamaVersionDto>("/api/version");
                if (resp is null) return StatusCode(500, "Empty response from Ollama");
                return Ok(resp);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: /api/Ai/models  -> ходит в Ollama /api/tags и возвращает список имён моделей
        [HttpGet("models")]
        public async Task<ActionResult<IEnumerable<string>>> GetAvailableModels()
        {
            try
            {
                // дергаем Ollama напрямую
                var tags = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
                if (tags == null)
                    return StatusCode(500, "Empty response from Ollama");

                // берем только имена моделей
                var modelNames = tags.Models
                    .Select(m => m.Name)
                    .ToList();

                return Ok(modelNames);
            }
            catch (Exception ex)
            {
                return StatusCode(503, "Не удалось получить список моделей от Ollama: " + ex.Message);
            }
        }
    }
}