using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.TealHQ;

/// <summary>
/// Scraper para TealHQ — preguntas de entrevista por rol profesional.
/// URLs: https://www.tealhq.com/interview-questions/[rol]
/// Contenido server-side, usa estructura limpia de Q&A.
/// </summary>
public class TealHQScraper : BaseScraper
{
    private const string BaseUrl = "https://www.tealhq.com/interview-questions";

    private static readonly Dictionary<string, (string Category, string Tech)> RoleSlugs = new()
    {
        { "backend-developer",    ("backend",         "backend") },
        { "api-developer",        ("rest-api",        "api") },
        { "database-developer",   ("database",        "database") },
        { "full-stack-developer", ("fullstack",       "fullstack") },
        { "software-engineer",    ("backend",         "software-engineering") },
        { "devops-engineer",      ("devops",          "devops") },
        { "cloud-engineer",       ("cloud",           "cloud") },
        { "data-engineer",        ("database",        "data-engineering") },
        { "machine-learning-engineer", ("ai-ml",     "ml") },
        { "python-developer",     ("python",          "python") },
        { "javascript-developer", ("javascript",      "javascript") },
        { "java-developer",       ("java",            "java") },
        { "react-developer",      ("react",           "react") },
        { "node-developer",       ("nodejs",          "nodejs") },
    };

    public override string SourceName => "TealHQ";
    public override SourceType SourceType => SourceType.JobBoard;

    public TealHQScraper(
        ILogger<TealHQScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[TealHQ] Iniciando scraping — {Count} roles", RoleSlugs.Count);

        foreach (var (slug, (category, tech)) in RoleSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}/";
            try
            {
                int count = await ScrapeRolePageAsync(url, category, tech, result, cancellationToken);
                Logger.LogInformation("[TealHQ] {Role}: {Count} preguntas", slug, count);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[TealHQ] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[TealHQ] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[TealHQ] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    private async Task<int> ScrapeRolePageAsync(
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

        // TealHQ: contenido principal en article o main
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main") ??
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content')]");

        if (contentNode == null) return 0;

        // Limpiar navegación y sidebar
        foreach (var garbage in contentNode.SelectNodes(".//script|.//style|.//nav|.//aside")?.ToList() ?? new List<HtmlNode>())
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"tealhq\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
