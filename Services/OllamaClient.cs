using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WebAPIChatAI.Services
{
    public class OllamaClient
    {
        private readonly HttpClient _http;

        public OllamaClient(HttpClient http)
        {
            _http = http;
            _http.BaseAddress = new Uri("http://10.16.69.133:11434/");
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
            return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
        }

        public async IAsyncEnumerable<string> StreamGenerateAsync(
            string prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var body = new
            {
                model = "qwen3-vl:2b-instruct-q4_K_M",
                prompt = prompt,
                stream = true
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
            {
                Content = content
            };

            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                using var doc = JsonDocument.Parse(line);

                if (doc.RootElement.TryGetProperty("done", out var doneProp)
                    && doneProp.GetBoolean())
                {
                    yield break;
                }

                if (doc.RootElement.TryGetProperty("response", out var respProp))
                {
                    var chunk = respProp.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        yield return chunk;
                    }
                }
            }
        }

        public async Task<string> SendChatAsync(string model, List<object> messages, CancellationToken cancellationToken = default)
        {
            var body = new
            {
                model = model,
                messages = messages,
                stream = false
            };

            var json = JsonSerializer.Serialize(body);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // ← ← ← ВАЖНО: сюда пробрасываем токен
            using var response = await _http.PostAsync("/api/chat", content, cancellationToken);

            response.EnsureSuccessStatusCode();

            // ← ← ← сюда тоже пробрасываем токен
            var respJson = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(respJson);

            // твой парсинг JSON как был
            return doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }


    }
}