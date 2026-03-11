using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Edureka;

/// <summary>
/// Scraper para Edureka — plataforma de e-learning con artículos extensos
/// de preguntas de entrevista técnica, bien estructurados con H2/H3 + párrafos.
/// URLs: https://www.edureka.co/blog/interview-questions/[tech]-interview-questions/
/// </summary>
public class EdurekaScraper : BaseScraper
{
    private const string BaseUrl = "https://www.edureka.co/blog/interview-questions";

    private static readonly Dictionary<string, (string Category, string Tech)> TechSlugs = new()
    {
        { "java-interview-questions",                ("java",          "java") },
        { "python-interview-questions",              ("python",        "python") },
        { "javascript-interview-questions",          ("javascript",    "javascript") },
        { "reactjs-interview-questions",             ("react",         "react") },
        { "node-js-interview-questions",             ("nodejs",        "nodejs") },
        { "angular-interview-questions",             ("angular",       "angular") },
        { "spring-interview-questions",              ("spring-boot",   "spring-boot") },
        { "sql-interview-questions",                 ("sql",           "sql") },
        { "mongodb-interview-questions",             ("mongodb",       "mongodb") },
        { "mysql-interview-questions",               ("mysql",         "mysql") },
        { "docker-interview-questions",              ("docker",        "docker") },
        { "kubernetes-interview-questions",          ("kubernetes",    "kubernetes") },
        { "aws-interview-questions",                 ("aws",           "aws") },
        { "azure-interview-questions",               ("azure",         "azure") },
        { "devops-interview-questions",              ("devops",        "devops") },
        { "git-interview-questions",                 ("git",           "git") },
        { "linux-interview-questions",               ("linux",         "linux") },
        { "system-design-interview-questions",       ("system-design", "system-design") },
        { "data-structures-interview-questions",     ("data-structures","data-structures") },
        { "microservices-interview-questions",       ("microservices", "microservices") },
        { "oops-interview-questions-and-answers",    ("oop",           "oop") },
        { "design-patterns-interview-questions",     ("design-patterns","design-patterns") },
        { "hibernate-interview-questions",           ("java",          "hibernate") },
        { "rest-api-interview-questions",            ("rest-api",      "rest-api") },
        { "typescript-interview-questions",          ("typescript",    "typescript") },
    };

    public override string SourceName => "Edureka";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public EdurekaScraper(
        ILogger<EdurekaScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Edureka] Iniciando scraping — {Count} tecnologías", TechSlugs.Count);

        foreach (var (slug, (category, tech)) in TechSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}/";
            try
            {
                int count = await ScrapePageAsync(url, category, tech, result, cancellationToken);
                Logger.LogInformation("[Edureka] {Tech}: {Count} preguntas", tech, count);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[Edureka] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[Edureka] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[Edureka] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
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
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        HttpClient.DefaultRequestHeaders.Add("Referer", "https://www.edureka.co/");

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Edureka: contenido en div.entry-content o article
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'entry-content')]") ??
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'blog-content')]") ??
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main");

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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"edureka\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
