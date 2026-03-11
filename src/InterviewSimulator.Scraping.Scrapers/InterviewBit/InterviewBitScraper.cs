using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.InterviewBit;

/// <summary>
/// Scraper para InterviewBit — excelente cobertura de preguntas técnicas
/// con respuestas detalladas. Renderizado server-side en su mayoría.
/// URLs: https://www.interviewbit.com/[tech]-interview-questions/
/// </summary>
public class InterviewBitScraper : BaseScraper
{
    private const string BaseUrl = "https://www.interviewbit.com";

    private static readonly Dictionary<string, (string Category, string Tech)> TechSlugs = new()
    {
        { "java-interview-questions",              ("java",              "java") },
        { "python-interview-questions",            ("python",            "python") },
        { "sql-interview-questions",               ("sql",               "sql") },
        { "javascript-interview-questions",        ("javascript",        "javascript") },
        { "react-interview-questions",             ("react",             "react") },
        { "node-js-interview-questions",           ("nodejs",            "nodejs") },
        { "angular-interview-questions",           ("angular",           "angular") },
        { "css-interview-questions",               ("frontend",          "css") },
        { "html-interview-questions",              ("frontend",          "html") },
        { "rest-api-interview-questions",          ("rest-api",          "rest-api") },
        { "system-design-interview-questions",     ("system-design",     "system-design") },
        { "full-stack-developer-interview-questions", ("fullstack",      "fullstack") },
        { "csharp-interview-questions",            ("csharp",            "csharp") },
        { "dotnet-interview-questions",            ("dotnet",            "dotnet") },
        { "spring-boot-interview-questions",       ("spring-boot",       "spring-boot") },
        { "docker-interview-questions",            ("docker",            "docker") },
        { "kubernetes-interview-questions",        ("kubernetes",        "kubernetes") },
        { "aws-interview-questions",               ("aws",               "aws") },
        { "azure-interview-questions",             ("azure",             "azure") },
        { "git-interview-questions",               ("git",               "git") },
        { "mongodb-interview-questions",           ("mongodb",           "mongodb") },
        { "data-structures-interview-questions",   ("data-structures",   "data-structures") },
        { "oops-interview-questions",              ("oop",               "oop") },
        { "microservices-interview-questions",     ("microservices",     "microservices") },
        { "devops-interview-questions",            ("devops",            "devops") },
        { "linux-interview-questions",             ("linux",             "linux") },
        { "typescript-interview-questions",        ("typescript",        "typescript") },
        { "postgresql-interview-questions",        ("postgresql",        "postgresql") },
        { "mysql-interview-questions",             ("mysql",             "mysql") },
        { "redis-interview-questions",             ("redis",             "redis") },
    };

    public override string SourceName => "InterviewBit";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public InterviewBitScraper(
        ILogger<InterviewBitScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var cfg = Settings.Scrapers.GetValueOrDefault("InterviewBit");
        var maxPages = cfg?.MaxPages ?? Settings.MaxPagesPerSource;

        Logger.LogInformation("[InterviewBit] Iniciando scraping — {Count} tecnologías", TechSlugs.Count);

        foreach (var (slug, (category, tech)) in TechSlugs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var url = $"{BaseUrl}/{slug}/";
            try
            {
                var count = await ScrapeInterviewPageAsync(url, category, tech, result, cancellationToken);
                Logger.LogInformation("[InterviewBit] {Tech}: {Count} preguntas", tech, count);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[InterviewBit] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[InterviewBit] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[InterviewBit] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    // ---------------------------------------------------------------------------
    // Scraping de una página de InterviewBit
    // Estructura típica:
    //   <div class="ib__content">
    //     <h3 class="ib__h3">1. ¿Pregunta...?</h3>
    //     <div class="ib__text">Respuesta...</div>
    // ---------------------------------------------------------------------------
    private async Task<int> ScrapeInterviewPageAsync(
        string url, string category, string tech, ScrapingResult result, CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        HttpClient.DefaultRequestHeaders.Add("Referer", "https://www.interviewbit.com/");

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        int count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ---- Estrategia 1: Estructura nativa de InterviewBit ----
        // Buscar pares h3/h4 (pregunta) + div/p (respuesta)
        var articleNode =
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'ib__content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'style__ContQuesBody')]") ??
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main");

        if (articleNode != null)
        {
            var contentHtml = articleNode.InnerHtml;
            var qaPairs = ExtractQuestionsWithAnswersFromText(contentHtml);

            foreach (var (questionText, answerText) in qaPairs)
            {
                if (!seen.Add(questionText.ToLowerInvariant())) continue;
                if (answerText.Length < 30) continue;

                var q = CreateScrapedQuestion(questionText, url, null, sourceId: 0, answerText: answerText);
                q.Category = QuestionCategory.Technical;
                q.Technology = tech;
                q.Subcategory = category;
                q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"interviewbit\"]";
                q.OriginalLanguage = "en";

                result.Questions.Add(q);
                count++;
            }
        }

        // ---- Estrategia 2: Fallback — usar el HTML completo ----
        if (count == 0)
        {
            var qaPairs = ExtractQuestionsWithAnswersFromText(html);
            foreach (var (questionText, answerText) in qaPairs)
            {
                if (!seen.Add(questionText.ToLowerInvariant())) continue;
                if (answerText.Length < 30) continue;

                var q = CreateScrapedQuestion(questionText, url, null, sourceId: 0, answerText: answerText);
                q.Category = QuestionCategory.Technical;
                q.Technology = tech;
                q.Subcategory = category;
                q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"interviewbit\"]";
                q.OriginalLanguage = "en";

                result.Questions.Add(q);
                count++;
            }
        }

        return count;
    }
}
