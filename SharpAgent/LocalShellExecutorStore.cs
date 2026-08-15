using Microsoft.Agents.AI.Tools.Shell;
using System.Collections.Concurrent;

namespace SharpAgent
{
    public class LocalShellExecutorStore
    {
        private readonly ConcurrentDictionary<string, LocalShellExecutor> _store = new();

        public bool TryGetValue(string conversationId, out LocalShellExecutor executor)
        {
            return _store.TryGetValue(conversationId, out executor);
        }

        public void GetOrAdd(string conversationId, LocalShellExecutor executor)
        {
            _store.GetOrAdd(conversationId, executor);
        }

        public void Remove(string conversationId)
        {
            _store.TryRemove(conversationId, out _);
        }
    }


}
