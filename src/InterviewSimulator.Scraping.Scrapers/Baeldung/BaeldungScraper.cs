using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Baeldung;

/// <summary>
/// Scraper para Baeldung — referencia de alta calidad para Java y Spring.
/// Artículos tutoriales + preguntas de entrevista con respuestas detalladas.
/// URLs: https://www.baeldung.com/[tech]-interview-questions
/// </summary>
public class BaeldungScraper : BaseScraper
{
    private const string BaseUrl = "https://www.baeldung.com";

    private static readonly Dictionary<string, (string Category, string Tech)> ArticleSlugs = new()
    {
        { "java-interview-questions",                          ("java",         "java") },
        { "spring-interview-questions",                        ("spring-boot",  "spring-boot") },
        { "spring-boot-interview-questions",                   ("spring-boot",  "spring-boot") },
        { "java-collections-interview-questions",              ("java",         "java") },
        { "java-concurrency-interview-questions",              ("java",         "java-concurrency") },
        { "hibernate-interview-questions",                     ("java",         "hibernate") },
        { "jpa-interview-questions",                           ("java",         "jpa") },
        { "java-8-interview-questions",                        ("java",         "java") },
        { "java-string-interview-questions",                   ("java",         "java") },
        { "java-oop-interview-questions",                      ("oop",          "oop") },
        { "java-database-interview-questions",                 ("database",     "database") },
        { "microservices-interview-questions",                 ("microservices","microservices") },
        { "design-patterns-interview-questions",               ("design-patterns","design-patterns") },
        { "solid-principles",                                  ("design-patterns","solid") },
        { "rest-api-interview-questions",                      ("rest-api",     "rest-api") },
        { "algorithm-interview-questions",                     ("algorithms",   "algorithms") },
        { "data-structures-interview-questions",               ("data-structures","data-structures") },
        { "sql-interview-questions",                           ("sql",          "sql") },
        { "maven-interview-questions",                         ("devops",       "maven") },
        { "docker-interview-questions",                        ("docker",       "docker") },
        { "git-interview-questions",                           ("git",          "git") },
    };

    public override string SourceName => "Baeldung";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public BaeldungScraper(
        ILogger<BaeldungScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Baeldung] Iniciando scraping — {Count} artículos objetivo", ArticleSlugs.Count);

        foreach (var (slug, (category, tech)) in ArticleSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}";
            try
            {
                int count = await ScrapeArticleAsync(url, category, tech, result, cancellationToken);
                Logger.LogInformation("[Baeldung] {Tech}: {Count} preguntas — {Url}", tech, count, url);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[Baeldung] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[Baeldung] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        result.TotalDocumentsFound = result.Documents.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[Baeldung] Completado — {Total} preguntas, {Docs} documentos en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, result.TotalDocumentsFound, sw.Elapsed);
        return result;
    }

    private async Task<int> ScrapeArticleAsync(
        string url, string category, string tech, ScrapingResult result, CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Baeldung: contenido en article.post o div.entry-content
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//article[contains(@class,'post')]") ??
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'entry-content')]") ??
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main");

        if (contentNode == null) return 0;

        foreach (var garbage in contentNode.SelectNodes(".//script|.//style|.//nav|.//aside|.//header|.//footer|.//figure")?.ToList() ?? new List<HtmlNode>())
            garbage.Remove();

        var qaPairs = ExtractQuestionsWithAnswersFromText(contentNode.InnerHtml);
        int count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (questionText, answerText) in qaPairs)
        {
            if (!seen.Add(questionText.ToLowerInvariant())) continue;
            if (answerText.Length < 30) continue;

            var q = CreateScrapedQuestion(questionText, url, null, sourceId: 0, answerText: answerText);
            q.Category = QuestionCategory.Technical;
            q.Technology = tech;
            q.Subcategory = category;
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"baeldung\",\"java\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        // Extraer documentos RAG del contenido del artículo
        var slug = url.Split('/').Last(s => !string.IsNullOrEmpty(s));
        var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? tech;
        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "Baeldung", sourceId: 0,
            technology: slug.Contains("java") ? "Java" : slug.Contains("spring") ? "Spring Boot" : null,
            contentType: ContentType.Article);
        result.Documents.AddRange(docs);

        return count;
    }
}
