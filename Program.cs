using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using WebAPIChatAI.Models;
using WebAPIChatAI.Services;


var builder = WebApplication.CreateBuilder(args);



var csb = new MySqlConnectionStringBuilder
{
    Server = "127.0.0.1",
    Port = 3306,
    Database = "alabugaaidb",
    UserID = "clientKotlin",
    Password = "12345678",
    SslMode = MySqlSslMode.None,
    AllowPublicKeyRetrieval = true,
    CharacterSet = "utf8mb4"
};
var cs = csb.ConnectionString;
//var cs = builder.Configuration.GetConnectionString("ChatADBConnection");

builder.Services.AddDbContext<ChatAIDB>(options =>
{
    options.UseMySql(cs, ServerVersion.AutoDetect(cs)); // Pomelo
    options.UseLazyLoadingProxies();                    // если реально нужно
});




// Add services to the container.

builder.Services.AddControllers();



builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");  // или http://ollama:11434
    client.Timeout = Timeout.InfiniteTimeSpan;                // ❗ бесконечное ожидание
});


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
