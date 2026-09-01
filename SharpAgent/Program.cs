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

// https://github.com/wonderwhy-er/DesktopCommanderMCP
var desktopCommanderTransport = new StdioClientTransport(new()
{
    Command = "npx",
    Arguments = new[] { "-y", "@wonderwhy-er/desktop-commander@latest" },
    Name = "desktop-commander",
});
var desktopCommanderOptions = new McpClientOptions
{
    InitializationTimeout = TimeSpan.FromSeconds(120)
};
var desktopCommanderClient = await McpClient.CreateAsync(desktopCommanderTransport, desktopCommanderOptions);
var desktopCommanderTools = await desktopCommanderClient.ListToolsAsync();

// context7 https://context7.com/  https://github.com/mcp/upstash/context7
/*
var httpClientTransport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("https://mcp.context7.com/mcp"),
    Name = "context7",
    AdditionalHeaders = new Dictionary<string, string>
    {
        { "Authorization", "Bearer your apikey"}
    }
});
mcpClient = await McpClient.CreateAsync(httpClientTransport);
IList<McpClientTool> context7Tools = await mcpClient.ListToolsAsync();
//*/

// tavily-mcp https://github.com/tavily-ai/tavily-mcp
var tavilyApiKey = builder.Configuration["TavilyApiKey"];
var tavilyTransport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("https://mcp.tavily.com/mcp/?tavilyApiKey=" + tavilyApiKey),
    Name = "tavily-mcp",
});
var tavilyClient = await McpClient.CreateAsync(tavilyTransport);
var tavilyTools = await tavilyClient.ListToolsAsync();

string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
string skillPath = Path.Combine(userProfile, ".sharp-agent", "skills");
Directory.CreateDirectory(skillPath);
var skillsProvider = new AgentSkillsProvider(skillPath);

builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetService<ILoggerFactory>();
    var openAIClient = sp.GetService<OpenAIClient>();

    var agent = openAIClient.GetChatClient(builder.Configuration["OpenAIClientOptions:Model"]).AsAIAgent(
        options: new ChatClientAgentOptions()
        {
            Name = "SharpAgent",
            ChatOptions = new()
            {
                Instructions = "You are a helpful assistant.",
                Tools = [.. desktopCommanderTools, .. tavilyTools],
            },
            ChatHistoryProvider = new InMemoryChatHistoryProvider(),
            AIContextProviders = [skillsProvider]
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