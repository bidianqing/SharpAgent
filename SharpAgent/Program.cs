using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using SharpAgent;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.Configure<OutpubOptions>(builder.Configuration.GetSection("OutpubOptions"));

builder.Services.Configure<OpenAIClientOptions>(builder.Configuration.GetSection("OpenAI"));

builder.Services.AddSingleton<ChatHistoryStore>()
    .AddSingleton<AgentSessionStore>();

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

// Create the MCP client.
// Configure it to start and connect to your MCP server.
var transport = new StdioClientTransport(new()
{
    Command = "npx",
    Arguments = new[] { "-y", "@wonderwhy-er/desktop-commander@latest" },
    Name = "desktop-commander",
});
var mcpClientOptions = new McpClientOptions
{
    InitializationTimeout = TimeSpan.FromSeconds(120)
};
McpClient mcpClient = await McpClient.CreateAsync(transport, mcpClientOptions);

IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

// context7 https://context7.com/  https://github.com/mcp/upstash/context7
//var httpClientTransport = new HttpClientTransport(new HttpClientTransportOptions
//{
//    Endpoint = new Uri("https://mcp.context7.com/mcp"),
//    Name = "context7",
//    AdditionalHeaders = new Dictionary<string, string>
//    {
//        { "Authorization", "Bearer your apikey"}
//    }
//});
//mcpClient = await McpClient.CreateAsync(httpClientTransport);
//IList<McpClientTool> context7Tools = await mcpClient.ListToolsAsync();


builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetService<ILoggerFactory>();
    var chatClient = sp.GetService<ChatClient>();

    var agent = chatClient.AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            ChatOptions = new()
            {
                Instructions = "You are a helpful assistant. 你可以调用desktop-commander工具操作本地文件系统",
                Tools = [.. tools],
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