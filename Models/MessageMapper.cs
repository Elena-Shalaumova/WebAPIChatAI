using System;
using System.Linq;
using System.Collections.Generic;
using WebAPIChatAI.Tables;

namespace WebAPIChatAI.Models
{
    /// <summary>
    /// Маппер: из сущности Message_Table (EF / БД)
    /// в DTO MessageDto (то, что отдаём в Android).
    /// </summary>
    public static class MessageMapper
    {
        public static MessageDto ToDto(this Message_Table m)
        {
            return new MessageDto
            {
                Id = m.Id,
                ChatId = m.ChatId,
                Role = m.Role,
                Text = m.Text,
                Type = m.Type,
                Images = m.Images != null
                    ? m.Images.Select(img => Convert.ToBase64String(img.ImageBlob)).ToList()
                    : new List<string>(),
                CreatedAt = m.CreatedAt
            };
        }

        public static List<MessageDto> ToDtos(this IEnumerable<Message_Table> messages)
        {
            return messages.Select(m => m.ToDto()).ToList();
        }
    }
}
