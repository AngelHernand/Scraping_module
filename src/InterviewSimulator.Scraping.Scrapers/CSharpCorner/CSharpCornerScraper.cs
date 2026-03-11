using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.CSharpCorner;

/// <summary>
/// Scraper para C# Corner — comunidad especializada en .NET/C#/Azure.
/// Excelente fuente de preguntas de entrevista para el stack Microsoft.
/// URLs: https://www.c-sharpcorner.com/interview-questions/ (listado paginado)
///       https://www.c-sharpcorner.com/uploadfile/[autor]/[slug]/
/// </summary>
public class CSharpCornerScraper : BaseScraper
{
    private const string BaseUrl = "https://www.c-sharpcorner.com";

    // Páginas de listado de preguntas de entrevista
    private static readonly List<(string Url, string Category, string Tech)> DirectPages = new()
    {
        ($"{BaseUrl}/interview-questions/csharp/",         "csharp",        "csharp"),
        ($"{BaseUrl}/interview-questions/dotnet/",         "dotnet",        "dotnet"),
        ($"{BaseUrl}/interview-questions/aspnet/",         "aspnet-core",   "aspnet"),
        ($"{BaseUrl}/interview-questions/sql/",            "sql",           "sql"),
        ($"{BaseUrl}/interview-questions/azure/",          "azure",         "azure"),
        ($"{BaseUrl}/interview-questions/javascript/",     "javascript",    "javascript"),
        ($"{BaseUrl}/interview-questions/angular/",        "angular",       "angular"),
        ($"{BaseUrl}/interview-questions/react/",          "react",         "react"),
        ($"{BaseUrl}/interview-questions/oops/",           "oop",           "oop"),
        ($"{BaseUrl}/interview-questions/microservices/",  "microservices", "microservices"),
        ($"{BaseUrl}/interview-questions/",                "backend",       "general"),
    };

    // Artículos directos conocidos de alta calidad
    private static readonly string[] DirectArticleUrls =
    {
        $"{BaseUrl}/uploadfile/puranindia/c-sharp-interview-questions-and-answers/",
        $"{BaseUrl}/uploadfile/mahesh/top-50-net-interview-questions-and-answers/",
        $"{BaseUrl}/uploadfile/8f4a9b/asp-net-mvc-questions-and-answers/",
        $"{BaseUrl}/quiz/oop/",
        $"{BaseUrl}/interview-questions/csharp/",
        $"{BaseUrl}/interview-questions/dotnet/",
    };

    public override string SourceName => "CSharpCorner";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public CSharpCornerScraper(
        ILogger<CSharpCornerScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var cfg = Settings.Scrapers.GetValueOrDefault("CSharpCorner");
        var maxPages = cfg?.MaxPages ?? Settings.MaxPagesPerSource;

        Logger.LogInformation("[CSharpCorner] Iniciando scraping — {Count} páginas objetivo", DirectPages.Count);

        foreach (var (url, category, tech) in DirectPages)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Las páginas de listado paginan con ?page=N
                int total = 0;
                for (int page = 1; page <= maxPages; page++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var pagedUrl = page == 1 ? url : $"{url}?page={page}";
                    int count = await ScrapePageAsync(pagedUrl, category, tech, result, cancellationToken);
                    total += count;
                    if (count == 0) break; // Sin más contenido
                }

                Logger.LogInformation("[CSharpCorner] {Tech}: {Count} preguntas", tech, total);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[CSharpCorner] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[CSharpCorner] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        result.TotalDocumentsFound = result.Documents.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[CSharpCorner] Completado — {Total} preguntas, {Docs} documentos en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, result.TotalDocumentsFound, sw.Elapsed);
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

        // C# Corner: contenido en article o div.article-body
        var contentNode =
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'article-body')]") ??
            doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content-article')]") ??
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//main");

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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"csharpcorner\",\"dotnet\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        // Extraer documentos RAG del contenido del artículo
        var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "C# Corner";
        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "CSharpCorner", sourceId: 0,
            technology: "C#",
            contentType: ContentType.Article);
        result.Documents.AddRange(docs);

        return count;
    }
}
