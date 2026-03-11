using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.StackOverflow;

/// <summary>
/// Scraper de StackOverflow — preguntas más votadas por tags técnicos.
/// Extrae tanto Q&A como documentos de conocimiento de las respuestas aceptadas.
/// </summary>
public class StackOverflowScraper : BaseScraper
{
    public override string SourceName => "StackOverflow";
    public override SourceType SourceType => SourceType.Forum;

    private static readonly (string Tag, string Tech)[] TopTags =
    [
        ("c%23", "C#"), ("java", "Java"), ("python", "Python"),
        ("javascript", "JavaScript"), ("typescript", "TypeScript"),
        ("react", "React"), ("angular", "Angular"), ("node.js", "Node.js"),
        ("docker", "Docker"), ("kubernetes", "Kubernetes"),
        ("sql", "SQL"), ("postgresql", "PostgreSQL"), ("mongodb", "MongoDB"),
        ("asp.net-core", "ASP.NET Core"), (".net", ".NET"),
        ("entity-framework-core", "Entity Framework"),
        ("spring-boot", "Spring Boot"), ("django", "Django"),
        ("design-patterns", "Design Patterns"), ("rest", "REST API"),
        ("git", "Git"), ("aws", "AWS"), ("azure", "Azure"),
        ("microservices", "Microservices"), ("redis", "Redis"),
        ("linux", "Linux"),
    ];

    public StackOverflowScraper(
        ILogger<StackOverflowScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapingResult();
        Logger.LogInformation("[{Source}] Iniciando scraping de StackOverflow", SourceName);

        foreach (var (tag, tech) in TopTags)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await ScrapeTagQuestionsAsync(tag, tech, result, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[{Source}] Error scrapeando tag: {Tag}", SourceName, tag);
                result.Errors.Add($"Error en tag {tag}: {ex.Message}");
            }
        }

        result.TotalDocumentsFound = result.Documents.Count;
        result.TotalQuestionsFound = result.Questions.Count;
        Logger.LogInformation("[{Source}] Completado: {Docs} docs, {QA} Q&A",
            SourceName, result.Documents.Count, result.Questions.Count);

        return result;
    }

    private async Task ScrapeTagQuestionsAsync(string tag, string technology, ScrapingResult result, CancellationToken ct)
    {
        const int maxPages = 2;

        for (int page = 1; page <= maxPages; page++)
        {
            if (ct.IsCancellationRequested) break;

            // Top questions by votes
            var listUrl = $"https://stackoverflow.com/questions/tagged/{tag}?tab=votes&page={page}&pagesize=15";

            var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
            request.Headers.Add("User-Agent", GetRandomUserAgent());

            var response = await HttpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) break;

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extraer links de preguntas
            var questionLinks = doc.DocumentNode.SelectNodes(
                "//h3[contains(@class,'s-post-summary--content-title')]//a/@href" +
                " | //a[contains(@class,'question-hyperlink')]/@href");

            if (questionLinks == null || questionLinks.Count == 0) break;

            var hrefs = questionLinks
                .Select(a => a.GetAttributeValue("href", ""))
                .Where(h => h.StartsWith("/questions/") && !h.Contains("/tagged/"))
                .Distinct()
                .Take(10)
                .ToList();

            foreach (var href in hrefs)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var questionUrl = $"https://stackoverflow.com{href}";
                    await ScrapeQuestionPageAsync(questionUrl, technology, result, ct);
                    await ApplyRateLimitAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "[{Source}] Error en pregunta: {Href}", SourceName, href);
                }
            }

            await ApplyRateLimitAsync(ct);
        }
    }

    private async Task ScrapeQuestionPageAsync(string url, string technology, ScrapingResult result, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GetRandomUserAgent());

        var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var questionTitle = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'question-hyperlink')] | //h1//a")
            ?.InnerText?.Trim() ?? "";

        var questionBody = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'s-prose') and ancestor::div[contains(@class,'question')]]")
            ?? doc.DocumentNode.SelectSingleNode("//div[@id='question']//div[contains(@class,'js-post-body')]");

        // Respuesta aceptada o más votada
        var acceptedAnswer = doc.DocumentNode.SelectSingleNode(
            "//div[contains(@class,'accepted-answer')]//div[contains(@class,'s-prose')]")
            ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'answer')]//div[contains(@class,'s-prose')]");

        // Extraer Q&A
        if (!string.IsNullOrWhiteSpace(questionTitle) && acceptedAnswer != null)
        {
            var answer = acceptedAnswer.InnerText?.Trim() ?? "";
            if (answer.Length >= 50)
            {
                var q = CreateScrapedQuestion(questionTitle, url, null, 0, answer);
                result.Questions.Add(q);
            }
        }

        // Extraer documentos RAG de la respuesta aceptada (especialmente si es detallada)
        if (acceptedAnswer != null)
        {
            var combinedHtml = $"<h1>{System.Net.WebUtility.HtmlEncode(questionTitle)}</h1>{acceptedAnswer.InnerHtml}";
            if (questionBody != null)
                combinedHtml = $"<h1>{System.Net.WebUtility.HtmlEncode(questionTitle)}</h1>{questionBody.InnerHtml}<h2>Answer</h2>{acceptedAnswer.InnerHtml}";

            var docs = ExtractDocumentsFromHtml(
                combinedHtml, questionTitle, url, "StackOverflow",
                sourceId: 0, technology: technology, contentType: ContentType.Article);
            result.Documents.AddRange(docs);
        }
    }
}
