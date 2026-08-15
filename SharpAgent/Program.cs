using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Tools.Shell;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using SharpAgent;
using System.ClientModel;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.Configure<OutpubOptions>(builder.Configuration.GetSection("OutpubOptions"));

builder.Services.Configure<OpenAIClientOptions>(builder.Configuration.GetSection("OpenAI"));

builder.Services.AddSingleton<ChatHistoryStore>()
    .AddSingleton<AgentSessionStore>()
    .AddSingleton<LocalShellExecutorStore>();

// register the ChatClient as follows
/*
  "OpenAI": {
    "Model": "kimi-k3",
    "Options": {
      "Endpoint":  "https://api.moonshot.cn/v1"
    },
    "Credential": {
      "CredentialSource": "ApiKeyCredential",
      "Key": "your_api_key"
    }
  }
//*/
builder.AddChatClient("OpenAI");

builder.Services.AddSingleton(sp =>
{
    var openAIClientOptions = sp.GetService<OpenAIClientOptions>();
    var key = builder.Configuration["OpenAI:Credential:Key"];
    return new OpenAIClient(new ApiKeyCredential(key), openAIClientOptions);
});

builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetService<ILoggerFactory>();
    var chatClient = sp.GetService<ChatClient>();

    var agent = chatClient.AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            ChatOptions = new()
            {
                Instructions = "You are a helpful assistant.",
                Tools = [AIFunctionFactory.Create(GetCurrentLocation)],
            },
            ChatHistoryProvider = new InMemoryChatHistoryProvider()
        },
        clientFactory: null,
        loggerFactory: loggerFactory,
        services: sp
    );

    return agent;
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.MapHub<ChatHub>("/chat");

app.Run();


[Description("获取用户的当前位置")]
static string GetCurrentLocation()
{
    Console.WriteLine("Getting current location...");
    return "{\"location\": \"上海\"}";
}
