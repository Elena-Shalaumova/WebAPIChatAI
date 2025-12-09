using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Net.Sockets;

namespace WebAPIChatAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        [HttpGet("db")]
        public async Task<IActionResult> DebugDb([FromServices] IConfiguration config)
        {
            var cs = config.GetConnectionString("ChatAIDBConnection");

            // Разберём хост и порт из строки
            var builder = new MySqlConnectionStringBuilder(cs);
            string host = builder.Server;
            int port = (int)builder.Port;

            // 1) Проверка TCP-доступности хоста и порта
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port);
            }
            catch (Exception ex)
            {
                return Ok($"❌ TCP ERROR: не удалось подключиться к {host}:{port}\n{ex.Message}");
            }

            // 2) Проверка MySQL-подключения
            try
            {
                await using var conn = new MySqlConnection(cs);
                await conn.OpenAsync();
                return Ok("✅ MySQL CONNECT OK — WebAPI успешно подключился к базе!");
            }
            catch (Exception ex)
            {
                return Ok($"❌ MySQL CONNECT FAILED:\n{ex.Message}");
            }
        }
    }
}
