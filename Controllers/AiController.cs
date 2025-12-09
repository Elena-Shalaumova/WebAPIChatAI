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
            //_httpClient.BaseAddress = new Uri("http://localhost:11434");
            _httpClient.BaseAddress = new Uri("http://192.168.3.63:11434");

            _config = config;                      // ← теперь всё ок
        }

        // дальше твои методы Chat / ollama-version / models / debug-db
        // можно оставить как есть
    }
}

