using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services;

// Minimal stub implementation that demonstrates how to process a job:
// - marks job as processing/finished, creates a small number of FaqCandidate rows
// Replace with a real implementation that calls OpenAI and your analysis logic.
public class StubAnalysisService : IAnalysisService
{
    private readonly IServiceProvider _services;

    public StubAnalysisService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task ProcessAnalysisJobAsync(string jobId)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
        var job = await db.AiFaqAnalysisJobs.FindAsync(jobId);
        if (job == null) return;

        job.Status = "processing";
        job.StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SaveChangesAsync();

        // Simulate analysis: generate 3 candidate FAQs
        for (int i = 0; i < 3; i++)
        {
            var cand = new ARCompletions.Domain.FaqCandidate
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorId = job.VendorId,
                AnalysisJobId = job.Id,
                Question = $"Sample question {i+1} for {job.JobNo}",
                Answer = $"Sample answer {i+1}",
                ConfidenceScore = 0.7 + i * 0.1,
                Status = "candidate",
                GeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            db.FaqCandidates.Add(cand);
        }

        job.CandidateCount = await db.FaqCandidates.CountAsync(c => c.AnalysisJobId == job.Id);
        job.Status = "finished";
        job.FinishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SaveChangesAsync();
    }
}
