using System.Diagnostics;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.GeeksForGeeks;

/// <summary>
/// Scraper para GeeksforGeeks — una de las fuentes más densas de preguntas
/// técnicas de entrevista. Navega páginas de interview-questions por tecnología.
/// Renderizado server-side, compatible con HtmlAgilityPack.
/// </summary>
public class GeeksForGeeksScraper : BaseScraper
{
    private const string BaseUrl = "https://www.geeksforgeeks.org";

    // Mapa de slug → (categoría semántica, tecnología)
    private static readonly Dictionary<string, (string Category, string Tech)> TechSlugs = new()
    {
        { "backend-developer-interview-questions-and-answers",   ("backend",            "backend") },
        { "javascript-interview-questions-and-answers",          ("javascript",         "javascript") },
        { "python-interview-questions",                          ("python",             "python") },
        { "java-interview-questions",                            ("java",               "java") },
        { "sql-interview-questions",                             ("sql",                "sql") },
        { "mongodb-interview-questions",                         ("mongodb",            "mongodb") },
        { "node-interview-questions",                            ("nodejs",             "nodejs") },
        { "express-interview-questions",                         ("expressjs",          "expressjs") },
        { "django-interview-questions",                          ("django",             "django") },
        { "reactjs-interview-questions-and-answers",             ("react",              "react") },
        { "angular-interview-questions-and-answers",             ("angular",            "angular") },
        { "spring-interview-questions",                          ("spring-boot",        "spring") },
        { "php-interview-questions",                             ("php",                "php") },
        { "rest-api-interview-questions",                        ("rest-api",           "rest-api") },
        { "system-design-interview-questions-and-answers",       ("system-design",      "system-design") },
        { "data-structures-interview-questions-and-answers",     ("data-structures",    "data-structures") },
        { "algorithm-interview-questions-and-answers",           ("algorithms",         "algorithms") },
        { "operating-systems-interview-questions",               ("operating-systems",  "os") },
        { "computer-network-interview-questions",                ("networking",         "networking") },
        { "dbms-interview-questions-answers",                    ("database",           "dbms") },
        { "oops-interview-questions-and-answers",                ("oop",                "oop") },
        { "docker-interview-questions",                          ("docker",             "docker") },
        { "kubernetes-interview-questions-and-answers",          ("kubernetes",         "kubernetes") },
        { "aws-interview-questions",                             ("aws",                "aws") },
        { "azure-interview-questions-and-answers",               ("azure",              "azure") },
        { "git-interview-questions",                             ("git",                "git") },
        { "linux-interview-questions",                           ("linux",              "linux") },
        { "typescript-interview-questions",                      ("typescript",         "typescript") },
        { "csharp-interview-questions",                          ("csharp",             "csharp") },
        { "microservices-interview-questions",                   ("microservices",      "microservices") },
    };

    public override string SourceName => "GeeksForGeeks";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public GeeksForGeeksScraper(
        ILogger<GeeksForGeeksScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var pageSettings = Settings.Scrapers.GetValueOrDefault("GeeksForGeeks");
        var maxPages = pageSettings?.MaxPages ?? Settings.MaxPagesPerSource;

        Logger.LogInformation("[GeeksForGeeks] Iniciando scraping — {Count} tecnologías configuradas", TechSlugs.Count);

        foreach (var (slug, (category, tech)) in TechSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Probar las dos variantes de URL comunes en GFG
            var candidateUrls = new[]
            {
                $"{BaseUrl}/{slug}/",
                $"{BaseUrl}/interview-prep/{slug}/"
            };

            foreach (var pageUrl in candidateUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var count = await ScrapePageAsync(pageUrl, category, tech, result, maxPages, cancellationToken);
                    if (count > 0)
                    {
                        Logger.LogInformation("[GeeksForGeeks] {Tech}: {Count} preguntas — {Url}", tech, count, pageUrl);
                        break; // Con éxito, no probar la segunda variante
                    }
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Logger.LogDebug("[GeeksForGeeks] {Url} → {Code}", pageUrl, ex.StatusCode);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[GeeksForGeeks] Error en {Url}", pageUrl);
                    result.Errors.Add($"Error en {pageUrl}: {ex.Message}");
                    break;
                }
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        result.TotalDocumentsFound = result.Documents.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[GeeksForGeeks] Completado — {Total} preguntas, {Docs} documentos en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, result.TotalDocumentsFound, sw.Elapsed);
        return result;
    }

    // ---------------------------------------------------------------------------
    // Scraping de una página individual con paginación interna tipo "?page=N"
    // ---------------------------------------------------------------------------
    private async Task<int> ScrapePageAsync(
        string basePageUrl,
        string category,
        string tech,
        ScrapingResult result,
        int maxPages,
        CancellationToken ct)
    {
        int totalExtracted = 0;

        for (int page = 1; page <= maxPages; page++)
        {
            if (ct.IsCancellationRequested) break;

            await ApplyRateLimitAsync(ct);

            var url = page == 1 ? basePageUrl : $"{basePageUrl}?page={page}";

            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
            HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await HttpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) break;

            var html = await response.Content.ReadAsStringAsync(ct);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var questionsFound = ExtractGfgQaPairs(doc, url, category, tech, result);
            totalExtracted += questionsFound;

            // Si no encontramos nada en la página actual, detener paginación
            if (questionsFound == 0) break;

            // Verificar si existe página siguiente (evitar bucles innecesarios)
            bool hasNextPage = doc.DocumentNode
                .SelectNodes("//a[contains(@class,'page-link') or contains(@href,'?page=')]")
                ?.Any(n => n.InnerText.Trim() == "Next" || n.GetAttributeValue("href", "").Contains($"page={page + 1}"))
                ?? false;

            if (!hasNextPage && page > 1) break;
        }

        return totalExtracted;
    }

    // ---------------------------------------------------------------------------
    // Extracción de pares Q&A del DOM de GeeksForGeeks
    // GFG usa varias estructuras según la antigüedad del artículo:
    //   A) <div class="header-main__content"> → artículo estructurado con h2/h3
    //   B) <article> → post estilo blog
    //   C) Tabla numerada con TH/TD
    // ---------------------------------------------------------------------------
    private int ExtractGfgQaPairs(HtmlDocument doc, string url, string category, string tech, ScrapingResult result)
    {
        int count = 0;

        // Estrategia A: Selecciona el nodo principal del artículo
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'content') and not(contains(@class,'sidebar'))]") ??
            doc.DocumentNode.SelectSingleNode("//main");

        if (contentNode == null) return 0;

        // Eliminar scripts, estilos y sidebar del DOM para no contaminar la extracción
        foreach (var garbage in contentNode.SelectNodes("//script|//style|//nav|//footer|//aside")?.ToList() ?? new List<HtmlNode>())
            garbage.Remove();

        var contentHtml = contentNode.InnerHtml;

        // Usar el extractor base que busca H2/H3 + párrafos, bold + contenido, etc.
        var qaPairs = ExtractQuestionsWithAnswersFromText(contentHtml);

        // Estrategia B: Buscar listas ordenadas de preguntas (GFG usa <ol> en algunos posts)
        var olNodes = contentNode.SelectNodes(".//ol/li")?.ToList() ?? new List<HtmlNode>();
        foreach (var li in olNodes)
        {
            var liHtml = li.InnerHtml;
            var morePairs = ExtractQuestionsWithAnswersFromText(liHtml);
            qaPairs.AddRange(morePairs);
        }

        // Deduplicar dentro de esta página
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (questionText, answerText) in qaPairs)
        {
            if (!seen.Add(questionText.ToLowerInvariant())) continue;
            if (answerText.Length < 30) continue;

            var q = CreateScrapedQuestion(questionText, url, null, sourceId: 0, answerText: answerText);
            q.Category = QuestionCategory.Technical;
            q.Technology = tech;
            q.Subcategory = category;
            q.Tags = BuildTagsJson(tech, category);
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        // Extraer documentos RAG del contenido del artículo
        var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? tech;
        var docs = ExtractDocumentsFromHtml(
            contentHtml, pageTitle, url, "GeeksForGeeks", sourceId: 0,
            technology: tech,
            contentType: ContentType.Article);
        result.Documents.AddRange(docs);

        return count;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------
    private static string BuildTagsJson(string tech, string category)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tech };
        if (!string.Equals(tech, category, StringComparison.OrdinalIgnoreCase))
            tags.Add(category);
        tags.Add("interview");
        tags.Add("geeksforgeeks");
        return $"[\"{string.Join("\",\"", tags)}\"]";
    }
}
