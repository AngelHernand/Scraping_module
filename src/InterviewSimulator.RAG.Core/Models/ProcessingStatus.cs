using InterviewSimulator.RAG.Core.Models.Enums;

namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Entidad de tracking: registra el estado de procesamiento de cada ScrapedQuestion en el pipeline RAG.
/// </summary>
public class ProcessingStatus
{
    public int Id { get; set; }
    public int ScrapedQuestionId { get; set; }
    public ProcessingState State { get; set; } = ProcessingState.Pending;
    public int ChunksGenerated { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? QdrantCollectionName { get; set; }
    public string? QdrantPointIds { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
