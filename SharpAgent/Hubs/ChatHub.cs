using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Tools.Shell;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace SharpAgent
{
    public class ChatHub : Hub
    {
        private readonly ChatHistoryStore _historyStore;
        private readonly AgentSessionStore _sessionStore;

        private readonly ILogger<ChatHub> _logger;
        private readonly OutpubOptions _outpubOptions;

        private readonly ChatClientAgent agent;

        public ChatHub(ChatHistoryStore historyStore, AgentSessionStore sessionStore, ILogger<ChatHub> logger, IOptionsMonitor<OutpubOptions> optionsMonitorAccessor, ChatClientAgent agent)
        {
            _outpubOptions = optionsMonitorAccessor.CurrentValue;
            _logger = logger;
            _historyStore = historyStore;
            _sessionStore = sessionStore;
            this.agent = agent;
        }

        public async Task Chat(string conversationId, string userName, string message)
        {
            var id = Guid.NewGuid().ToString();

            var chatMessage = new ChatMessage(ChatRole.User, message);

            if (!_sessionStore.TryGetValue(conversationId, out var session))
            {
                session = await agent.CreateSessionAsync();
                _sessionStore.GetOrAdd(conversationId, session);
            }

            // 非流式传输
            /*
            var agentResponse = await agent.RunAsync(messages);
            _logger.LogInformation($"AgentResponse: {JsonSerializer.Serialize(agentResponse.Messages, new JsonSerializerOptions() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })}");

            var reponseMessages = agentResponse.Messages.Select(m => m.Text);
            await Clients.Client(base.Context.ConnectionId).SendAsync("newMessageWithId", "", id, string.Join("", reponseMessages));

            _history.AddChatMessages(base.Context.ConnectionId, agentResponse.Messages);
            //*/


            // 流式传输

            var updating = new StringBuilder();
            bool isFirstReasoning = true;
            bool isFirstText = true;
            bool hasMore = true;
            

            // 这里手动使用循环是为了工具执行需要审批，必须在控制台输入y或者yes之后，才能继续执行下一步
            while (hasMore)
            {
                var approvalRequests = new List<ToolApprovalRequestContent>();

                await foreach (var update in agent.RunStreamingAsync(chatMessage, session))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        var updateString = JsonSerializer.Serialize(update, new JsonSerializerOptions() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                        _logger.LogDebug($"{updateString}");
                    }


                    // [调用大模型]推理 → 执行工具 → 拿到工具结果 → [调用大模型]继续推理 → ... → 推理结束 → 输出最终结果
                    if (update.Contents != null && update.Contents.Count > 0)
                    {
                        foreach (var content in update.Contents)
                        {
                            if (content is TextReasoningContent textReasoning && _outpubOptions.OutputTextReasoning)
                            {
                                // 持续输入推理的文本
                                if (isFirstReasoning)
                                {
                                    updating.AppendLine();
                                    updating.AppendLine();
                                    updating.AppendLine("---");
                                    updating.Append("【思考】" + textReasoning);
                                    isFirstReasoning = false;
                                }
                                else
                                {
                                    updating.Append(textReasoning);
                                }
                            }
                            else if (content is TextContent text)
                            {
                                // 推理结束，输出最终结果
                                if (isFirstText && !string.IsNullOrWhiteSpace(text.Text))
                                {
                                    updating.AppendLine();
                                    updating.AppendLine();
                                    updating.AppendLine("---");
                                    updating.Append(text);
                                    isFirstText = false;
                                }
                                else
                                {
                                    updating.Append(text);
                                }
                            }
                            else if (content is FunctionCallContent functionCall && _outpubOptions.OutputFunctionCall)
                            {
                                isFirstText = true;
                                isFirstReasoning = true;
                                updating.AppendLine();
                                updating.AppendLine();
                                updating.AppendLine("---");
                                updating.Append($"【FunctionCall】Calling function: {functionCall.Name} with arguments: {JsonSerializer.Serialize(functionCall.Arguments, new JsonSerializerOptions() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })}");
                            }
                            else if (content is FunctionResultContent functionResult && _outpubOptions.OutputFunctionResult)
                            {
                                isFirstText = true;
                                isFirstReasoning = true;
                                updating.AppendLine();
                                updating.AppendLine();
                                updating.AppendLine("---");
                                updating.Append($"【FunctionResult】FunctionCall result: {JsonSerializer.Serialize(functionResult.Result, new JsonSerializerOptions() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })}");
                            }
                            else if (content is UsageContent usage && _outpubOptions.OutputUsage)
                            {
                                isFirstText = true;
                                isFirstReasoning = true;
                                updating.AppendLine();
                                updating.AppendLine();
                                updating.AppendLine("---");
                                updating.Append($"【用量】Tokens used: {JsonSerializer.Serialize(usage.Details)}");
                            }
                            else if (content is ToolApprovalRequestContent toolApprovalRequest)
                            {
                                approvalRequests.Add(toolApprovalRequest);
                            }
                        }

                        await Clients.Client(base.Context.ConnectionId).SendAsync("newMessageWithId", "", id, updating.ToString());
                    }
                }

                if (approvalRequests.Count == 0)
                {
                    hasMore = false;
                    continue;
                }

                var responses = approvalRequests.Select(req =>
                {
                    var call = (FunctionCallContent)req.ToolCall;
                    Console.WriteLine($"【工具审批请求】{JsonSerializer.Serialize(req, new JsonSerializerOptions() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })}");
                    Console.WriteLine("是否同意执行该工具？(y/n): ");
                    string suggestion = Console.ReadLine();
                    bool approved = suggestion.Equals("y", StringComparison.OrdinalIgnoreCase) || suggestion.Equals("yes", StringComparison.OrdinalIgnoreCase);

                    return new ChatMessage(ChatRole.User, new[] { req.CreateResponse(approved, "同意执行") });
                }).ToList();

                chatMessage = responses.Count == 1 ? responses[0] : new ChatMessage(ChatRole.User, responses.SelectMany(r => r.Contents).ToList());
            }



            // 从[多轮]的[流式输出]中提取结构化的List<ChatMessage> 保存到history
            session.TryGetInMemoryChatHistory(out List<ChatMessage> sessionMessages);
            _historyStore.SetChatMessages(conversationId, sessionMessages);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var allMessages = _historyStore.GetMessages(conversationId);

                _logger.LogDebug(JsonSerializer.Serialize(allMessages, new JsonSerializerOptions() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }

            //*/
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Task.CompletedTask;
        }
    }

}
