using System.Threading.Channels;

namespace ARCompletions.Services;

public class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ChannelReader<string> Reader => _channel.Reader;

    public void Enqueue(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return;
        _channel.Writer.TryWrite(jobId);
    }
}
