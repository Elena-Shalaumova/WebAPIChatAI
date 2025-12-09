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

        public record CreateChatRequest(string Title, long UserId, bool IsIncognito);

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
            var query =
                from c in _context.Chats
                where c.UserId == userId
                 && !c.IsIncognito
                join sc in _context.SettingChats
                    on c.Id equals sc.ChatId into scGroup

                from sc in scGroup
                    .OrderByDescending(x => x.Id)
                    .Take(1)
                    .DefaultIfEmpty()

                orderby c.Id descending

                select new ChatDto(c.Id, c.Title)
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    Title = c.Title,
                    Model = sc != null ? sc.Model : null,
                    IsIncognito = c.IsIncognito
                };

            var chats = await query.ToListAsync();
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
                CreatedAt = DateTime.UtcNow,
                IsIncognito = request.IsIncognito
            };


            _context.Chats.Add(newChat);
            await _context.SaveChangesAsync();

            var userSettings = await _context.Settings.FirstOrDefaultAsync(s => s.UserId == request.UserId);

            var defaultModel = userSettings?.Model ?? "qwen3-vl:2b";
            var defaultTemp = userSettings?.Temperature ?? 0.7;
            var defaultMaxTokens = userSettings?.MaxTokens ?? 1024;

            var chatSetting = new SettingsChat_Table
            {
                ChatId = newChat.Id,
                Model = defaultModel,
                Temperature = defaultTemp,
                MaxTokens = defaultMaxTokens
            };

            _context.SettingChats.Add(chatSetting);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetChats),
                new { userId = newChat.UserId },
                new ChatDto(newChat.Id, newChat.Title)
                {
                    UserId = newChat.UserId,
                    Model = chatSetting.Model,
                    IsIncognito = newChat.IsIncognito
                }
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

        // GET: api/ChatSettings/{chatId}
        [HttpGet("getChatSettings/{chatId}")]
        public async Task<ActionResult<SettingsChat_Table>> GetChatSettings(int chatId)
        {
            var entity = await _context.SettingChats
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ChatId == chatId);

            if (entity == null) return NotFound();

            return new SettingsChat_Table
            {
                ChatId = entity.ChatId,
                Model = entity.Model,
                Temperature = entity.Temperature,
                MaxTokens = entity.MaxTokens
            };
        }

        // POST: api/ChatSettings/chat/save
        [HttpPost("chat/saveChatSettings")]
        public async Task<ActionResult<SettingsChat_Table>> SaveChatSettings(
            [FromBody] SettingsChat_Table request)
        {
            // ищем запись по ChatId
            var entity = await _context.SettingChats
                .FirstOrDefaultAsync(s => s.ChatId == request.ChatId);

            if (entity == null)
            {
                entity = new SettingsChat_Table
                {
                    ChatId = request.ChatId,
                    Model = request.Model,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens
                };
                _context.SettingChats.Add(entity);
            }
            else
            {
                entity.Model = request.Model;
                entity.Temperature = request.Temperature;
                entity.MaxTokens = request.MaxTokens;
            }

            await _context.SaveChangesAsync();

            return Ok(entity);
        }

        // POST: /api/Chat/send 
        [HttpPost("send")]
        public async Task<ActionResult<SendMessageResponse>> SendMessage(
            [FromBody] SendMessageRequest request,
             CancellationToken cancellationToken)
        {
            try
            {
                // 1. Проверяем, что чат существует
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.Id == request.ChatId);

                if (chat == null)
                    return NotFound(new { error = "Chat not found" });

                // ⚡ флаг инкогнито
                bool isIncognito = chat.IsIncognito;

                // 2. Настройки пользователя (модель)
                var settings = await _context.Settings
                    .FirstOrDefaultAsync(s => s.UserId == request.UserId);

                var modelName = settings?.Model ?? "qwen2.5-vl:7b";

                var images = request.Base64Images ?? new List<string>();
                var hasImages = images.Any();

                Console.WriteLine(
                    $"[DEBUG] SendMessage: Text='{request.Text}', ImagesCount={images.Count}, Model={modelName}");

                // 3. Загружаем прошлую историю чата (без текущего хода)
                List<Message_Table> history;

                if (isIncognito)
                {
                    // инкогнито: историю не берём из БД
                    history = new List<Message_Table>();
                }
                else
                {
                    history = await _context.Messages
                        .Where(m => m.ChatId == request.ChatId && m.Type != "context_reset")
                        .Include(m => m.Images)
                        .OrderBy(m => m.CreatedAt)
                        .ToListAsync(cancellationToken);
                    //.ToListAsync();
                }

                // 3.1 Ограничиваем историю по токенам
                var selected = new List<Message_Table>();
                int usedTokens = 0;

                int historyTokensLimit = GetHistoryTokensLimit(modelName);

                for (int i = history.Count - 1; i >= 0; i--)
                {
                    var msg = history[i];
                    int tokens = EstimateTokens(msg.Text);

                    if (usedTokens + tokens > historyTokensLimit)
                        break;

                    selected.Add(msg);
                    usedTokens += tokens;
                }

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
                    if (!string.IsNullOrWhiteSpace(request.Text))
                    {
                        ollamaMessages.Add(new
                        {
                            role = "user",
                            content = request.Text
                        });
                    }

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
                    ollamaMessages.Add(new
                    {
                        role = "user",
                        content = request.Text ?? ""
                    });
                }

                // 4. Сохраняем сообщение пользователя (только если НЕ инкогнито)
                Message_Table? userMessage = null;

                if (!isIncognito)
                {
                    userMessage = new Message_Table
                    {
                        ChatId = (int)request.ChatId,
                        Role = 1,
                        Text = request.Text ?? "",
                        Type = hasImages ? "image" : "text",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(userMessage);
                    //await _context.SaveChangesAsync();
                    await _context.SaveChangesAsync(cancellationToken);

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
                    await _context.SaveChangesAsync(cancellationToken);
                    //await _context.SaveChangesAsync();
                }

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

                    if (!isIncognito)
                    {
                        _context.Messages.Add(aiStub);
                        await _context.SaveChangesAsync(cancellationToken);
                        //await _context.SaveChangesAsync();

                        if (userMessage != null)
                        {
                            await _context.Entry(userMessage)
                                .Collection(m => m.Images)
                                .LoadAsync(cancellationToken);
                            //.LoadAsync();
                        }
                    }

                    return Ok(new SendMessageResponse
                    {
                        UserMessage = userMessage?.ToDto(),  // в инкогнито может быть null
                        AiMessage = aiStub.ToDto()
                    });
                }

                // 6–7. Ответ модели (с защитой от падения)
                Message_Table aiMessage;

                try
                {
                    var answerText = await _ollama.SendChatAsync(modelName, ollamaMessages, cancellationToken);

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

                    if (!isIncognito)
                    {
                        _context.Messages.Add(aiMessage);
                        await _context.SaveChangesAsync(cancellationToken);
                        //await _context.SaveChangesAsync();
                    }
                }

                catch (OperationCanceledException)
                {
                    // 🛑 Клиент отменил запрос — НЕ сохраняем aiMessage
                    Console.WriteLine("[INFO] SendMessage canceled by client");
                    // Можно вернуть спец-код, но клиент всё равно уже отвалился.
                    return StatusCode(StatusCodes.Status499ClientClosedRequest);
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

                    if (!isIncognito)
                    {
                        _context.Messages.Add(aiMessage);
                        //await _context.SaveChangesAsync();
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }

                // 8. Подгружаем картинки для userMessage (если есть и не инкогнито)
                if (!isIncognito && userMessage != null)
                {
                    await _context.Entry(userMessage)
                        .Collection(m => m.Images)
                        .LoadAsync(cancellationToken);
                    // .LoadAsync();
                }

                return Ok(new SendMessageResponse
                {
                    UserMessage = userMessage?.ToDto(),  // для инкогнито null
                    AiMessage = aiMessage.ToDto()
                });
            }
            catch (OperationCanceledException)
            {
                // На всякий случай общий catch, если отмена случится раньше
                Console.WriteLine("[INFO] SendMessage canceled (outer catch)");
                return StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            
        }



        public record RenameChatRequest(string Title);

        [HttpPut("{chatId}/rename")]
        public async Task<IActionResult> RenameChat(int chatId, [FromBody] RenameChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required");

            var chat = await _context.Chats.FindAsync(chatId);
            if (chat == null)
                return NotFound();

            chat.Title = request.Title;
            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}
