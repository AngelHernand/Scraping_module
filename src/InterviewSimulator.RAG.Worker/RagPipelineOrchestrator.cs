using System.Diagnostics;
using System.Text.Json;
using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Constants;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;
using InterviewSimulator.RAG.Processing.Enrichment;
using InterviewSimulator.RAG.VectorStore;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.RAG.Worker;

public class RagPipelineOrchestrator : IRagPipelineOrchestrator
{
    private readonly ITextCleaner _textCleaner;
    private readonly IChunkingService _chunkingService;
    private readonly ChunkMetadataEnricher _enricher;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly QdrantCollectionManager _collectionManager;
    private readonly IProcessingStatusRepository _statusRepository;
    private readonly ScrapingDbContext _scrapingDb;
    private readonly RagPipelineSettings _settings;
    private readonly OpenAISettings _openAiSettings;
    private readonly QdrantSettings _qdrantSettings;
    private readonly ILogger<RagPipelineOrchestrator> _logger;

    public RagPipelineOrchestrator(
        ITextCleaner textCleaner,
        IChunkingService chunkingService,
        ChunkMetadataEnricher enricher,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        QdrantCollectionManager collectionManager,
        IProcessingStatusRepository statusRepository,
        ScrapingDbContext scrapingDb,
        IOptions<RagPipelineSettings> settings,
        IOptions<OpenAISettings> openAiSettings,
        IOptions<QdrantSettings> qdrantSettings,
        ILogger<RagPipelineOrchestrator> logger)
    {
        _textCleaner = textCleaner;
        _chunkingService = chunkingService;
        _enricher = enricher;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _collectionManager = collectionManager;
        _statusRepository = statusRepository;
        _scrapingDb = scrapingDb;
        _settings = settings.Value;
        _openAiSettings = openAiSettings.Value;
        _qdrantSettings = qdrantSettings.Value;
        _logger = logger;
    }

    public async Task<PipelineResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new PipelineResult();

        try
        {
            // Asegurar que la colección exista
            await _collectionManager.EnsureCollectionExistsAsync(cancellationToken);

            // Obtener IDs no procesados
            var unprocessedIds = await _statusRepository.GetUnprocessedScrapedQuestionIdsAsync(
                _settings.ProcessingBatchSize);

            // También obtener los que fallaron y aún tienen reintentos disponibles
            var failedStatuses = await _statusRepository.GetPendingOrFailedAsync(
                _settings.MaxRetryCount, _settings.ProcessingBatchSize);

            var allIds = unprocessedIds
                .Union(failedStatuses.Select(s => s.ScrapedQuestionId))
                .Distinct()
                .ToList();

            if (allIds.Count == 0)
            {
                _logger.LogInformation("No pending ScrapedQuestions to process");
                sw.Stop();
                result.Duration = sw.Elapsed;
                return result;
            }

            _logger.LogInformation("Processing {Count} ScrapedQuestions", allIds.Count);

            // Cargar las ScrapedQuestions
            var questions = await _scrapingDb.ScrapedQuestions
                .Include(q => q.Source)
                .Where(q => allIds.Contains(q.Id) && q.IsActive && !q.IsDuplicate)
                .ToListAsync(cancellationToken);

            foreach (var question in questions)
            {
                try
                {
                    await ProcessSingleAsync(question, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"ScrapedQuestionId {question.Id}: {ex.Message}");
                    _logger.LogError(ex, "Failed to process ScrapedQuestionId {Id}", question.Id);

                    await UpdateStatusOnError(question.Id, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution failed");
            result.Errors.Add($"Pipeline error: {ex.Message}");
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.TotalProcessed = result.Successful + result.Failed + result.Skipped;

        // Calcular costo estimado
        result.EstimatedCostUSD = result.TotalTokensEmbedded *
            (decimal)RagConstants.CostPerMillionTokensSmall / 1_000_000m;

        _logger.LogInformation(
            "Pipeline completed: {Processed} processed, {Successful} successful, {Failed} failed, {Skipped} skipped, {Chunks} chunks, {Cost:F6} USD in {Duration}",
            result.TotalProcessed, result.Successful, result.Failed, result.Skipped,
            result.TotalChunksGenerated, result.EstimatedCostUSD, result.Duration);

        return result;
    }

    public async Task<PipelineResult> ReprocessAsync(int scrapedQuestionId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new PipelineResult();

        try
        {
            await _collectionManager.EnsureCollectionExistsAsync(cancellationToken);

            // Eliminar puntos existentes en Qdrant
            await _vectorStoreService.DeleteByScrapedQuestionIdAsync(scrapedQuestionId, cancellationToken);

            var question = await _scrapingDb.ScrapedQuestions
                .Include(q => q.Source)
                .FirstOrDefaultAsync(q => q.Id == scrapedQuestionId, cancellationToken);

            if (question == null)
            {
                result.Errors.Add($"ScrapedQuestion {scrapedQuestionId} not found");
                result.Failed = 1;
            }
            else
            {
                await ProcessSingleAsync(question, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result.Failed++;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Reprocess failed for ScrapedQuestionId {Id}", scrapedQuestionId);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.TotalProcessed = 1;
        return result;
    }

    private async Task ProcessSingleAsync(
        Scraping.Core.Models.ScrapedQuestion question,
        PipelineResult result,
        CancellationToken cancellationToken)
    {
        // Obtener o crear el status de procesamiento
        var status = await _statusRepository.GetByScrapedQuestionIdAsync(question.Id);
        if (status == null)
        {
            status = await _statusRepository.CreateAsync(new ProcessingStatus
            {
                ScrapedQuestionId = question.Id,
                State = ProcessingState.Pending,
                EmbeddingModel = _openAiSettings.EmbeddingModel,
                QdrantCollectionName = _qdrantSettings.CollectionName
            });
        }

        // Step 1: Limpiar
        string rawContent = question.RawContent ?? question.AnswerText ?? question.QuestionText;
        string sourceName = question.Source?.Name ?? string.Empty;

        var cleaned = await _textCleaner.CleanAsync(rawContent, question.QuestionText, sourceName);
        cleaned.ScrapedQuestionId = question.Id;

        if (!cleaned.HasSufficientContent)
        {
            await _statusRepository.UpdateStateAsync(status.Id, ProcessingState.Skipped,
                "Insufficient content after cleaning");
            result.Skipped++;
            return;
        }

        await _statusRepository.UpdateStateAsync(status.Id, ProcessingState.Cleaned);

        // Step 2: Chunk
        var chunks = await _chunkingService.ChunkAsync(cleaned);
        if (chunks.Count == 0)
        {
            await _statusRepository.UpdateStateAsync(status.Id, ProcessingState.Skipped,
                "No valid chunks generated");
            result.Skipped++;
            return;
        }

        await _statusRepository.UpdateStateAsync(status.Id, ProcessingState.Chunked);

        // Step 3: Enrich
        chunks = _enricher.Enrich(chunks, question);

        // Step 4: Embed
        var embeddedChunks = await _embeddingService.GenerateEmbeddingsAsync(chunks, cancellationToken);
        await _statusRepository.UpdateStateAsync(status.Id, ProcessingState.Embedded);

        // Step 5: Store in Qdrant
        var pointIds = await _vectorStoreService.UpsertAsync(embeddedChunks, cancellationToken);

        // Step 6: Update tracking
        status.State = ProcessingState.Stored;
        status.ChunksGenerated = chunks.Count;
        status.QdrantPointIds = JsonSerializer.Serialize(pointIds);
        status.ProcessedAt = DateTime.UtcNow;
        status.ErrorMessage = null;
        await _statusRepository.UpdateAsync(status);

        // Acumular resultados
        int totalTokens = chunks.Sum(c => c.TokenCount);
        result.Successful++;
        result.TotalChunksGenerated += chunks.Count;
        result.TotalTokensEmbedded += totalTokens;

        _logger.LogDebug("Processed ScrapedQuestionId {Id}: {Chunks} chunks, {Tokens} tokens",
            question.Id, chunks.Count, totalTokens);
    }

    private async Task UpdateStatusOnError(int scrapedQuestionId, string errorMessage)
    {
        try
        {
            var status = await _statusRepository.GetByScrapedQuestionIdAsync(scrapedQuestionId);
            if (status != null)
            {
                status.State = ProcessingState.Failed;
                status.ErrorMessage = errorMessage;
                status.RetryCount++;
                await _statusRepository.UpdateAsync(status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update error status for ScrapedQuestionId {Id}", scrapedQuestionId);
        }
    }
}
