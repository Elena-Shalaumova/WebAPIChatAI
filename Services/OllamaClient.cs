using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebAPIChatAI.Services
{
    public class OllamaClient
    {
        private readonly HttpClient _http;

        public OllamaClient(HttpClient http)
        {
            _http = http;
            _http.BaseAddress = new Uri("http://localhost:11434/");
            //_http.BaseAddress = new Uri("http://178.130.131.73:8080/");

            // ВАЖНО: ОТКЛЮЧАЕМ ТАЙМАУТ ПОЛНОСТЬЮ
            _http.Timeout = Timeout.InfiniteTimeSpan;
        }

        /// <summary>
        /// Старый простой текстовый запрос — оставляем как есть
        /// </summary>
        public async Task<string> GenerateAsync(string prompt)
        {
            var body = new
            {
                model = "qwen3-vl:2b-instruct-q4_K_M",
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("response").GetString() ?? "";
        }

       

        public async Task<string> SendChatAsync(string model, List<object> messages)
        {
            var body = new
            {
                model = model,
                messages = messages,
                stream = false
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("api/chat", content);
            response.EnsureSuccessStatusCode();

            var respJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respJson);

            // { "message": { "content": "..." }, ... }
            return doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

    }
}
