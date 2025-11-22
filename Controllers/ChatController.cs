//Правильный вчерашний чат контроллер
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPIChatAI.Models;
using WebAPIChatAI.Tables;
using WebAPIChatAI.Services; // для OllamaClient

namespace WebAPIChatAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatAIDB _context; // Твой контекст базы данных
        private AiController _ai;
        private readonly OllamaClient _ollama;




        public ChatController(ChatAIDB context, OllamaClient ollama)
        {
            _context = context;
            _ollama = ollama;
        }

        // --- DTO (Data Transfer Objects) ---
        // Эти классы нужны для приема данных от клиента.
        // Их можно вынести в отдельный файл.
        public record CreateChatRequest(string Title, long UserId);
        public record SendMessageRequest(long ChatId, string Text, int UserId, string Base64Image);

        // --- Эндпоинты для ЧАТОВ ---

        /// GET: /api/Chat/user/{userId}/chats
        [HttpGet("user/{userId}/chats")]
        public async Task<ActionResult<IEnumerable<ChatDto>>> GetChats(long userId)
        {
            var chats = await _context.Chats
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.Id) // Сначала сортируем
                .Select(c => new ChatDto(c.Id, c.Title)) // Затем преобразуем в DTO
                .ToListAsync();

            return Ok(chats);
        }

        // POST: /api/Chat
        [HttpPost]
        public async Task<ActionResult<ChatDto>> CreateChat([FromBody] CreateChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.UserId <= 0)
            {
                return BadRequest("Title and UserId are required.");
            }

            var newChat = new Chat_Table // Создаем новую запись для таблицы
            {
                Title = request.Title,
                UserId = (int)request.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Chats.Add(newChat);
            await _context.SaveChangesAsync();

            // Возвращаем созданный чат в виде DTO
            return CreatedAtAction(nameof(GetChats), new { userId = newChat.UserId }, new ChatDto(newChat.Id, newChat.Title));
        }

        // DELETE: /api/Chat/{chatId}
        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteChat(int chatId)
        {
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat == null)
            {
                return NotFound();
            }

            _context.Chats.Remove(chat); // Entity Framework каскадно удалит и сообщения, если настроено
            await _context.SaveChangesAsync();

            return NoContent(); // Успешно удалено
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

        // --- Эндпоинты для СООБЩЕНИЙ ---

        // GET: /api/Chat/{chatId}/messages
        [HttpGet("{chatId}/messages")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessages(long chatId)
        {
            // Сначала получаем "сырые" данные из базы данных
            var messagesFromDb = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            // А потом, уже в памяти, преобразуем их в DTO
            // Это решает проблему "could not be translated"
            var messagesDto = messagesFromDb
                .Select(m => new MessageDto(m.Id, m.Text, m.Role))
                .ToList();

            return Ok(messagesDto);
        }




        //[HttpPost("send")]
        //public async Task<ActionResult<SendMessageResponse>> SendMessage([FromBody] SendMessageRequest request)
        //{
        //    // 1. Проверяем чат (это у тебя уже есть)
        //    var chat = await _context.Chats
        //        .FirstOrDefaultAsync(c => c.Id == request.ChatId);

        //    if (chat == null)
        //    {
        //        return NotFound("chat not found");
        //    }

        //    // 2. Создаём и сохраняем сообщение пользователя (это у тебя уже есть)
        //    var userMessage = new Message_Table
        //    {
        //        ChatId = (int)request.ChatId,
        //        Text = request.Text,
        //        Role = 1, // 1 = пользователь
        //        CreatedAt = DateTime.UtcNow
        //    };
        //    _context.Messages.Add(userMessage);
        //    await _context.SaveChangesAsync(); // <- Важно сохраниться здесь, чтобы сообщение попало в историю

        //    // --- НОВЫЙ ШАГ: ЗАГРУЖАЕМ КОНТЕКСТ ИЗ БД ---

        //    // 3. Получаем ВСЮ историю сообщений для этого чата, включая только что добавленное
        //    var history = await _context.Messages
        //        .Where(m => m.ChatId == request.ChatId)
        //        .OrderBy(m => m.CreatedAt) // Сортируем по дате, чтобы диалог был в правильном порядке
        //        .ToListAsync();

        //    // 4. Формируем из истории единый промпт для нейросети
        //    // (Формат "Роль: Текст" очень хорошо понимают языковые модели)
        //    var prompt = string.Join("\n", history.Select(m => {
        //        var prefix = (m.Role == 1) ? "User:" : "Assistant:";
        //        return $"{prefix} {m.Text}";
        //    }));

        //    // 5. Получаем ответ от AI, передавая ВЕСЬ КОНТЕКСТ
        //    var aiText = await _ollama.GenerateAsync(prompt); // <-- ИЗМЕНЕНИЕ ЗДЕСЬ

        //    // 6. Сохраняем ответ AI (это у тебя уже было)
        //    var aiMessage = new Message_Table
        //    {
        //        ChatId = (int)request.ChatId,
        //        Text = aiText,
        //        Role = 0, // 0 = модель
        //        CreatedAt = DateTime.UtcNow
        //    };
        //    _context.Messages.Add(aiMessage);
        //    await _context.SaveChangesAsync();

        //    // 7. Готовим и отправляем ответ для Android (это у тебя уже было)
        //    var response = new SendMessageResponse
        //    {
        //        UserMessage = new MessageDto { /* ... */ },
        //        AiMessage = new MessageDto { /* ... */ },
        //    };

        //    return Ok(response);
        //}

        [HttpPost("send")]
        public async Task<ActionResult<SendMessageResponse>> SendMessage([FromBody] SendMessageRequest request)
        {
            // 1. Проверяем, что чат существует
            var chat = await _context.Chats
                .FirstOrDefaultAsync(c => c.Id == request.ChatId);

            if (chat == null)
                return NotFound(new { error = "Chat not found" });

            // 2. Вытащим настройки пользователя (модель)
            var settings = await _context.Settings
                .FirstOrDefaultAsync(s => s.UserId == request.UserId);

            var modelName = settings?.Model ?? "qwen2.5-vl:7b";

            // 3. Создаём и сохраняем сообщение пользователя
            var userMessage = new Message_Table
            {
                ChatId = (int)request.ChatId,
                Role = 1, // 1 = user
                Text = request.Text ?? "",
                Type = string.IsNullOrEmpty(request.Base64Image) ? "text" : "image",
                ImageBlob = string.IsNullOrEmpty(request.Base64Image)
                    ? null
                    : Convert.FromBase64String(request.Base64Image),
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(userMessage);
            await _context.SaveChangesAsync();

            // 4. Если модель не поддерживает картинки, просто вернём текст-заглушку
            if (!ModelCapabilities.SupportsImages(modelName) &&
                userMessage.Type == "image")
            {
                var aiText = "Эта модель не поддерживает работу с картинками";

                var aiMessage = new Message_Table
                {
                    ChatId = (int)request.ChatId,
                    Role = 0, // assistant
                    Text = aiText,
                    Type = "text",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Messages.Add(aiMessage);
                await _context.SaveChangesAsync();

                return Ok(new SendMessageResponse
                {
                    UserMessage = userMessage.ToDto(),
                    AiMessage = aiMessage.ToDto()
                });
            }

            // 5. Загружаем весь контекст чата
            var history = await _context.Messages
                .Where(m => m.ChatId == request.ChatId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var ollamaMessages = new List<object>();

            foreach (var m in history)
            {
                var roleStr = m.Role == 1 ? "user" : "assistant";

                if (m.Type == "image" && m.ImageBlob != null)
                {
                    ollamaMessages.Add(new
                    {
                        role = roleStr,
                        content = string.IsNullOrWhiteSpace(m.Text) ? null : m.Text,
                        images = new[] { Convert.ToBase64String(m.ImageBlob) }
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

            // 6. Запрашиваем ответ у модели
            var answerText = await _ollama.SendChatAsync(modelName, ollamaMessages);

            var aiMsg = new Message_Table
            {
                ChatId = (int)request.ChatId,
                Role = 0,
                Text = answerText,
                Type = "text",
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(aiMsg);
            await _context.SaveChangesAsync();

            return Ok(new SendMessageResponse
            {
                UserMessage = userMessage.ToDto(),
                AiMessage = aiMsg.ToDto()
            });
        }






    }
}

public static class MessageMapper
{
    public static MessageDto ToDto(this Message_Table m)
    {
        return new MessageDto
        {
            Id = m.Id,
            ChatId = m.ChatId,
            Role = m.Role,
            Type = m.Type,
            Text = m.Text,
            Base64Image = m.ImageBlob != null ? Convert.ToBase64String(m.ImageBlob) : null,
            CreatedAt = m.CreatedAt
        };
    }
}
