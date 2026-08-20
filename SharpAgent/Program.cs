using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
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

builder.Services.Configure<OpenAIClientOptions>(builder.Configuration.GetSection(nameof(OpenAIClientOptions)))
    .PostConfigure<OpenAIClientOptions>((options) =>
    {
        //options.AddPolicy(new BodyLoggingPolicy(), PipelinePosition.PerCall);
    });

builder.Services.AddSingleton<ChatHistoryStore>()
    .AddSingleton<AgentSessionStore>();

builder.Services.AddSingleton(sp =>
{
    var openAIClientOptions = (sp.GetService<IOptions<OpenAIClientOptions>>()).Value;
    var key = builder.Configuration["OpenAIClientOptions:ApiKey"];
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
var desktopCommanderTools = await mcpClient.ListToolsAsync();

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
    var openAIClient = sp.GetService<OpenAIClient>();

    var agent = openAIClient.GetChatClient(builder.Configuration["OpenAIClientOptions:Model"]).AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            ChatOptions = new()
            {
                Instructions = "You are a helpful assistant. 你可以调用desktop-commander工具操作本地文件系统",
                Tools = [.. desktopCommanderTools],
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