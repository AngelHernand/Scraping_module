using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.DigitalOcean;

/// <summary>
/// Scraper de DigitalOcean Community Tutorials — guías técnicas de alta calidad
/// sobre DevOps, Linux, Docker, Kubernetes, bases de datos, programación, etc.
/// </summary>
public class DigitalOceanScraper : BaseScraper
{
    public override string SourceName => "DigitalOcean";
    public override SourceType SourceType => SourceType.EducationalPlatform;

    private static readonly (string Tag, string Tech)[] Tags =
    [
        ("docker", "Docker"),
        ("kubernetes", "Kubernetes"),
        ("linux-basics", "Linux"),
        ("nginx", "Nginx"),
        ("node-js", "Node.js"),
        ("python", "Python"),
        ("mysql", "MySQL"),
        ("postgresql", "PostgreSQL"),
        ("mongodb", "MongoDB"),
        ("redis", "Redis"),
        ("git", "Git"),
        ("ci-cd", "CI/CD"),
        ("react", "React"),
        ("vue-js", "Vue.js"),
        ("typescript", "TypeScript"),
        ("java", "Java"),
        ("go", "Go"),
        ("security", "Security"),
        ("api", "REST API"),
        ("microservices", "Microservices"),
    ];

    public DigitalOceanScraper(
        ILogger<DigitalOceanScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapingResult();
        Logger.LogInformation("[{Source}] Iniciando scraping de DigitalOcean Tutorials", SourceName);

        foreach (var (tag, tech) in Tags)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await ScrapeTagAsync(tag, tech, result, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[{Source}] Error scrapeando tag: {Tag}", SourceName, tag);
                result.Errors.Add($"Error en tag {tag}: {ex.Message}");
            }
        }

        result.TotalDocumentsFound = result.Documents.Count;
        result.TotalQuestionsFound = result.Questions.Count;
        Logger.LogInformation("[{Source}] Completado: {Docs} documentos", SourceName, result.Documents.Count);

        return result;
    }

    private async Task ScrapeTagAsync(string tag, string technology, ScrapingResult result, CancellationToken ct)
    {
        const int maxPages = 3;

        for (int page = 1; page <= maxPages; page++)
        {
            if (ct.IsCancellationRequested) break;

            var listUrl = $"https://www.digitalocean.com/community/tags/{tag}?type=tutorials&page={page}";

            var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
            request.Headers.Add("User-Agent", GetRandomUserAgent());

            var response = await HttpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) break;

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extraer links de tutoriales
            var articleLinks = doc.DocumentNode.SelectNodes(
                "//a[contains(@href,'/community/tutorials/')]/@href");

            if (articleLinks == null || articleLinks.Count == 0) break;

            var urls = articleLinks
                .Select(a => a.GetAttributeValue("href", ""))
                .Where(h => h.Contains("/community/tutorials/") && !h.Contains("?"))
                .Distinct()
                .Take(10)
                .ToList();

            foreach (var relativeUrl in urls)
            {
                if (ct.IsCancellationRequested) break;

                var articleUrl = relativeUrl.StartsWith("http")
                    ? relativeUrl
                    : $"https://www.digitalocean.com{relativeUrl}";

                try
                {
                    await ScrapeArticleAsync(articleUrl, technology, result, ct);
                    await ApplyRateLimitAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "[{Source}] Error en artículo: {Url}", SourceName, articleUrl);
                }
            }

            await ApplyRateLimitAsync(ct);
        }
    }

    private async Task ScrapeArticleAsync(string url, string technology, ScrapingResult result, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GetRandomUserAgent());

        var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content-body')]")
            ?? doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//main");

        if (contentNode == null) return;

        // Limpiar
        var garbage = contentNode.SelectNodes(
            ".//div[contains(@class,'author')] | .//div[contains(@class,'share')] | .//nav | .//aside");
        if (garbage != null)
            foreach (var g in garbage) g.Remove();

        var pageTitle = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim() ?? "DigitalOcean Tutorial";

        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "DigitalOcean",
            sourceId: 0, technology: technology, contentType: ContentType.Tutorial);
        result.Documents.AddRange(docs);

        // También extraer Q&A si hay
        var qa = ExtractQuestionsWithAnswersFromText(contentNode.InnerHtml);
        foreach (var (q, a) in qa)
        {
            var question = CreateScrapedQuestion(q, url, null, 0, a);
            result.Questions.Add(question);
        }
    }
}
