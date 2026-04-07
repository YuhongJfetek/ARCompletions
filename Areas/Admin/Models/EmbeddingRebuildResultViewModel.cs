using System;
using System.Collections.Generic;
using ARCompletions.Domain;

namespace ARCompletions.Areas.Admin.Models;

public class EmbeddingRebuildResultViewModel
{
    public BotEmbeddingJob Job { get; set; } = new BotEmbeddingJob();
    public IReadOnlyList<BotFaqEmbedding> Embeddings { get; set; } = Array.Empty<BotFaqEmbedding>();
}
