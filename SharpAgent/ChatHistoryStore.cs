using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace SharpAgent
{
    public class ChatHistoryStore
    {
        // Microsoft.Extensions.AI.ChatMessage
        // OpenAI.Chat.ChatMessage
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _store = new();


        public void SetChatMessages(string conversationId, List<ChatMessage> messages)
        {
            //_store.GetOrAdd(conversationId, _ => new List<ChatMessage>());
            _store[conversationId] = messages;
        }


        public List<ChatMessage> GetMessages(string conversationId)
        {
            if (_store.TryGetValue(conversationId, out var messages))
            {
                return messages;
            }

            return new List<ChatMessage>();
        }
    }
}
