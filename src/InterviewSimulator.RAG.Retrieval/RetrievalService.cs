using System.Diagnostics;
using InterviewSimulator.RAG.Core.Constants;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.RAG.Retrieval;

public class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILogger<RetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Generar embedding de la query
        string queryText = QueryBuilder.BuildQueryText(query);
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken);

        // 2. Construir filtros
        var filters = QueryBuilder.BuildFilters(query);

        // 3. Buscar en Qdrant
        var searchResults = await _vectorStoreService.SearchAsync(
            queryVector,
            filters.Count > 0 ? filters : null,
            query.ExcludeScrapedQuestionIds,
            query.TopK,
            query.MinScore,
            cancellationToken);

        // 4. Mapear resultados
        var chunks = searchResults.Select(r => MapToRetrievedChunk(r.Payload, r.Score)).ToList();

        // 5. Deduplicar por ScrapedQuestionId (max N chunks por fuente)
        chunks = RetrievalReranker.DeduplicateBySource(chunks, RagConstants.MaxChunksPerSource);

        sw.Stop();

        _logger.LogInformation("Retrieved {Count} chunks in {Duration}ms for query: {Query}",
            chunks.Count, sw.ElapsedMilliseconds, query.QueryText[..Math.Min(100, query.QueryText.Length)]);

        return new RetrievalResult
        {
            Chunks = chunks,
            TotalFound = searchResults.Count,
            SearchDuration = sw.Elapsed
        };
    }

    public async Task<string> RetrieveAndAssembleContextAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await RetrieveAsync(query, cancellationToken);
        return ContextAssembler.Assemble(result.Chunks);
    }

    private static RetrievedChunk MapToRetrievedChunk(Dictionary<string, object> payload, float score)
    {
        return new RetrievedChunk
        {
            Text = GetString(payload, "text"),
            QuestionText = GetString(payload, "question_text"),
            Category = GetString(payload, "category"),
            Subcategory = GetString(payload, "subcategory"),
            DifficultyLevel = GetString(payload, "difficulty_level"),
            SourceName = GetString(payload, "source_name"),
            SourceUrl = GetString(payload, "source_url"),
            Language = GetString(payload, "original_language"),
            ChunkType = GetString(payload, "chunk_type"),
            Tags = GetStringList(payload, "tags"),
            SimilarityScore = score
        };
    }

    private static string GetString(Dictionary<string, object> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    private static List<string> GetStringList(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value)) return new List<string>();

        if (value is List<object> list)
            return list.Select(x => x?.ToString() ?? string.Empty).ToList();

        if (value is string s && !string.IsNullOrEmpty(s))
            return new List<string> { s };

        return new List<string>();
    }
}
