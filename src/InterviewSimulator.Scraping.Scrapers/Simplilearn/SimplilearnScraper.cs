using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Simplilearn;

/// <summary>
/// Scraper para Simplilearn — plataforma de e-learning con artículos detallados
/// de preguntas de entrevista técnica. Buen coverage de tecnologías modernas.
/// URLs: https://www.simplilearn.com/tutorials/[tech]-tutorial/[tech]-interview-questions
/// </summary>
public class SimplilearnScraper : BaseScraper
{
    private const string BaseUrl = "https://www.simplilearn.com";

    // Mapa de (segmento-tutorial, slug-artículo) → (category, tech)
    private static readonly List<(string Url, string Category, string Tech)> TargetPages = new()
    {
        ($"{BaseUrl}/tutorials/java-tutorial/java-interview-questions",                 "java",           "java"),
        ($"{BaseUrl}/tutorials/python-tutorial/python-interview-questions",             "python",         "python"),
        ($"{BaseUrl}/tutorials/javascript-tutorial/javascript-interview-questions",     "javascript",     "javascript"),
        ($"{BaseUrl}/tutorials/react-js-tutorial/reactjs-interview-questions",         "react",          "react"),
        ($"{BaseUrl}/tutorials/nodejs-tutorial/node-js-interview-questions",           "nodejs",         "nodejs"),
        ($"{BaseUrl}/tutorials/angular-tutorial/angular-interview-questions",          "angular",        "angular"),
        ($"{BaseUrl}/tutorials/sql-tutorial/sql-interview-questions",                  "sql",            "sql"),
        ($"{BaseUrl}/tutorials/mongodb-tutorial/mongodb-interview-questions",          "mongodb",        "mongodb"),
        ($"{BaseUrl}/tutorials/docker-tutorial/docker-interview-questions",            "docker",         "docker"),
        ($"{BaseUrl}/tutorials/kubernetes-tutorial/kubernetes-interview-questions",    "kubernetes",     "kubernetes"),
        ($"{BaseUrl}/tutorials/aws-tutorial/aws-interview-questions",                  "aws",            "aws"),
        ($"{BaseUrl}/tutorials/azure-tutorial/azure-interview-questions",              "azure",          "azure"),
        ($"{BaseUrl}/tutorials/data-structure-tutorial/data-structure-interview-questions", "data-structures","data-structures"),
        ($"{BaseUrl}/tutorials/spring-tutorial/spring-interview-questions",            "spring-boot",    "spring-boot"),
        ($"{BaseUrl}/tutorials/devops-tutorial/devops-interview-questions",            "devops",         "devops"),
        ($"{BaseUrl}/tutorials/git-tutorial/git-interview-questions",                  "git",            "git"),
        ($"{BaseUrl}/tutorials/redis-tutorial/redis-interview-questions",              "redis",          "redis"),
        ($"{BaseUrl}/tutorials/microservice-tutorial/microservices-interview-questions","microservices", "microservices"),
        ($"{BaseUrl}/tutorials/typescript-tutorial/typescript-interview-questions",    "typescript",     "typescript"),
        ($"{BaseUrl}/tutorials/system-design/system-design-interview-questions",       "system-design",  "system-design"),
    };

    public override string SourceName => "Simplilearn";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public SimplilearnScraper(
        ILogger<SimplilearnScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Simplilearn] Iniciando scraping — {Count} páginas objetivo", TargetPages.Count);

        foreach (var (url, category, tech) in TargetPages)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                int count = await ScrapePageAsync(url, category, tech, result, cancellationToken);
                Logger.LogInformation("[Simplilearn] {Tech}: {Count} preguntas", tech, count);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[Simplilearn] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[Simplilearn] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[Simplilearn] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
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

        // Simplilearn: contenido en article o div.blog-content o div.ArticleBody
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'blog-content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'ArticleBody')]") ??
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"simplilearn\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
