using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public record EmbeddingUpdateRequest(string FaqId, string Text, string Provider, string? Model = null);

    public interface IEmbeddingUpdateQueue
    {
        ValueTask EnqueueAsync(EmbeddingUpdateRequest req);
    }

    public class EmbeddingUpdateQueue : IEmbeddingUpdateQueue, IDisposable
    {
        private readonly Channel<EmbeddingUpdateRequest> _channel;

        public EmbeddingUpdateQueue(int capacity = 1024)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<EmbeddingUpdateRequest>(options);
        }

        public ValueTask EnqueueAsync(EmbeddingUpdateRequest req)
        {
            return _channel.Writer.WriteAsync(req);
        }

        internal IAsyncEnumerable<EmbeddingUpdateRequest> ReadAllAsync() => _channel.Reader.ReadAllAsync();

        public void Dispose()
        {
            try { _channel.Writer.Complete(); } catch { }
        }
    }
}
