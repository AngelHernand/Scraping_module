using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.RAG.Processing.Chunking;

public class ChunkingService : IChunkingService
{
    private readonly InterviewQuestionChunker _questionChunker;
    private readonly RecursiveTextChunker _recursiveChunker;
    private readonly ILogger<ChunkingService> _logger;

    private const int MinQuestionsForSpecializedChunker = 3;

    public ChunkingService(
        InterviewQuestionChunker questionChunker,
        RecursiveTextChunker recursiveChunker,
        ILogger<ChunkingService> logger)
    {
        _questionChunker = questionChunker;
        _recursiveChunker = recursiveChunker;
        _logger = logger;
    }

    public Task<List<TextChunk>> ChunkAsync(CleanedDocument document)
    {
        int detectedQuestions = _questionChunker.CountDetectedQuestions(document.CleanedText);
        List<TextChunk> chunks;

        if (detectedQuestions >= MinQuestionsForSpecializedChunker)
        {
            _logger.LogDebug("Using InterviewQuestionChunker ({Count} questions detected) for ScrapedQuestionId {Id}",
                detectedQuestions, document.ScrapedQuestionId);
            chunks = _questionChunker.Chunk(document);
        }
        else
        {
            _logger.LogDebug("Using RecursiveTextChunker ({Count} questions detected) for ScrapedQuestionId {Id}",
                detectedQuestions, document.ScrapedQuestionId);
            chunks = _recursiveChunker.Chunk(document);
        }

        _logger.LogInformation("Chunked ScrapedQuestionId {Id}: {Count} chunks generated",
            document.ScrapedQuestionId, chunks.Count);

        return Task.FromResult(chunks);
    }
}
