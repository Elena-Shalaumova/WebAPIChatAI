using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPIChatAI.Models;   // MessageDto, SendMessageResponse, MessageMapper (ToDto)
using WebAPIChatAI.Tables;   // Chat_Table, Message_Table, Image_Table
using WebAPIChatAI.Services; // OllamaClient

namespace WebAPIChatAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatAIDB _context;
        private readonly OllamaClient _ollama;

        public ChatController(ChatAIDB context, OllamaClient ollama)
        {
            _context = context;
            _ollama = ollama;
        }

        // --- Внутренние DTO ---

        public record CreateChatRequest(string Title, long UserId);

        // тело запроса на отправку сообщения
        public record SendMessageRequest(
            long ChatId,
            int UserId,
            string? Text,
            List<string>? Base64Images     // список картинок в base64
        );

        // --- Утилиты для оценки длины истории ---

        // грубая оценка числа токенов по длине строки
        private static int EstimateTokens(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // примерно 4 символа на токен
            return Math.Max(1, text.Length / 4);
        }

        // лимит токенов ИМЕННО для истории чата в зависимости от модели
        private static int GetHistoryTokensLimit(string modelName)
        {
            var name = modelName.ToLowerInvariant();

            // Qwen3-VL instruct (обычно квантованные)
            if (name.Contains("qwen3-vl") && name.Contains("instruct"))
                return 500; //получать только последние ~500 токенов истории, всё, что "не влезло", — отрезается и НЕ отправляется в Ollama.

            // Qwen-VL / Qwen3-VL обычные
            if (name.Contains("qwen") && name.Contains("vl"))
                return 8000;

            // Phi 3.5
            if (name.Contains("phi3.5") || name.Contains("phi-3.5") || name.Contains("phi3"))
                return 3000;

            return 500;
        }


        // ---------- ЧАТЫ ----------

        /// GET: /api/Chat/user/{userId}/chats
        [HttpGet("user/{userId}/chats")]
        public async Task<ActionResult<IEnumerable<ChatDto>>> GetChats(long userId)
        {
            var chats = await _context.Chats
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.Id)
                .Select(c => new ChatDto(c.Id, c.Title))
                .ToListAsync();

            return Ok(chats);
        }

        // POST: /api/Chat
        [HttpPost]
        public async Task<ActionResult<ChatDto>> CreateChat([FromBody] CreateChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.UserId <= 0)
                return BadRequest("Title and UserId are required.");

            var newChat = new Chat_Table
            {
                Title = request.Title,
                UserId = (int)request.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Chats.Add(newChat);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetChats),
                new { userId = newChat.UserId },
                new ChatDto(newChat.Id, newChat.Title)
            );
        }

        // DELETE: /api/Chat/{chatId}
        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteChat(int chatId)
        {
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat == null)
                return NotFound();

            _context.Chats.Remove(chat);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: /api/Chat/{chatId}/clear
        [HttpPost("{chatId}/clear")]
        public async Task<IActionResult> ClearChat(long chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .ToListAsync();

            _context.Messages.RemoveRange(messages);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // ---------- СООБЩЕНИЯ ----------

        // GET: /api/Chat/{chatId}/messages
        [HttpGet("{chatId}/messages")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessages(long chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId && m.Type != "context_reset") // на всякий случай фильтруем служебные
                .Include(m => m.Images)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return Ok(messages.Select(m => m.ToDto()));
        }

        // POST: /api/Chat/send
        [HttpPost("send")]
        public async Task<ActionResult<SendMessageResponse>> SendMessage(
            [FromBody] SendMessageRequest request)
        {
            // 1. Проверяем, что чат существует
            var chat = await _context.Chats
                .FirstOrDefaultAsync(c => c.Id == request.ChatId);

            if (chat == null)
                return NotFound(new { error = "Chat not found" });

            // 2. Настройки пользователя (модель)
            var settings = await _context.Settings
                .FirstOrDefaultAsync(s => s.UserId == request.UserId);

            var modelName = settings?.Model ?? "qwen2.5-vl:7b";

            var images = request.Base64Images ?? new List<string>();
            var hasImages = images.Any();

            Console.WriteLine(
                $"[DEBUG] SendMessage: Text='{request.Text}', ImagesCount={images.Count}, Model={modelName}");

            // 3. Загружаем прошлую историю чата (без текущего хода)
            var history = await _context.Messages
                .Where(m => m.ChatId == request.ChatId && m.Type != "context_reset")
                .Include(m => m.Images)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            // 3.1 Ограничиваем историю по токенам
            var selected = new List<Message_Table>();
            int usedTokens = 0;

            int historyTokensLimit = GetHistoryTokensLimit(modelName);

            // идём с конца списка (от новых к старым)
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                int tokens = EstimateTokens(msg.Text);

                if (usedTokens + tokens > historyTokensLimit)
                    break;

                selected.Add(msg);
                usedTokens += tokens;
            }

            // разворачиваем обратно от старых к новым
            selected.Reverse();
            history = selected;

            var ollamaMessages = new List<object>();

            // 3.2. История
            foreach (var m in history)
            {
                var roleStr = m.Role == 1 ? "user" : "assistant";

                if (m.Images != null && m.Images.Count > 0)
                {
                    var imgsBase64 = m.Images
                        .Select(img => Convert.ToBase64String(img.ImageBlob))
                        .ToList();

                    ollamaMessages.Add(new
                    {
                        role = roleStr,
                        content = string.IsNullOrWhiteSpace(m.Text) ? null : m.Text,
                        images = imgsBase64
                    });
                }
                else
                {
                    ollamaMessages.Add(new
                    {
                        role = roleStr,
                        content = m.Text
                    });
                }
            }

            // 3.3. Добавляем текущий запрос пользователя
            if (hasImages)
            {
                // если есть текст — кидаем его отдельным сообщением
                if (!string.IsNullOrWhiteSpace(request.Text))
                {
                    ollamaMessages.Add(new
                    {
                        role = "user",
                        content = request.Text
                    });
                }

                // каждую картинку — отдельным user-сообщением
                int index = 1;
                foreach (var imgBase64 in images)
                {
                    ollamaMessages.Add(new
                    {
                        role = "user",
                        content = $"Current image #{index}",
                        images = new List<string> { imgBase64 }
                    });
                    index++;
                }
            }
            else
            {
                // только текст
                ollamaMessages.Add(new
                {
                    role = "user",
                    content = request.Text ?? ""
                });
            }

            // 4. Сохраняем сообщение пользователя в БД
            var userMessage = new Message_Table
            {
                ChatId = (int)request.ChatId,
                Role = 1,
                Text = request.Text ?? "",
                Type = hasImages ? "image" : "text",
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(userMessage);
            await _context.SaveChangesAsync();

            // 4.1. Сохраняем все картинки
            foreach (var imgBase64 in images)
            {
                if (string.IsNullOrWhiteSpace(imgBase64))
                    continue;

                var bytes = Convert.FromBase64String(imgBase64);

                var imgRow = new Image_Table
                {
                    MessageId = userMessage.Id,
                    ImageBlob = bytes
                };

                _context.Images.Add(imgRow);
            }

            await _context.SaveChangesAsync();

            // 5. Если модель не умеет в картинки — заглушка
            if (hasImages && !ModelCapabilities.SupportsImages(modelName))
            {
                var aiText = "Эта модель не поддерживает работу с картинками";

                var aiStub = new Message_Table
                {
                    ChatId = (int)request.ChatId,
                    Role = 0,
                    Text = aiText,
                    Type = "text",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Messages.Add(aiStub);
                await _context.SaveChangesAsync();

                await _context.Entry(userMessage)
                    .Collection(m => m.Images)
                    .LoadAsync();

                return Ok(new SendMessageResponse
                {
                    UserMessage = userMessage.ToDto(),
                    AiMessage = aiStub.ToDto()
                });
            }

            // 6–7. Пытаемся получить ответ модели, но не даём контроллеру упасть
            Message_Table aiMessage;

            try
            {
                var answerText = await _ollama.SendChatAsync(modelName, ollamaMessages);

                if (!string.IsNullOrWhiteSpace(answerText))
                {
                    answerText = answerText
                        .Replace("<im_start|>", "")
                        .Replace("<im_start>", "")
                        .Trim();
                }
                else
                {
                    answerText = "(модель вернула пустой ответ)";
                }

                aiMessage = new Message_Table
                {
                    ChatId = (int)request.ChatId,
                    Role = 0,
                    Text = answerText,
                    Type = "text",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Messages.Add(aiMessage);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Ollama SendChatAsync failed:");
                Console.WriteLine(ex.ToString());

                aiMessage = new Message_Table
                {
                    ChatId = (int)request.ChatId,
                    Role = 0,
                    Text = "(модель временно недоступна или вернула ошибку)",
                    Type = "text",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Messages.Add(aiMessage);
                await _context.SaveChangesAsync();
            }

            // 8. Подгружаем картинки для userMessage (для корректного DTO)
            await _context.Entry(userMessage)
                .Collection(m => m.Images)
                .LoadAsync();

            return Ok(new SendMessageResponse
            {
                UserMessage = userMessage.ToDto(),
                AiMessage = aiMessage.ToDto()
            });
        }
    }
}
