using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Interfaces;

/// <summary>
/// Contrato para el repositorio de datos de scraping (persistencia).
/// Soporta tanto preguntas Q&A como documentos RAG.
/// </summary>
public interface IScrapedDataRepository
{
    // ══ Preguntas Q&A (legacy) ══
    Task<ScrapedQuestion?> GetQuestionByHashAsync(string hashFingerprint);
    Task<bool> QuestionExistsByHashAsync(string hashFingerprint);
    Task<ScrapedQuestion> AddQuestionAsync(ScrapedQuestion question);
    Task<List<ScrapedQuestion>> AddQuestionsAsync(List<ScrapedQuestion> questions);
    Task<int> GetTotalQuestionsCountAsync();
    Task<int> GetQuestionsByCategoryCountAsync(QuestionCategory category);
    Task<List<ScrapedQuestion>> GetByTechnologyAsync(string technology, int take = 50);
    Task<List<ScrapedQuestion>> GetQuestionsWithAnswersAsync(string? technology = null, QuestionCategory? category = null, int take = 50);
    Task<List<ScrapedQuestion>> SearchQuestionsAsync(
        string? technology = null,
        QuestionCategory? category = null,
        DifficultyLevel? difficulty = null,
        bool? hasAnswer = null,
        int skip = 0,
        int take = 50);
    Task<List<string>> GetAvailableTechnologiesAsync();

    // ══ Documentos RAG (corpus) ══
    Task<ScrapedDocument?> GetDocumentByHashAsync(string hashFingerprint);
    Task<bool> DocumentExistsByHashAsync(string hashFingerprint);
    Task<ScrapedDocument> AddDocumentAsync(ScrapedDocument document);
    Task<List<ScrapedDocument>> AddDocumentsAsync(List<ScrapedDocument> documents);
    Task<int> GetTotalDocumentsCountAsync();
    Task<int> GetDocumentsByCategoryCountAsync(ContentCategory category);
    Task<int> GetDocumentsByContentTypeCountAsync(ContentType contentType);
    Task<List<ScrapedDocument>> SearchDocumentsAsync(
        string? technology = null,
        ContentCategory? category = null,
        ContentType? contentType = null,
        DifficultyLevel? difficulty = null,
        string? language = null,
        int skip = 0,
        int take = 50);
    Task<List<string>> GetAvailableDocumentTechnologiesAsync();

    // ══ Fuentes y Jobs ══
    Task<ScrapedSource> GetOrCreateSourceAsync(string name, string baseUrl, SourceType type);
    Task<ScrapingJob> CreateJobAsync(ScrapingJob job);
    Task UpdateJobAsync(ScrapingJob job);
    Task<List<ScrapedSource>> GetActiveSourcesAsync();
    Task UpdateSourceLastScrapedAsync(int sourceId, DateTime scrapedAt);

    // ══ Backward compatibility aliases ══
    Task<ScrapedQuestion?> GetByHashAsync(string hashFingerprint) => GetQuestionByHashAsync(hashFingerprint);
    Task<bool> ExistsByHashAsync(string hashFingerprint) => QuestionExistsByHashAsync(hashFingerprint);
}
