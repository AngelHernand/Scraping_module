using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Interacción con la base de datos vectorial (Qdrant).
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Almacena chunks con sus embeddings en Qdrant. Retorna los point IDs asignados.
    /// </summary>
    Task<List<Guid>> UpsertAsync(
        List<EmbeddedChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca los chunks más similares a un vector query con filtros opcionales.
    /// </summary>
    Task<List<(string PointId, float Score, Dictionary<string, object> Payload)>> SearchAsync(
        float[] queryVector,
        Dictionary<string, object>? filters = null,
        List<int>? excludeScrapedQuestionIds = null,
        int topK = 10,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina puntos por ScrapedQuestionId (para reprocesar).
    /// </summary>
    Task DeleteByScrapedQuestionIdAsync(
        int scrapedQuestionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene estadísticas de la colección.
    /// </summary>
    Task<(long PointCount, long SegmentCount)> GetCollectionStatsAsync(
        CancellationToken cancellationToken = default);
}
