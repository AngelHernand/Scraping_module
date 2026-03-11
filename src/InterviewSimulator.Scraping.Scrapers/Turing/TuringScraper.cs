using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Turing;

/// <summary>
/// Scraper para Turing.com — páginas estructuradas de preguntas de entrevista
/// por tecnología. Cada ruta /interview-questions/{tech} tiene Q&amp;A detalladas.
/// Muchas páginas están disponibles en español o con traducción automática.
/// </summary>
public class TuringScraper : BaseScraper
{
    private const string BaseUrl = "https://www.turing.com/interview-questions";

    private static readonly Dictionary<string, (string Category, string Tech)> TechSlugs = new()
    {
        { "java",              ("java",              "java") },
        { "python",            ("python",            "python") },
        { "sql",               ("sql",               "sql") },
        { "javascript",        ("javascript",        "javascript") },
        { "react",             ("react",             "react") },
        { "angular",           ("angular",           "angular") },
        { "node-js",           ("nodejs",            "nodejs") },
        { "csharp",            ("csharp",            "csharp") },
        { "dotnet",            ("dotnet",            "dotnet") },
        { "php",               ("php",               "php") },
        { "ruby",              ("ruby",              "ruby") },
        { "swift",             ("swift",             "swift") },
        { "kotlin",            ("kotlin",            "kotlin") },
        { "docker",            ("docker",            "docker") },
        { "kubernetes",        ("kubernetes",        "kubernetes") },
        { "aws",               ("aws",               "aws") },
        { "devops",            ("devops",            "devops") },
        { "machine-learning",  ("ai-ml",             "machine-learning") },
        { "typescript",        ("typescript",        "typescript") },
        { "css",               ("frontend",          "css") },
        { "html",              ("frontend",          "html") },
        { "mongodb",           ("mongodb",           "mongodb") },
        { "postgresql",        ("postgresql",        "postgresql") },
        { "git",               ("git",               "git") },
        { "linux",             ("linux",             "linux") },
    };

    public override string SourceName => "Turing";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public TuringScraper(
        ILogger<TuringScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Turing] Iniciando scraping — {Count} tecnologías", TechSlugs.Count);

        foreach (var (slug, (category, tech)) in TechSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}";
            try
            {
                await ApplyRateLimitAsync(cancellationToken);
                var count = await ScrapePageAsync(url, category, tech, result, cancellationToken);
                if (count > 0)
                    Logger.LogInformation("[Turing] {Tech}: {Count} preguntas", tech, count);
                else
                    Logger.LogDebug("[Turing] {Tech}: 0 preguntas", tech);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[Turing] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[Turing] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[Turing] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    private async Task<int> ScrapePageAsync(
        string url, string category, string tech, ScrapingResult result, CancellationToken ct)
    {
        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9,en;q=0.5");

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Eliminar ruido
        foreach (var garbage in doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//aside|//header")?.ToList() ?? new List<HtmlNode>())
            garbage.Remove();

        var contentNode =
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'content') and not(contains(@class,'sidebar'))]") ??
            doc.DocumentNode.SelectSingleNode("//main") ??
            doc.DocumentNode.SelectSingleNode("//body");

        if (contentNode == null) return 0;

        var contentHtml = contentNode.InnerHtml;
        var qaPairs = ExtractQuestionsWithAnswersFromText(contentHtml);

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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"turing\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
