using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Divide un documento limpio en chunks semánticos.
/// </summary>
public interface IChunkingService
{
    Task<List<TextChunk>> ChunkAsync(CleanedDocument document);
}
