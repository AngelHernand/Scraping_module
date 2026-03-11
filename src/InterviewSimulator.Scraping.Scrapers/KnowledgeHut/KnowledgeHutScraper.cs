using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.KnowledgeHut;

/// <summary>
/// Scraper para KnowledgeHut — plataforma de capacitación con buenas colecciones
/// de preguntas de entrevista técnica estructuradas por tecnología.
/// URLs: https://www.knowledgehut.com/interview-questions/[tech]
/// </summary>
public class KnowledgeHutScraper : BaseScraper
{
    private const string BaseUrl = "https://www.knowledgehut.com/interview-questions";

    private static readonly Dictionary<string, (string Category, string Tech)> TechSlugs = new()
    {
        { "backend-developer",       ("backend",        "backend") },
        { "java",                    ("java",           "java") },
        { "python",                  ("python",         "python") },
        { "javascript",              ("javascript",     "javascript") },
        { "react",                   ("react",          "react") },
        { "angular",                 ("angular",        "angular") },
        { "node-js",                 ("nodejs",         "nodejs") },
        { "sql",                     ("sql",            "sql") },
        { "mongodb",                 ("mongodb",        "mongodb") },
        { "docker",                  ("docker",         "docker") },
        { "kubernetes",              ("kubernetes",     "kubernetes") },
        { "aws",                     ("aws",            "aws") },
        { "devops",                  ("devops",         "devops") },
        { "system-design",           ("system-design",  "system-design") },
        { "data-structures",         ("data-structures","data-structures") },
        { "microservices",           ("microservices",  "microservices") },
        { "spring-boot",             ("spring-boot",    "spring-boot") },
        { "csharp",                  ("csharp",         "csharp") },
        { "dotnet",                  ("dotnet",         "dotnet") },
        { "git",                     ("git",            "git") },
    };

    public override string SourceName => "KnowledgeHut";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public KnowledgeHutScraper(
        ILogger<KnowledgeHutScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[KnowledgeHut] Iniciando scraping — {Count} tecnologías", TechSlugs.Count);

        foreach (var (slug, (category, tech)) in TechSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}";
            try
            {
                int count = await ScrapePageAsync(url, category, tech, result, cancellationToken);
                Logger.LogInformation("[KnowledgeHut] {Tech}: {Count} preguntas", tech, count);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[KnowledgeHut] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[KnowledgeHut] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[KnowledgeHut] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    private async Task<int> ScrapePageAsync(
        string url, string category, string tech, ScrapingResult result, CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // KnowledgeHut: artículo en section or article con FAQ accordion
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'interview-questions')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'faq')]") ??
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main") ??
            doc.DocumentNode.SelectSingleNode("//section[contains(@class,'content')]");

        if (contentNode == null) return 0;

        foreach (var garbage in contentNode.SelectNodes(".//script|.//style|.//nav|.//aside|.//header|.//footer")?.ToList() ?? new List<HtmlNode>())
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"knowledgehut\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
