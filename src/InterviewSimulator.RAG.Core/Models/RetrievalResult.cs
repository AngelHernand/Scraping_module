namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Resultado de una búsqueda de retrieval en Qdrant.
/// </summary>
public class RetrievalResult
{
    public List<RetrievedChunk> Chunks { get; set; } = new();
    public int TotalFound { get; set; }
    public TimeSpan SearchDuration { get; set; }
}

/// <summary>
/// Un chunk recuperado de Qdrant con su score de similitud.
/// </summary>
public class RetrievedChunk
{
    public string Text { get; set; } = string.Empty;
    public string? QuestionText { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string DifficultyLevel { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public float SimilarityScore { get; set; }
    public string ChunkType { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
