using System.Threading.Channels;

namespace ARCompletions.Services;

public interface IBulkJobQueue
{
    ChannelReader<string> Reader { get; }
    void Enqueue(string jobId);
}
