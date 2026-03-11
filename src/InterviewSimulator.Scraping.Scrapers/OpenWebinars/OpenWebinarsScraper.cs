using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.OpenWebinars;

/// <summary>
/// Scraper para OpenWebinars (openwebinars.net/blog/) — plataforma de formación
/// tecnológica española con artículos de preguntas de entrevista en español nativo.
/// </summary>
public class OpenWebinarsScraper : BaseScraper
{
    private const string BaseUrl = "https://openwebinars.net/blog";

    private static readonly List<(string[] Slugs, string Category, string Tech)> TechArticles = new()
    {
        (new[] { "preguntas-entrevista-java", "preguntas-entrevista-programador-java" }, "java", "java"),
        (new[] { "preguntas-entrevista-python", "preguntas-entrevista-programador-python" }, "python", "python"),
        (new[] { "preguntas-entrevista-javascript", "preguntas-javascript" }, "javascript", "javascript"),
        (new[] { "preguntas-entrevista-sql", "preguntas-sql-entrevista" }, "sql", "sql"),
        (new[] { "preguntas-entrevista-react", "preguntas-react-entrevista" }, "react", "react"),
        (new[] { "preguntas-entrevista-angular", "preguntas-angular-entrevista" }, "angular", "angular"),
        (new[] { "preguntas-entrevista-docker", "preguntas-docker-entrevista" }, "docker", "docker"),
        (new[] { "preguntas-entrevista-devops", "preguntas-devops-entrevista" }, "devops", "devops"),
        (new[] { "preguntas-entrevista-git", "preguntas-git" }, "git", "git"),
        (new[] { "preguntas-entrevista-aws", "preguntas-aws-entrevista" }, "aws", "aws"),
        (new[] { "preguntas-entrevista-css", "preguntas-css" }, "frontend", "css"),
        (new[] { "preguntas-entrevista-html", "preguntas-html" }, "frontend", "html"),
        (new[] { "preguntas-entrevista-node", "preguntas-nodejs" }, "nodejs", "nodejs"),
        (new[] { "preguntas-entrevista-typescript", "preguntas-typescript" }, "typescript", "typescript"),
        (new[] { "preguntas-entrevista-kubernetes", "preguntas-kubernetes" }, "kubernetes", "kubernetes"),
        (new[] { "preguntas-entrevista-csharp", "preguntas-entrevista-c-sharp", "preguntas-entrevista-net" }, "csharp", "csharp"),
        (new[] { "preguntas-entrevista-programador", "preguntas-entrevista-desarrollador", "preguntas-entrevista-developer" }, "backend", "backend"),
    };

    public override string SourceName => "OpenWebinars";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public OpenWebinarsScraper(
        ILogger<OpenWebinarsScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[OpenWebinars] Iniciando scraping — {Count} tecnologías", TechArticles.Count);

        // Descubrir artículos vía sitemap
        var discoveredUrls = await DiscoverFromSitemapAsync(cancellationToken);
        foreach (var url in discoveredUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                await ApplyRateLimitAsync(cancellationToken);
                var count = await ScrapeArticleAsync(url, "general", "general", result, cancellationToken);
                if (count > 0)
                    Logger.LogInformation("[OpenWebinars] Sitemap: {Count} preguntas — {Url}", count, url);
            }
            catch (Exception ex)
            {
                Logger.LogDebug("[OpenWebinars] Error sitemap URL {Url}: {Msg}", url, ex.Message);
            }
        }

        // URLs candidatas por tecnología
        foreach (var (slugs, category, tech) in TechArticles)
        {
            if (cancellationToken.IsCancellationRequested) break;

            foreach (var slug in slugs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var url = $"{BaseUrl}/{slug}/";
                if (discoveredUrls.Contains(url)) continue;

                try
                {
                    await ApplyRateLimitAsync(cancellationToken);
                    var count = await ScrapeArticleAsync(url, category, tech, result, cancellationToken);
                    if (count > 0)
                    {
                        Logger.LogInformation("[OpenWebinars] {Tech}: {Count} preguntas — {Url}", tech, count, url);
                        break;
                    }
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Logger.LogDebug("[OpenWebinars] {Url} → {Code}", url, ex.StatusCode);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug("[OpenWebinars] Error en {Url}: {Msg}", url, ex.Message);
                }
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[OpenWebinars] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    private async Task<HashSet<string>> DiscoverFromSitemapAsync(CancellationToken ct)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());

            var xml = await HttpClient.GetStringAsync("https://openwebinars.net/sitemap.xml", ct);
            var pattern = new System.Text.RegularExpressions.Regex(
                @"<loc>(https?://openwebinars\.net/blog/[^<]*(?:pregunta|entrevista)[^<]*)</loc>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(xml))
                urls.Add(match.Groups[1].Value);

            // Sub-sitemaps
            var subPattern = new System.Text.RegularExpressions.Regex(
                @"<loc>(https?://openwebinars\.net/[^<]*sitemap[^<]*\.xml)</loc>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in subPattern.Matches(xml))
            {
                try
                {
                    await ApplyRateLimitAsync(ct);
                    var subXml = await HttpClient.GetStringAsync(match.Groups[1].Value, ct);
                    foreach (System.Text.RegularExpressions.Match subMatch in pattern.Matches(subXml))
                        urls.Add(subMatch.Groups[1].Value);
                }
                catch { }
            }

            if (urls.Count > 0)
                Logger.LogInformation("[OpenWebinars] Descubiertas {Count} URLs de entrevista en sitemap", urls.Count);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("[OpenWebinars] Sitemap no disponible: {Msg}", ex.Message);
        }

        return urls;
    }

    private async Task<int> ScrapeArticleAsync(
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
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'entry-content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'post-content')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'blog-content')]") ??
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"openwebinars\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
