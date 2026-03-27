using System.Threading.Channels;

namespace ARCompletions.Services;

public interface IBackgroundJobQueue
{
    ChannelReader<string> Reader { get; }
    void Enqueue(string jobId);
}
