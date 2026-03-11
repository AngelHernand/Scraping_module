using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.DevTo;

// Scraper para Dev.to usando su API REST pública (Forem API).
// Fuente más confiable.
public class DevToScraper : BaseScraper
{
    private const string BaseApiUrl = "https://dev.to/api/articles";

    // Solo tags en español / comunidad hispana en Dev.to
    private static readonly string[] PrimaryTags =
    {
        "spanish", "espanol", "programacion", "desarrollo",
        "entrevista", "preguntas", "tutorial",
        "aprendizaje", "tecnologia", "webdev",
        "codenewbie", "beginners" // tags comunes donde hay contenido en español
    };

    // Sin tags secundarios en inglés — evita descargar miles de artículos irrelevantes
    private static readonly string[] SecondaryTags = Array.Empty<string>();

    // Solo procesa artículos detectados como español
    private const bool SpanishOnly = true;

    public override string SourceName => "DevTo";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public DevToScraper(
        ILogger<DevToScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var scraperSettings = Settings.Scrapers.GetValueOrDefault("DevTo");
        var maxPages = scraperSettings?.MaxPages ?? Settings.MaxPagesPerSource;

        Logger.LogInformation("[DevTo] Iniciando scraping...");

        try
        {
            var allTags = PrimaryTags.Concat(SecondaryTags).ToArray();

            foreach (var tag in allTags)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    Logger.LogInformation("[DevTo] Buscando artículos con tag: {Tag}", tag);
                    int spanishFoundInTag = 0;
                    int consecutiveEmptyPages = 0;

                    for (int page = 1; page <= maxPages; page++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var articles = await FetchArticlesByTagAsync(tag, page, cancellationToken);
                        if (articles == null || articles.Count == 0) break;

                        int spanishInPage = 0;

                        foreach (var article in articles)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            try
                            {
                                // Pre-filtro rápido por título/descripción (evita descargar el body completo)
                                if (SpanishOnly)
                                {
                                    var preview = $"{article.Title} {article.Description}";
                                    if (!IsSpanishText(preview))
                                    {
                                        Logger.LogDebug("[DevTo] Artículo '{Title}' descartado rápido (título no es español)", article.Title);
                                        continue;
                                    }
                                }

                                // Obtener body completo del artículo
                                await ApplyRateLimitAsync(cancellationToken);
                                var fullArticle = await FetchFullArticleAsync(article.Id, cancellationToken);

                                if (fullArticle?.BodyHtml == null) continue;

                                // Filtrar por idioma: solo español si SpanishOnly está activo
                                if (SpanishOnly && !IsSpanishText(fullArticle.BodyHtml))
                                {
                                    Logger.LogDebug("[DevTo] Artículo '{Title}' descartado (no es español)", article.Title);
                                    continue;
                                }

                                spanishInPage++;

                                // Extraer solo preguntas CON respuestas del contenido
                                var qaPairs = ExtractQuestionsWithAnswersFromText(fullArticle.BodyHtml);

                                foreach (var (questionText, answerText) in qaPairs)
                                {
                                    var scrapedQuestion = CreateScrapedQuestion(
                                        questionText,
                                        article.Url,
                                        fullArticle.BodyHtml,
                                        sourceId: 0, // Se asigna en el orquestador
                                        answerText: answerText
                                    );

                                    result.Questions.Add(scrapedQuestion);
                                }

                                // Extraer documentos RAG del contenido del artículo
                                var techHint = article.TagList?.FirstOrDefault();
                                var articleDocs = ExtractDocumentsFromHtml(
                                    fullArticle.BodyHtml, article.Title, article.Url, "DevTo", sourceId: 0,
                                    technology: techHint,
                                    contentType: ContentType.Article);
                                result.Documents.AddRange(articleDocs);

                                Logger.LogDebug("[DevTo] Artículo '{Title}': {QACount} preguntas con respuesta, {Docs} documentos",
                                    article.Title, qaPairs.Count, articleDocs.Count);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning(ex, "[DevTo] Error procesando artículo {Id}", article.Id);
                                result.Errors.Add($"Error en artículo {article.Id}: {ex.Message}");
                            }
                        }

                        // Rate limit de Dev.to: 30 requests por 30 segundos
                        await Task.Delay(1500, cancellationToken);

                        // Corte inteligente: si una página no tuvo artículos en español, incrementar contador
                        if (spanishInPage == 0)
                            consecutiveEmptyPages++;
                        else
                            consecutiveEmptyPages = 0;

                        spanishFoundInTag += spanishInPage;

                        // Si 2 páginas consecutivas sin contenido español, pasar al siguiente tag
                        if (consecutiveEmptyPages >= 2)
                        {
                            Logger.LogInformation("[DevTo] Tag '{Tag}' sin contenido español en {Pages} páginas consecutivas, saltando", tag, consecutiveEmptyPages);
                            break;
                        }
                    }

                    Logger.LogInformation("[DevTo] Tag '{Tag}': {Count} artículos en español encontrados", tag, spanishFoundInTag);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[DevTo] Error en tag {Tag}", tag);
                    result.Errors.Add($"Error en tag '{tag}': {ex.Message}");
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
            result.TotalDocumentsFound = result.Documents.Count;
            Logger.LogInformation("[DevTo] Scraping completado. {Count} preguntas, {Docs} documentos encontrados", result.TotalQuestionsFound, result.TotalDocumentsFound);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[DevTo] Error fatal durante scraping");
            result.Success = false;
            result.Errors.Add($"Error fatal: {ex.Message}");
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private async Task<List<DevToArticleSummary>?> FetchArticlesByTagAsync(string tag, int page, CancellationToken ct)
    {
        var url = $"{BaseApiUrl}?tag={tag}&per_page=100&page={page}";
        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await HttpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<List<DevToArticleSummary>>(options, ct);
    }

    private async Task<DevToArticleFull?> FetchFullArticleAsync(long articleId, CancellationToken ct)
    {
        var url = $"{BaseApiUrl}/{articleId}";
        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await HttpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<DevToArticleFull>(options, ct);
    }

    #region DTO Models

    private class DevToArticleSummary
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("tag_list")]
        public List<string>? TagList { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("positive_reactions_count")]
        public int PositiveReactionsCount { get; set; }
    }

    private class DevToArticleFull
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("body_html")]
        public string? BodyHtml { get; set; }

        [JsonPropertyName("body_markdown")]
        public string? BodyMarkdown { get; set; }

        public string Url { get; set; } = string.Empty;
    }

    #endregion
}
