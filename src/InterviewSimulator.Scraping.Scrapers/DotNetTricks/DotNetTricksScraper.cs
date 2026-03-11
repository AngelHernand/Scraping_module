using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.DotNetTricks;

/// <summary>
/// Scraper para DotNetTricks — comunidad especializada en .NET, C#, SQL Server
/// y tecnologías del stack Microsoft. Buena cobertura de preguntas de entrevista.
/// URLs: https://www.dotnettricks.com/learn/[tech]
///       https://www.dotnettricks.com/interview-questions/[tech]
/// </summary>
public class DotNetTricksScraper : BaseScraper
{
    private const string BaseUrl = "https://www.dotnettricks.com";

    private static readonly List<(string Url, string Category, string Tech)> TargetPages = new()
    {
        ($"{BaseUrl}/interview-questions/csharp",             "csharp",        "csharp"),
        ($"{BaseUrl}/interview-questions/dotnet",             "dotnet",        "dotnet"),
        ($"{BaseUrl}/interview-questions/aspnet",             "aspnet-core",   "aspnet"),
        ($"{BaseUrl}/interview-questions/aspnet-mvc",         "aspnet-core",   "aspnet-mvc"),
        ($"{BaseUrl}/interview-questions/aspnet-webapi",      "rest-api",      "webapi"),
        ($"{BaseUrl}/interview-questions/sql",                "sql",           "sql"),
        ($"{BaseUrl}/interview-questions/sqlserver",          "sql",           "sql-server"),
        ($"{BaseUrl}/interview-questions/entityframework",    "dotnet",        "entity-framework"),
        ($"{BaseUrl}/interview-questions/angular",            "angular",       "angular"),
        ($"{BaseUrl}/interview-questions/javascript",         "javascript",    "javascript"),
        ($"{BaseUrl}/interview-questions/typescript",         "typescript",    "typescript"),
        ($"{BaseUrl}/interview-questions/react",              "react",         "react"),
        ($"{BaseUrl}/interview-questions/azure",              "azure",         "azure"),
        ($"{BaseUrl}/interview-questions/docker",             "docker",        "docker"),
        ($"{BaseUrl}/interview-questions/git",                "git",           "git"),
        ($"{BaseUrl}/interview-questions/designpattern",      "design-patterns","design-patterns"),
        ($"{BaseUrl}/interview-questions/oops",               "oop",           "oop"),
        ($"{BaseUrl}/interview-questions/microservices",      "microservices", "microservices"),
    };

    public override string SourceName => "DotNetTricks";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public DotNetTricksScraper(
        ILogger<DotNetTricksScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var cfg = Settings.Scrapers.GetValueOrDefault("DotNetTricks");
        var maxPages = cfg?.MaxPages ?? Settings.MaxPagesPerSource;

        Logger.LogInformation("[DotNetTricks] Iniciando scraping — {Count} páginas objetivo", TargetPages.Count);

        foreach (var (url, category, tech) in TargetPages)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                int total = 0;
                for (int page = 1; page <= maxPages; page++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var pagedUrl = page == 1 ? url : $"{url}?page={page}";
                    int count = await ScrapePageAsync(pagedUrl, category, tech, result, cancellationToken);
                    total += count;
                    if (count == 0) break;
                }

                Logger.LogInformation("[DotNetTricks] {Tech}: {Count} preguntas", tech, total);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[DotNetTricks] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[DotNetTricks] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[DotNetTricks] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
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

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // DotNetTricks: contenido en div.panel-body o article o main
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'panel-body')]") ??
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'qs-content')]") ??
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main") ??
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content')]");

        if (contentNode == null) return 0;

        foreach (var garbage in contentNode.SelectNodes(".//script|.//style|.//nav|.//aside|.//header|.//footer|.//form")?.ToList() ?? new List<HtmlNode>())
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"dotnettricks\",\"dotnet\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
