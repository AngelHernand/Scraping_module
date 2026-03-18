using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.FreeCodeCamp;

/// <summary>
/// Scraper para FreeCodeCamp en Español e Inglés.
/// Usa HttpClient + HtmlAgilityPack (contenido estático, sin JavaScript).
/// </summary>
public class FreeCodeCampScraper : BaseScraper
{
    private const string BaseUrlEs = "https://www.freecodecamp.org/espanol/news";
    private const string BaseUrlEn = "https://www.freecodecamp.org/news";

    // Páginas de búsqueda con términos relevantes para entrevistas técnicas
    private static readonly string[] SearchUrls =
    {
        // Español
        $"{BaseUrlEs}/search/?query=preguntas+entrevista",
        $"{BaseUrlEs}/search/?query=entrevista+programacion",
        $"{BaseUrlEs}/search/?query=preguntas+tecnicas",
        $"{BaseUrlEs}/search/?query=entrevista+desarrollador",
        $"{BaseUrlEs}/search/?query=preguntas+javascript",
        $"{BaseUrlEs}/search/?query=preguntas+python",
        $"{BaseUrlEs}/search/?query=preguntas+sql",
        $"{BaseUrlEs}/search/?query=preguntas+react",
        $"{BaseUrlEs}/search/?query=algoritmos+estructuras+datos",
        $"{BaseUrlEs}/search/?query=preparar+entrevista+tecnica",
        // Inglés
        $"{BaseUrlEn}/search/?query=interview+questions+programming",
        $"{BaseUrlEn}/search/?query=coding+interview",
        $"{BaseUrlEn}/search/?query=technical+interview+questions",
        $"{BaseUrlEn}/search/?query=javascript+interview",
        $"{BaseUrlEn}/search/?query=python+interview",
        $"{BaseUrlEn}/search/?query=sql+interview+questions",
        $"{BaseUrlEn}/search/?query=react+interview",
        $"{BaseUrlEn}/search/?query=algorithms+data+structures"
    };

    // URLs directas de artículos conocidos con preguntas de entrevista
    private static readonly string[] DirectArticleUrls =
    {
        // Español
        $"{BaseUrlEs}/preguntas-comunes-en-entrevistas-tecnicas/",
        $"{BaseUrlEs}/preguntas-tipicas-de-entrevista-de-javascript/",
        $"{BaseUrlEs}/preguntas-de-entrevista-de-python/",
        $"{BaseUrlEs}/preguntas-de-entrevista-de-react/",
        $"{BaseUrlEs}/preguntas-de-entrevista-de-sql/",
        $"{BaseUrlEs}/preguntas-de-entrevista-sobre-css/",
        $"{BaseUrlEs}/preguntas-de-entrevista-sobre-html/",
        $"{BaseUrlEs}/preguntas-de-entrevistas-de-angular/",
        $"{BaseUrlEs}/preguntas-de-entrevista-de-java/",
        // Inglés
        $"{BaseUrlEn}/javascript-interview-questions-and-answers/",
        $"{BaseUrlEn}/python-interview-questions-and-answers/",
        $"{BaseUrlEn}/react-interview-questions-and-answers/",
        $"{BaseUrlEn}/sql-interview-questions/",
        $"{BaseUrlEn}/css-interview-questions-and-answers/",
        $"{BaseUrlEn}/html-interview-questions-and-answers/",
        $"{BaseUrlEn}/java-interview-questions-and-answers/",
        $"{BaseUrlEn}/common-coding-interview-questions/",
        $"{BaseUrlEn}/software-engineering-interview-questions/"
    };

    public override string SourceName => "FreeCodeCamp";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public FreeCodeCampScraper(
        ILogger<FreeCodeCampScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var processedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Logger.LogInformation("[FreeCodeCamp] Iniciando scraping bilingüe (ES + EN)...");

        try
        {
            // 1. Primero procesar artículos directos (más confiables)
            foreach (var url in DirectArticleUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    if (processedUrls.Contains(url)) continue;
                    processedUrls.Add(url);

                    await ProcessArticleAsync(url, result, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logger.LogDebug("[FreeCodeCamp] Artículo no encontrado (404): {Url}", url);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[FreeCodeCamp] Error procesando artículo directo: {Url}", url);
                    result.Errors.Add($"Error en {url}: {ex.Message}");
                }
            }

            // 2. Luego buscar artículos por búsqueda (FreeCodeCamp search es client-side con Ghost API)
            foreach (var searchUrl in SearchUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var articleUrls = await DiscoverArticlesFromSearchAsync(searchUrl, cancellationToken);
                    Logger.LogInformation("[FreeCodeCamp] Búsqueda '{Url}': {Count} artículos encontrados", searchUrl, articleUrls.Count);

                    foreach (var articleUrl in articleUrls)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (processedUrls.Contains(articleUrl)) continue;
                        processedUrls.Add(articleUrl);

                        try
                        {
                            await ProcessArticleAsync(articleUrl, result, cancellationToken);
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            Logger.LogDebug("[FreeCodeCamp] Artículo no encontrado: {Url}", articleUrl);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "[FreeCodeCamp] Error en artículo: {Url}", articleUrl);
                            result.Errors.Add($"Error en {articleUrl}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[FreeCodeCamp] Error en búsqueda: {Url}", searchUrl);
                    result.Errors.Add($"Error en búsqueda {searchUrl}: {ex.Message}");
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
            result.TotalDocumentsFound = result.Documents.Count;
            Logger.LogInformation("[FreeCodeCamp] Scraping completado. {Count} preguntas, {Docs} documentos de {Articles} artículos",
                result.TotalQuestionsFound, result.TotalDocumentsFound, processedUrls.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[FreeCodeCamp] Error fatal durante scraping");
            result.Success = false;
            result.Errors.Add($"Error fatal: {ex.Message}");
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private async Task ProcessArticleAsync(string url, ScrapingResult result, CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html");

        var response = await HttpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Extraer el contenido del artículo (FreeCodeCamp usa <article> y <section class="post-content">)
        var articleNode = doc.DocumentNode.SelectSingleNode("//article") ??
                          doc.DocumentNode.SelectSingleNode("//section[contains(@class,'post-content')]") ??
                          doc.DocumentNode.SelectSingleNode("//main");

        if (articleNode == null)
        {
            Logger.LogDebug("[FreeCodeCamp] Sin contenido de artículo en: {Url}", url);
            return;
        }

        var articleHtml = articleNode.InnerHtml;

        // Extraer solo preguntas CON respuestas
        var qaPairs = ExtractQuestionsWithAnswersFromText(articleHtml);

        foreach (var (questionText, answerText) in qaPairs)
        {
            var scrapedQuestion = CreateScrapedQuestion(
                questionText,
                url,
                articleHtml,
                sourceId: 0,
                answerText: answerText
            );
            // CreateScrapedQuestion ya detecta el idioma automáticamente
            result.Questions.Add(scrapedQuestion);
        }

        // Extraer documentos RAG del contenido del artículo
        var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "FreeCodeCamp";
        var docs = ExtractDocumentsFromHtml(
            articleHtml, pageTitle, url, "FreeCodeCamp", sourceId: 0,
            technology: null,
            contentType: ContentType.Article);
        result.Documents.AddRange(docs);

        Logger.LogDebug("[FreeCodeCamp] '{Url}': {QA} preguntas con respuesta, {Docs} documentos",
            url, qaPairs.Count, docs.Count);
    }

    private async Task<List<string>> DiscoverArticlesFromSearchAsync(string searchUrl, CancellationToken ct)
    {
        var urls = new List<string>();

        try
        {
            // FreeCodeCamp search usa Ghost Content API internamente
            // Intentamos obtener la página de búsqueda y extraer los links
            await ApplyRateLimitAsync(ct);

            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
            HttpClient.DefaultRequestHeaders.Add("Accept", "text/html");

            var response = await HttpClient.GetAsync(searchUrl, ct);
            if (!response.IsSuccessStatusCode) return urls;

            var html = await response.Content.ReadAsStringAsync(ct);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Buscar links a artículos dentro de los resultados de búsqueda
            var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (linkNodes == null) return urls;

            foreach (var link in linkNodes)
            {
                var href = link.GetAttributeValue("href", "");

                // Solo enlaces a artículos de FreeCodeCamp (Español o Inglés)
                if (string.IsNullOrEmpty(href)) continue;

                // Normalizar URL
                if ((href.StartsWith("/espanol/news/") || href.StartsWith("/news/")) &&
                    !href.Contains("/search/") && !href.Contains("/tag/") && !href.Contains("/page/"))
                {
                    var fullUrl = $"https://www.freecodecamp.org{href}";
                    if (!urls.Contains(fullUrl))
                        urls.Add(fullUrl);
                }
                else if ((href.StartsWith("https://www.freecodecamp.org/espanol/news/") ||
                          href.StartsWith("https://www.freecodecamp.org/news/")) &&
                         !href.Contains("/search/") && !href.Contains("/tag/") && !href.Contains("/page/"))
                {
                    if (!urls.Contains(href))
                        urls.Add(href);
                }
            }

            // También intentar el Ghost Content API directamente (más confiable para búsqueda)
            // FreeCodeCamp usa Ghost y expone posts API
            var searchTerm = searchUrl.Contains("query=")
                ? searchUrl.Split("query=").Last().Replace("+", " ")
                : "";

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var ghostUrls = await SearchGhostApiAsync(searchTerm, ct);
                foreach (var url in ghostUrls)
                {
                    if (!urls.Contains(url))
                        urls.Add(url);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FreeCodeCamp] Error en búsqueda: {Url}", searchUrl);
        }

        return urls;
    }

    private async Task<List<string>> SearchGhostApiAsync(string searchTerm, CancellationToken ct)
    {
        var urls = new List<string>();

        try
        {
            // Ghost Content API público de FreeCodeCamp
            // Buscar en tags de entrevistas en español e inglés
            var tagUrls = new[]
            {
                $"{BaseUrlEs}/tag/entrevistas/",
                $"{BaseUrlEn}/tag/interview/",
                $"{BaseUrlEn}/tag/interviews/"
            };

            foreach (var tagUrl in tagUrls)
            {
                try
                {
                    await ApplyRateLimitAsync(ct);

                    HttpClient.DefaultRequestHeaders.Clear();
                    HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());

                    var response = await HttpClient.GetAsync(tagUrl, ct);
                    if (!response.IsSuccessStatusCode) continue;

                    var html = await response.Content.ReadAsStringAsync(ct);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
                    if (linkNodes == null) continue;

                    foreach (var link in linkNodes)
                    {
                        var href = link.GetAttributeValue("href", "");
                        if ((href.StartsWith("/espanol/news/") || href.StartsWith("/news/")) &&
                            !href.Contains("/tag/") && !href.Contains("/page/"))
                        {
                            var fullUrl = $"https://www.freecodecamp.org{href}";
                            if (!urls.Contains(fullUrl))
                                urls.Add(fullUrl);
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Logger.LogDebug(ex2, "[FreeCodeCamp] Error buscando en tag {Url}", tagUrl);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FreeCodeCamp] Error buscando en Ghost API");
        }

        return urls;
    }
}
