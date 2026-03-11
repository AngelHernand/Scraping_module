using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.TheBridge;

/// <summary>
/// Scraper para The Bridge (thebridge.tech/blog) — bootcamp tecnológico español
/// con contenido de blog sobre preguntas de entrevista técnica en español.
/// </summary>
public class TheBridgeScraper : BaseScraper
{
    private static readonly List<(string Url, string Category, string Tech)> KnownPages = new()
    {
        ("https://www.thebridge.tech/blog/10-preguntas-entrevista-desarrollador-web", "frontend", "web-development"),
        ("https://www.thebridge.tech/blog/preguntas-entrevista-programador", "backend", "programming"),
        ("https://www.thebridge.tech/blog/preguntas-entrevista-data-science", "data-science", "data-science"),
        ("https://www.thebridge.tech/blog/preguntas-entrevista-ciberseguridad", "cybersecurity", "cybersecurity"),
        ("https://www.thebridge.tech/blog/preguntas-entrevista-ux-ui", "design", "ux-ui"),
        ("https://www.thebridge.tech/blog/preguntas-entrevista-devops", "devops", "devops"),
    };

    public override string SourceName => "TheBridge";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public TheBridgeScraper(
        ILogger<TheBridgeScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[TheBridge] Iniciando scraping — {Count} páginas conocidas", KnownPages.Count);

        // Descubrir más artículos vía sitemap
        var extraUrls = await DiscoverFromSitemapAsync(cancellationToken);
        
        var allUrls = new List<(string Url, string Category, string Tech)>(KnownPages);
        foreach (var url in extraUrls)
        {
            if (!allUrls.Any(x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                allUrls.Add((url, "general", "general"));
        }

        foreach (var (url, category, tech) in allUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await ApplyRateLimitAsync(cancellationToken);
                var count = await ScrapePageAsync(url, category, tech, result, cancellationToken);
                if (count > 0)
                    Logger.LogInformation("[TheBridge] {Tech}: {Count} preguntas — {Url}", tech, count, url);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[TheBridge] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[TheBridge] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[TheBridge] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    private async Task<List<string>> DiscoverFromSitemapAsync(CancellationToken ct)
    {
        var urls = new List<string>();
        var sitemapCandidates = new[]
        {
            "https://www.thebridge.tech/sitemap.xml",
            "https://www.thebridge.tech/blog/sitemap.xml",
            "https://www.thebridge.tech/post-sitemap.xml",
        };

        foreach (var sitemapUrl in sitemapCandidates)
        {
            try
            {
                HttpClient.DefaultRequestHeaders.Clear();
                HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());

                var xml = await HttpClient.GetStringAsync(sitemapUrl, ct);
                var pattern = new System.Text.RegularExpressions.Regex(
                    @"<loc>(https?://www\.thebridge\.tech/blog/[^<]*(?:pregunta|entrevista)[^<]*)</loc>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (System.Text.RegularExpressions.Match match in pattern.Matches(xml))
                    urls.Add(match.Groups[1].Value);

                if (urls.Count > 0)
                {
                    Logger.LogInformation("[TheBridge] Descubiertas {Count} URLs de entrevista en {Sitemap}",
                        urls.Count, sitemapUrl);
                    break;
                }
            }
            catch { /* sitemap no disponible */ }
        }

        return urls;
    }

    private async Task<int> ScrapePageAsync(
        string url, string category, string tech, ScrapingResult result, CancellationToken ct)
    {
        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9");

        var response = await HttpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var garbage in doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//aside|//header")?.ToList() ?? new List<HtmlNode>())
            garbage.Remove();

        var contentNode =
            doc.DocumentNode.SelectSingleNode("//article") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'blog-post')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'post-content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'entry-content')]") ??
            doc.DocumentNode.SelectSingleNode("//main");

        if (contentNode == null) return 0;

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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"thebridge\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
