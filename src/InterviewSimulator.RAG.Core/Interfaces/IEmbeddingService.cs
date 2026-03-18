using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Genera embeddings para chunks de texto usando OpenAI.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Genera embeddings para una lista de chunks.
    /// </summary>
    Task<List<EmbeddedChunk>> GenerateEmbeddingsAsync(
        List<TextChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera embedding para un texto individual (usado por retrieval para la query).
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
