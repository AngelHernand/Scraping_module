using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.Scraping.Data.Repositories;

/// <summary>
/// Repositorio para operaciones de persistencia del módulo de scraping.
/// Soporta tanto preguntas Q&A como documentos RAG del corpus.
/// </summary>
public class ScrapedDataRepository : IScrapedDataRepository
{
    private readonly ScrapingDbContext _context;
    private readonly ILogger<ScrapedDataRepository> _logger;

    public ScrapedDataRepository(ScrapingDbContext context, ILogger<ScrapedDataRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════
    //  Preguntas Q&A
    // ══════════════════════════════════════════════════════════════

    public async Task<ScrapedQuestion?> GetQuestionByHashAsync(string hashFingerprint)
    {
        return await _context.ScrapedQuestions
            .FirstOrDefaultAsync(q => q.HashFingerprint == hashFingerprint);
    }

    public async Task<bool> QuestionExistsByHashAsync(string hashFingerprint)
    {
        return await _context.ScrapedQuestions
            .AnyAsync(q => q.HashFingerprint == hashFingerprint);
    }

    public async Task<ScrapedQuestion> AddQuestionAsync(ScrapedQuestion question)
    {
        _context.ScrapedQuestions.Add(question);
        await _context.SaveChangesAsync();
        return question;
    }

    public async Task<List<ScrapedQuestion>> AddQuestionsAsync(List<ScrapedQuestion> questions)
    {
        if (questions.Count == 0) return questions;

        _context.ScrapedQuestions.AddRange(questions);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Insertadas {Count} preguntas nuevas en BD", questions.Count);
        return questions;
    }

    public async Task<int> GetTotalQuestionsCountAsync()
    {
        return await _context.ScrapedQuestions.CountAsync(q => q.IsActive && !q.IsDuplicate);
    }

    public async Task<int> GetQuestionsByCategoryCountAsync(QuestionCategory category)
    {
        return await _context.ScrapedQuestions
            .CountAsync(q => q.Category == category && q.IsActive && !q.IsDuplicate);
    }

    public async Task<List<ScrapedQuestion>> GetByTechnologyAsync(string technology, int take = 50)
    {
        return await _context.ScrapedQuestions
            .Where(q => q.IsActive && !q.IsDuplicate && q.Technology == technology)
            .OrderByDescending(q => q.ScrapedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<ScrapedQuestion>> GetQuestionsWithAnswersAsync(string? technology = null, QuestionCategory? category = null, int take = 50)
    {
        var query = _context.ScrapedQuestions
            .Where(q => q.IsActive && !q.IsDuplicate && q.AnswerText != null);

        if (!string.IsNullOrEmpty(technology))
            query = query.Where(q => q.Technology == technology);

        if (category.HasValue)
            query = query.Where(q => q.Category == category.Value);

        return await query
            .OrderByDescending(q => q.ScrapedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<ScrapedQuestion>> SearchQuestionsAsync(
        string? technology = null,
        QuestionCategory? category = null,
        DifficultyLevel? difficulty = null,
        bool? hasAnswer = null,
        int skip = 0,
        int take = 50)
    {
        var query = _context.ScrapedQuestions
            .Where(q => q.IsActive && !q.IsDuplicate);

        if (!string.IsNullOrEmpty(technology))
            query = query.Where(q => q.Technology == technology);

        if (category.HasValue)
            query = query.Where(q => q.Category == category.Value);

        if (difficulty.HasValue)
            query = query.Where(q => q.DifficultyLevel == difficulty.Value);

        if (hasAnswer == true)
            query = query.Where(q => q.AnswerText != null);
        else if (hasAnswer == false)
            query = query.Where(q => q.AnswerText == null);

        return await query
            .OrderByDescending(q => q.ScrapedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<string>> GetAvailableTechnologiesAsync()
    {
        return await _context.ScrapedQuestions
            .Where(q => q.IsActive && !q.IsDuplicate && q.Technology != null)
            .Select(q => q.Technology!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════════
    //  Documentos RAG (corpus)
    // ══════════════════════════════════════════════════════════════

    public async Task<ScrapedDocument?> GetDocumentByHashAsync(string hashFingerprint)
    {
        return await _context.ScrapedDocuments
            .FirstOrDefaultAsync(d => d.HashFingerprint == hashFingerprint);
    }

    public async Task<bool> DocumentExistsByHashAsync(string hashFingerprint)
    {
        return await _context.ScrapedDocuments
            .AnyAsync(d => d.HashFingerprint == hashFingerprint);
    }

    public async Task<ScrapedDocument> AddDocumentAsync(ScrapedDocument document)
    {
        _context.ScrapedDocuments.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<List<ScrapedDocument>> AddDocumentsAsync(List<ScrapedDocument> documents)
    {
        if (documents.Count == 0) return documents;

        _context.ScrapedDocuments.AddRange(documents);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Insertados {Count} documentos RAG nuevos en BD", documents.Count);
        return documents;
    }

    public async Task<int> GetTotalDocumentsCountAsync()
    {
        return await _context.ScrapedDocuments.CountAsync(d => d.IsActive && !d.IsDuplicate);
    }

    public async Task<int> GetDocumentsByCategoryCountAsync(ContentCategory category)
    {
        return await _context.ScrapedDocuments
            .CountAsync(d => d.Category == category && d.IsActive && !d.IsDuplicate);
    }

    public async Task<int> GetDocumentsByContentTypeCountAsync(ContentType contentType)
    {
        return await _context.ScrapedDocuments
            .CountAsync(d => d.ContentType == contentType && d.IsActive && !d.IsDuplicate);
    }

    public async Task<List<ScrapedDocument>> SearchDocumentsAsync(
        string? technology = null,
        ContentCategory? category = null,
        ContentType? contentType = null,
        DifficultyLevel? difficulty = null,
        string? language = null,
        int skip = 0,
        int take = 50)
    {
        var query = _context.ScrapedDocuments
            .Where(d => d.IsActive && !d.IsDuplicate);

        if (!string.IsNullOrEmpty(technology))
            query = query.Where(d => d.Technology == technology);

        if (category.HasValue)
            query = query.Where(d => d.Category == category.Value);

        if (contentType.HasValue)
            query = query.Where(d => d.ContentType == contentType.Value);

        if (difficulty.HasValue)
            query = query.Where(d => d.Difficulty == difficulty.Value);

        if (!string.IsNullOrEmpty(language))
            query = query.Where(d => d.Language == language);

        return await query
            .OrderByDescending(d => d.ScrapedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<string>> GetAvailableDocumentTechnologiesAsync()
    {
        return await _context.ScrapedDocuments
            .Where(d => d.IsActive && !d.IsDuplicate && d.Technology != null)
            .Select(d => d.Technology!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════════
    //  Fuentes y Jobs (compartido)
    // ══════════════════════════════════════════════════════════════

    public async Task<ScrapedSource> GetOrCreateSourceAsync(string name, string baseUrl, SourceType type)
    {
        var existing = await _context.ScrapedSources
            .FirstOrDefaultAsync(s => s.Name == name);

        if (existing != null)
            return existing;

        var source = new ScrapedSource
        {
            Name = name,
            BaseUrl = baseUrl,
            SourceType = type,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ScrapedSources.Add(source);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Fuente creada: {Name} ({BaseUrl})", name, baseUrl);
        return source;
    }

    public async Task<ScrapingJob> CreateJobAsync(ScrapingJob job)
    {
        _context.ScrapingJobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task UpdateJobAsync(ScrapingJob job)
    {
        _context.ScrapingJobs.Update(job);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ScrapedSource>> GetActiveSourcesAsync()
    {
        return await _context.ScrapedSources
            .Where(s => s.IsActive)
            .ToListAsync();
    }

    public async Task UpdateSourceLastScrapedAsync(int sourceId, DateTime scrapedAt)
    {
        var source = await _context.ScrapedSources.FindAsync(sourceId);
        if (source != null)
        {
            source.LastScrapedAt = scrapedAt;
            await _context.SaveChangesAsync();
        }
    }
}
