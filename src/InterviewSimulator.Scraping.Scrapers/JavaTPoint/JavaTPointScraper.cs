using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.JavaTPoint;

/// <summary>
/// Scraper para JavaTPoint — uno de los tutoriales técnicos más grandes de India.
/// Amplia cobertura de Java, .NET, SQL y tecnologías backend.
/// URLs: https://www.javatpoint.com/[tech]-interview-questions
/// Todo el contenido es server-side, compatible con HtmlAgilityPack.
/// </summary>
public class JavaTPointScraper : BaseScraper
{
    private const string BaseUrl = "https://www.javatpoint.com";

    private static readonly Dictionary<string, (string Category, string Tech)> TechSlugs = new()
    {
        { "java-interview-questions",         ("java",           "java") },
        { "python-interview-questions",       ("python",         "python") },
        { "sql-interview-questions",          ("sql",            "sql") },
        { "javascript-interview-questions",   ("javascript",     "javascript") },
        { "c-sharp-interview-questions",      ("csharp",         "csharp") },
        { "asp-net-interview-questions",      ("aspnet-core",    "aspnet") },
        { "dot-net-interview-questions",      ("dotnet",         "dotnet") },
        { "reactjs-interview-questions",      ("react",          "react") },
        { "angular-interview-questions",      ("angular",        "angular") },
        { "nodejs-interview-questions",       ("nodejs",         "nodejs") },
        { "mongodb-interview-questions",      ("mongodb",        "mongodb") },
        { "mysql-interview-questions",        ("mysql",          "mysql") },
        { "postgresql-interview-questions",   ("postgresql",     "postgresql") },
        { "docker-interview-questions",       ("docker",         "docker") },
        { "kubernetes-interview-questions",   ("kubernetes",     "kubernetes") },
        { "git-interview-questions",          ("git",            "git") },
        { "linux-interview-questions",        ("linux",          "linux") },
        { "aws-interview-questions",          ("aws",            "aws") },
        { "spring-interview-questions",       ("spring-boot",    "spring-boot") },
        { "hibernate-interview-questions",    ("java",           "hibernate") },
        { "servlet-interview-questions",      ("java",           "servlet") },
        { "oops-interview-questions",         ("oop",            "oop") },
        { "data-structure-interview-questions",("data-structures","data-structures") },
        { "dbms-interview-questions",         ("database",       "dbms") },
        { "php-interview-questions",          ("php",            "php") },
        { "html-interview-questions",         ("frontend",       "html") },
        { "css-interview-questions",          ("frontend",       "css") },
        { "typescript-interview-questions",   ("typescript",     "typescript") },
        { "microservices-interview-questions",("microservices",  "microservices") },
        { "restful-web-services-interview-questions", ("rest-api","rest-api") },
    };

    public override string SourceName => "JavaTPoint";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public JavaTPointScraper(
        ILogger<JavaTPointScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var cfg = Settings.Scrapers.GetValueOrDefault("JavaTPoint");
        var maxPages = cfg?.MaxPages ?? Settings.MaxPagesPerSource;

        Logger.LogInformation("[JavaTPoint] Iniciando scraping — {Count} tecnologías", TechSlugs.Count);

        foreach (var (slug, (category, tech)) in TechSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}";
            try
            {
                int count = await ScrapeInterviewPageAsync(url, category, tech, result, maxPages, cancellationToken);
                Logger.LogInformation("[JavaTPoint] {Tech}: {Count} preguntas — {Url}", tech, count, url);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[JavaTPoint] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[JavaTPoint] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[JavaTPoint] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    // ---------------------------------------------------------------------------
    // Scraping de una página de JavaTPoint con soporte de paginación
    // JavaTPoint divide artículos largos en múltiples páginas:
    //   page 1: /java-interview-questions
    //   page 2: /java-interview-questions2
    //   page 3: /java-interview-questions3 ...
    // ---------------------------------------------------------------------------
    private async Task<int> ScrapeInterviewPageAsync(
        string baseUrl,
        string category,
        string tech,
        ScrapingResult result,
        int maxPages,
        CancellationToken ct)
    {
        int total = 0;

        for (int page = 1; page <= maxPages; page++)
        {
            if (ct.IsCancellationRequested) break;

            await ApplyRateLimitAsync(ct);

            // JavaTPoint usa sufijos numéricos: page2, page3...
            var url = page == 1 ? baseUrl : $"{baseUrl}{page}";

            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
            HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            HttpResponseMessage response;
            try
            {
                response = await HttpClient.GetAsync(url, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                break; // Sin más páginas
            }

            if (!response.IsSuccessStatusCode) break;

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extraer nodo de contenido principal (JavaTPoint usa div.onpageDiv)
            var contentNode =
                doc.DocumentNode.SelectSingleNode("//div[contains(@class,'onpageDiv')]") ??
                doc.DocumentNode.SelectSingleNode("//div[contains(@id,'city')]") ??
                doc.DocumentNode.SelectSingleNode("//div[contains(@class,'main-div')]") ??
                doc.DocumentNode.SelectSingleNode("//article") ??
                doc.DocumentNode.SelectSingleNode("//main");

            if (contentNode == null) break;

            // Limpiar contenido de sidebar y anuncios
            foreach (var garbage in contentNode
                .SelectNodes(".//script|.//style|.//aside|.//*[contains(@class,'advertisement')]|.//*[contains(@class,'sidebar')]")
                ?.ToList() ?? new List<HtmlNode>())
            {
                garbage.Remove();
            }

            var contentHtml = contentNode.InnerHtml;
            var qaPairs = ExtractQuestionsWithAnswersFromText(contentHtml);

            int pageCount = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (questionText, answerText) in qaPairs)
            {
                if (!seen.Add(questionText.ToLowerInvariant())) continue;
                if (answerText.Length < 30) continue;

                var q = CreateScrapedQuestion(questionText, url, null, sourceId: 0, answerText: answerText);
                q.Category = QuestionCategory.Technical;
                q.Technology = tech;
                q.Subcategory = category;
                q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"javatpoint\"]";
                q.OriginalLanguage = "en";

                result.Questions.Add(q);
                pageCount++;
            }

            total += pageCount;

            // Si página 1 no da resultados, no tiene sentido continuar paginando
            if (pageCount == 0 && page == 1) break;

            // Si la página actual está vacía, no hay más páginas
            if (pageCount == 0) break;

            // Verificar si hay enlace "Next" (algunas páginas no tienen numeración)
            bool hasNext = doc.DocumentNode
                .SelectNodes("//a[contains(text(),'Next') or contains(@href,'2') or contains(@href,'next')]")
                ?.Any() ?? false;

            if (!hasNext && page >= 3) break; // Límite conservador sin paginación detectada
        }

        return total;
    }
}
