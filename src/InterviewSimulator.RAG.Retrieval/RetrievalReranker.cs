using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Retrieval;

public static class RetrievalReranker
{
    /// <summary>
    /// Deduplica chunks limitando a maxChunksPerSource por SourceName para diversidad.
    /// Los chunks ya vienen ordenados por score descendente desde Qdrant.
    /// </summary>
    public static List<RetrievedChunk> DeduplicateBySource(List<RetrievedChunk> chunks, int maxChunksPerSource)
    {
        var sourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RetrievedChunk>();

        foreach (var chunk in chunks)
        {
            string sourceKey = chunk.SourceName;
            if (string.IsNullOrEmpty(sourceKey))
                sourceKey = "__unknown__";

            sourceCounts.TryGetValue(sourceKey, out int count);
            if (count >= maxChunksPerSource)
                continue;

            sourceCounts[sourceKey] = count + 1;
            result.Add(chunk);
        }

        return result;
    }
}
