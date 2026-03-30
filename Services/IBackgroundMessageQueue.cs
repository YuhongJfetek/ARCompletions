using System.Threading.Channels;

namespace ARCompletions.Services;

public interface IBackgroundMessageQueue
{
    ChannelReader<long> Reader { get; }
    void Enqueue(long eventRowId);
}
