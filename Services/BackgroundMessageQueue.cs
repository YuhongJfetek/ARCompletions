using System.Threading.Channels;

namespace ARCompletions.Services;

public class BackgroundMessageQueue : IBackgroundMessageQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>();

    public ChannelReader<long> Reader => _channel.Reader;

    public void Enqueue(long eventRowId)
    {
        if (eventRowId <= 0) return;
        _channel.Writer.TryWrite(eventRowId);
    }
}
