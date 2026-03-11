using System.Diagnostics;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers;

/// <summary>
/// Orquestador que coordina la ejecución de todos los scrapers,
/// la clasificación, deduplicación y persistencia de preguntas Q&A y documentos RAG.
/// </summary>
public class ScrapingOrchestrator : IScrapingOrchestrator
{
    private readonly IEnumerable<IScraper> _scrapers;
    private readonly IQuestionClassifier _classifier;
    private readonly IContentClassifier _contentClassifier;
    private readonly IScrapedDataRepository _repository;
    private readonly ScrapingSettings _settings;
    private readonly ILogger<ScrapingOrchestrator> _logger;

    public ScrapingOrchestrator(
        IEnumerable<IScraper> scrapers,
        IQuestionClassifier classifier,
        IContentClassifier contentClassifier,
        IScrapedDataRepository repository,
        IOptions<ScrapingSettings> settings,
        ILogger<ScrapingOrchestrator> logger)
    {
        _scrapers = scrapers;
        _classifier = classifier;
        _contentClassifier = contentClassifier;
        _repository = repository;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrchestratorResult> RunAllScrapersAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var orchestratorResult = new OrchestratorResult();

        _logger.LogInformation("=== INICIO DE SCRAPING ORQUESTADO ===");
        _logger.LogInformation("Scrapers habilitados: {Scrapers}", string.Join(", ", _settings.EnabledScrapers));

        // Obtener fuentes activas
        var activeSources = await _repository.GetActiveSourcesAsync();

        foreach (var scraper in _scrapers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scraping cancelado por el usuario");
                break;
            }

            // Verificar si el scraper está habilitado
            if (!_settings.EnabledScrapers.Contains(scraper.SourceName))
            {
                _logger.LogDebug("Scraper '{Name}' no está habilitado, saltando", scraper.SourceName);
                continue;
            }

            // Verificar si la fuente específica está habilitada en settings
            var scraperConfig = _settings.Scrapers.GetValueOrDefault(scraper.SourceName);
            if (scraperConfig is { Enabled: false })
            {
                _logger.LogDebug("Scraper '{Name}' deshabilitado en configuración", scraper.SourceName);
                continue;
            }

            // Verificar frecuencia de scraping
            var source = activeSources.FirstOrDefault(s => s.Name == scraper.SourceName);
            if (source != null && source.LastScrapedAt.HasValue)
            {
                var frequencyHours = scraperConfig?.FrequencyHours ?? _settings.DefaultFrequencyHours;
                var nextRun = source.LastScrapedAt.Value.AddHours(frequencyHours);
                if (DateTime.UtcNow < nextRun)
                {
                    _logger.LogInformation("Scraper '{Name}' no necesita ejecutarse aún. Próxima ejecución: {Next}",
                        scraper.SourceName, nextRun);
                    continue;
                }
            }

            try
            {
                var scraperResult = await RunSingleScraperAsync(scraper, cancellationToken);
                orchestratorResult.ScraperResults.Add(scraperResult);
                orchestratorResult.TotalSourcesProcessed++;
                orchestratorResult.TotalQuestionsFound += scraperResult.TotalQuestionsFound;
                orchestratorResult.TotalNewQuestions += scraperResult.NewQuestions;
                orchestratorResult.TotalDuplicates += scraperResult.DuplicateQuestions;
                orchestratorResult.TotalDocumentsFound += scraperResult.TotalDocumentsFound;
                orchestratorResult.TotalNewDocuments += scraperResult.NewDocuments;
                orchestratorResult.TotalDuplicateDocuments += scraperResult.DuplicateDocuments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando scraper '{Name}'", scraper.SourceName);
                orchestratorResult.Errors.Add($"Error en {scraper.SourceName}: {ex.Message}");
                orchestratorResult.TotalErrors++;
            }
        }

        sw.Stop();
        orchestratorResult.TotalDuration = sw.Elapsed;

        _logger.LogInformation(
            "=== FIN DE SCRAPING === Fuentes: {Sources} | Preguntas: {Found} nuevas={NewQ} | Documentos: {Docs} nuevos={NewD} | Errores: {Errors} | Duración: {Duration}",
            orchestratorResult.TotalSourcesProcessed,
            orchestratorResult.TotalQuestionsFound,
            orchestratorResult.TotalNewQuestions,
            orchestratorResult.TotalDocumentsFound,
            orchestratorResult.TotalNewDocuments,
            orchestratorResult.TotalErrors,
            orchestratorResult.TotalDuration);

        return orchestratorResult;
    }

    /// <inheritdoc />
    public async Task<ScrapingResult> RunScraperAsync(string sourceName, CancellationToken cancellationToken = default)
    {
        var scraper = _scrapers.FirstOrDefault(s =>
            s.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

        if (scraper == null)
        {
            _logger.LogError("Scraper '{Name}' no encontrado", sourceName);
            return new ScrapingResult
            {
                Success = false,
                Errors = { $"Scraper '{sourceName}' no encontrado" }
            };
        }

        return await RunSingleScraperAsync(scraper, cancellationToken);
    }

    private async Task<ScrapingResult> RunSingleScraperAsync(IScraper scraper, CancellationToken ct)
    {
        _logger.LogInformation("--- Ejecutando scraper: {Name} ---", scraper.SourceName);

        // Obtener o crear la fuente en BD
        var baseUrl = scraper.SourceName switch
        {
            "DevTo" => "https://dev.to",
            "Medium" => "https://medium.com",
            "LeetCode" => "https://leetcode.com",
            "Glassdoor" => "https://www.glassdoor.com.mx",
            "Indeed" => "https://mx.indeed.com",
            "FreeCodeCamp" => "https://www.freecodecamp.org/espanol",
            "GeeksForGeeks" => "https://www.geeksforgeeks.org",
            "InterviewBit" => "https://www.interviewbit.com",
            "FullStackCafe" => "https://github.com/aershov24/full-stack-interview-questions",
            "JavaTPoint" => "https://www.javatpoint.com",
            "TealHQ" => "https://www.tealhq.com",
            "KnowledgeHut" => "https://www.knowledgehut.com",
            "Simplilearn" => "https://www.simplilearn.com",
            "Edureka" => "https://www.edureka.co",
            "CSharpCorner" => "https://www.c-sharpcorner.com",
            "Baeldung" => "https://www.baeldung.com",
            "DotNetTricks" => "https://www.dotnettricks.com",
            "Turing" => "https://www.turing.com",
            "KeepCoding" => "https://keepcoding.io",
            "Platzi" => "https://platzi.com",
            "OpenWebinars" => "https://openwebinars.net",
            "Talently" => "https://talently.tech",
            "Epitech" => "https://www.epitech-it.es",
            "TheBridge" => "https://www.thebridge.tech",
            "EPAMAnywhere" => "https://anywhere.epam.com",
            "MicrosoftLearn" => "https://learn.microsoft.com",
            "MdnWebDocs" => "https://developer.mozilla.org",
            "W3Schools" => "https://www.w3schools.com",
            "RefactoringGuru" => "https://refactoring.guru",
            "DigitalOcean" => "https://www.digitalocean.com/community",
            "PythonDocs" => "https://docs.python.org",
            "StackOverflow" => "https://stackoverflow.com",
            _ => $"https://{scraper.SourceName.ToLowerInvariant()}.com"
        };

        var source = await _repository.GetOrCreateSourceAsync(scraper.SourceName, baseUrl, scraper.SourceType);

        // Crear job de scraping
        var job = new ScrapingJob
        {
            SourceId = source.Id,
            StartedAt = DateTime.UtcNow,
            Status = ScrapingStatus.Running
        };
        job = await _repository.CreateJobAsync(job);

        try
        {
            // Ejecutar el scraper
            var result = await scraper.ScrapeAsync(ct);

            // ── Procesar preguntas Q&A ──
            var (newQuestions, duplicateQuestions) = await ProcessQuestionsAsync(scraper.SourceName, source.Id, result);
            result.NewQuestions = newQuestions;
            result.DuplicateQuestions = duplicateQuestions;

            // ── Procesar documentos RAG ──
            var (newDocuments, duplicateDocuments) = await ProcessDocumentsAsync(scraper.SourceName, source.Id, result);
            result.NewDocuments = newDocuments;
            result.DuplicateDocuments = duplicateDocuments;

            // Actualizar job
            job.FinishedAt = DateTime.UtcNow;
            job.QuestionsFound = result.TotalQuestionsFound;
            job.QuestionsNew = newQuestions;
            job.DocumentsFound = result.TotalDocumentsFound;
            job.DocumentsNew = newDocuments;
            job.Status = result.Errors.Count > 0 ? ScrapingStatus.CompletedWithErrors : ScrapingStatus.Completed;
            job.ErrorMessage = result.Errors.Count > 0 ? string.Join("; ", result.Errors.Take(5)) : null;
            await _repository.UpdateJobAsync(job);

            // Actualizar última ejecución de la fuente
            await _repository.UpdateSourceLastScrapedAsync(source.Id, DateTime.UtcNow);

            _logger.LogInformation(
                "--- Scraper {Name}: Q&A encontradas={QFound} nuevas={QNew} | Docs encontrados={DFound} nuevos={DNew} | Errores={Errors} ---",
                scraper.SourceName, result.TotalQuestionsFound, newQuestions,
                result.TotalDocumentsFound, newDocuments, result.Errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            job.FinishedAt = DateTime.UtcNow;
            job.Status = ScrapingStatus.Failed;
            job.ErrorMessage = ex.Message;
            await _repository.UpdateJobAsync(job);

            _logger.LogError(ex, "Scraper '{Name}' falló", scraper.SourceName);
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Pipeline de procesamiento de Preguntas Q&A
    // ══════════════════════════════════════════════════════════════

    private async Task<(int newCount, int dupCount)> ProcessQuestionsAsync(
        string sourceName, int sourceId, ScrapingResult result)
    {
        int newQuestions = 0;
        int duplicates = 0;
        int skippedNoAnswer = 0;
        int skippedNotIT = 0;
        int skippedNotSpanish = 0;

        foreach (var question in result.Questions)
        {
            try
            {
                question.SourceId = sourceId;

                // FILTRO 1: Solo guardar preguntas que tengan respuesta
                if (string.IsNullOrWhiteSpace(question.AnswerText) || question.AnswerText.Trim().Length < 20)
                {
                    skippedNoAnswer++;
                    continue;
                }

                // FILTRO 2: Solo guardar preguntas en español
                if (question.OriginalLanguage != "es")
                {
                    skippedNotSpanish++;
                    continue;
                }

                // FILTRO 3: Solo guardar preguntas relevantes a IT/desarrollo
                if (!_classifier.IsITRelevant(question.QuestionText, question.AnswerText))
                {
                    skippedNotIT++;
                    continue;
                }

                // Clasificar la pregunta
                var textToClassify = question.QuestionText + " " + question.AnswerText;
                var classification = _classifier.Classify(textToClassify);
                if (question.Category == QuestionCategory.Unknown)
                    question.Category = classification.Category;
                question.Subcategory ??= classification.Subcategory;
                if (question.DifficultyLevel == DifficultyLevel.Unknown)
                    question.DifficultyLevel = classification.Difficulty;
                if (string.IsNullOrEmpty(question.Tags) && classification.Tags.Count > 0)
                    question.Tags = System.Text.Json.JsonSerializer.Serialize(classification.Tags);
                question.Technology ??= classification.Technology;

                // Deduplicación por hash
                if (await _repository.QuestionExistsByHashAsync(question.HashFingerprint))
                {
                    duplicates++;
                    continue;
                }

                await _repository.AddQuestionAsync(question);
                newQuestions++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error procesando pregunta: {Text}",
                    question.QuestionText.Length > 50
                        ? question.QuestionText[..50] + "..."
                        : question.QuestionText);
            }
        }

        if (skippedNoAnswer > 0 || skippedNotIT > 0 || skippedNotSpanish > 0)
        {
            _logger.LogInformation(
                "--- Filtros Q&A {Name}: Sin respuesta={NoAns}, No español={NoEs}, No IT={NotIT} ---",
                sourceName, skippedNoAnswer, skippedNotSpanish, skippedNotIT);
        }

        return (newQuestions, duplicates);
    }

    // ══════════════════════════════════════════════════════════════
    //  Pipeline de procesamiento de Documentos RAG
    // ══════════════════════════════════════════════════════════════

    private async Task<(int newCount, int dupCount)> ProcessDocumentsAsync(
        string sourceName, int sourceId, ScrapingResult result)
    {
        if (result.Documents.Count == 0)
            return (0, 0);

        int newDocuments = 0;
        int duplicates = 0;
        int skippedNotIT = 0;

        foreach (var document in result.Documents)
        {
            try
            {
                document.SourceId = sourceId;

                // FILTRO: Solo contenido relevante a IT/desarrollo
                if (!_contentClassifier.IsITRelevantContent(document.Title, document.Content))
                {
                    skippedNotIT++;
                    continue;
                }

                // Clasificar el documento
                var classification = _contentClassifier.ClassifyDocument(document.Title, document.Content);

                if (document.Category == ContentCategory.Unknown)
                    document.Category = classification.Category;
                document.Subcategory ??= classification.Subcategory;
                if (document.ContentType == ContentType.Unknown)
                    document.ContentType = classification.ContentType;
                if (document.Difficulty == DifficultyLevel.Unknown)
                    document.Difficulty = classification.Difficulty;
                if (string.IsNullOrEmpty(document.Tags) && classification.Tags.Count > 0)
                    document.Tags = System.Text.Json.JsonSerializer.Serialize(classification.Tags);
                document.Technology ??= classification.Technology;
                document.ClassificationConfidence = classification.ConfidenceScore;

                // Deduplicación por hash
                if (await _repository.DocumentExistsByHashAsync(document.HashFingerprint))
                {
                    duplicates++;
                    document.IsDuplicate = true;
                    continue;
                }

                await _repository.AddDocumentAsync(document);
                newDocuments++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error procesando documento: {Title}",
                    document.Title.Length > 60
                        ? document.Title[..60] + "..."
                        : document.Title);
            }
        }

        if (skippedNotIT > 0)
        {
            _logger.LogInformation("--- Filtros DOC {Name}: No IT={NotIT} ---", sourceName, skippedNotIT);
        }

        _logger.LogInformation(
            "--- Documentos RAG {Name}: Total={Total}, Nuevos={New}, Duplicados={Dups} ---",
            sourceName, result.Documents.Count, newDocuments, duplicates);

        return (newDocuments, duplicates);
    }
}
