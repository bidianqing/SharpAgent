using Microsoft.Agents.AI;
using System.Collections.Concurrent;

namespace SharpAgent
{
    public class AgentSessionStore
    {
        private readonly ConcurrentDictionary<string, AgentSession> _store = new();

        public bool TryGetValue(string conversationId, out AgentSession agentSession)
        {
            return _store.TryGetValue(conversationId, out agentSession);
        }

        public AgentSession GetOrAdd(string conversationId, AgentSession agentSession)
        {
            return _store.GetOrAdd(conversationId, agentSession);
        }

        public void Remove(string conversationId)
        {
            _store.TryRemove(conversationId, out _);
        }

        public void Clear()
        {
            _store.Clear();
        }
    }
}
