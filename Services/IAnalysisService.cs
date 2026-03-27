using System.Threading.Tasks;

namespace ARCompletions.Services;

public interface IAnalysisService
{
    // Process the AiFaqAnalysisJob with given jobId. Implementation should:
    // - gather conversations/messages for the vendor/date range
    // - call AI/embedding services to generate candidate FAQs
    // - persist FaqCandidate rows
    Task ProcessAnalysisJobAsync(string jobId);
}
