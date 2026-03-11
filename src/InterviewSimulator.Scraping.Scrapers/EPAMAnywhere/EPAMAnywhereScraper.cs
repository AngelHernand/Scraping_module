using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.EPAMAnywhere;

/// <summary>
/// Scraper para EPAM Anywhere (anywhere.epam.com/es/blog) — sección en español del blog
/// de EPAM con artículos sobre preguntas de entrevista técnica para desarrolladores.
/// </summary>
public class EPAMAnywhereScraper : BaseScraper
{
    private static readonly List<(string Url, string Category, string Tech)> KnownPages = new()
    {
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-software-desarrollador", "backend", "software-engineering"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-java", "java", "java"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-python", "python", "python"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-javascript", "javascript", "javascript"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-react", "react", "react"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-dotnet", "dotnet", "dotnet"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-angular", "angular", "angular"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-devops", "devops", "devops"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-sql", "sql", "sql"),
        ("https://anywhere.epam.com/es/blog/preguntas-entrevista-data-science", "data-science", "data-science"),
    };

    public override string SourceName => "EPAMAnywhere";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public EPAMAnywhereScraper(
        ILogger<EPAMAnywhereScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[EPAMAnywhere] Iniciando scraping — {Count} páginas conocidas", KnownPages.Count);

        // Descubrir artículos en español vía sitemap
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
                    Logger.LogInformation("[EPAMAnywhere] {Tech}: {Count} preguntas — {Url}", tech, count, url);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Logger.LogDebug("[EPAMAnywhere] {Url} → {Code}", url, ex.StatusCode);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[EPAMAnywhere] Error en {Url}", url);
                result.Errors.Add($"Error en {url}: {ex.Message}");
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[EPAMAnywhere] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    private async Task<List<string>> DiscoverFromSitemapAsync(CancellationToken ct)
    {
        var urls = new List<string>();
        var sitemapCandidates = new[]
        {
            "https://anywhere.epam.com/sitemap.xml",
            "https://anywhere.epam.com/es/sitemap.xml",
        };

        foreach (var sitemapUrl in sitemapCandidates)
        {
            try
            {
                HttpClient.DefaultRequestHeaders.Clear();
                HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());

                var xml = await HttpClient.GetStringAsync(sitemapUrl, ct);

                // Buscar URLs en español que contengan keywords de entrevista
                var pattern = new System.Text.RegularExpressions.Regex(
                    @"<loc>(https?://anywhere\.epam\.com/es/blog/[^<]*(?:pregunta|entrevista|interview)[^<]*)</loc>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (System.Text.RegularExpressions.Match match in pattern.Matches(xml))
                    urls.Add(match.Groups[1].Value);

                // También buscar sub-sitemaps
                if (urls.Count == 0)
                {
                    var subPattern = new System.Text.RegularExpressions.Regex(
                        @"<loc>(https?://anywhere\.epam\.com/[^<]*sitemap[^<]*\.xml)</loc>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    foreach (System.Text.RegularExpressions.Match subMatch in subPattern.Matches(xml))
                    {
                        try
                        {
                            await ApplyRateLimitAsync(ct);
                            var subXml = await HttpClient.GetStringAsync(subMatch.Groups[1].Value, ct);
                            foreach (System.Text.RegularExpressions.Match m in pattern.Matches(subXml))
                                urls.Add(m.Groups[1].Value);
                        }
                        catch { /* sub-sitemap no disponible */ }
                    }
                }

                if (urls.Count > 0)
                {
                    Logger.LogInformation("[EPAMAnywhere] Descubiertas {Count} URLs de entrevista en español", urls.Count);
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
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'article-content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'blog-content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'post-content')]") ??
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"epam\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
