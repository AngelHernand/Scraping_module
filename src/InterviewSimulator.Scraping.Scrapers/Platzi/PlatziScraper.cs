using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Platzi;

/// <summary>
/// Scraper para Platzi (platzi.com/blog/) — la plataforma de educación en línea
/// más popular de América Latina. Contenido nativo en español con artículos
/// de preguntas de entrevista técnica por tecnología/rol.
/// </summary>
public class PlatziScraper : BaseScraper
{
    private const string BaseUrl = "https://platzi.com/blog";

    private static readonly List<(string[] Slugs, string Category, string Tech)> TechArticles = new()
    {
        (new[] { "preguntas-entrevista-javascript", "preguntas-javascript-entrevista", "preguntas-tecnicas-javascript" }, "javascript", "javascript"),
        (new[] { "preguntas-entrevista-python", "preguntas-python-entrevista" }, "python", "python"),
        (new[] { "preguntas-entrevista-java", "preguntas-java-entrevista" }, "java", "java"),
        (new[] { "preguntas-entrevista-react", "preguntas-react-entrevista" }, "react", "react"),
        (new[] { "preguntas-entrevista-programador", "preguntas-entrevista-desarrollador", "preguntas-tecnicas-entrevista" }, "backend", "backend"),
        (new[] { "preguntas-entrevista-frontend", "preguntas-frontend-developer" }, "frontend", "frontend"),
        (new[] { "preguntas-entrevista-backend", "preguntas-backend-entrevista" }, "backend", "backend"),
        (new[] { "preguntas-entrevista-sql", "preguntas-sql-entrevista" }, "sql", "sql"),
        (new[] { "preguntas-entrevista-data-science", "preguntas-ciencia-de-datos-entrevista" }, "data-science", "data-science"),
        (new[] { "preguntas-entrevista-devops", "preguntas-devops-entrevista" }, "devops", "devops"),
        (new[] { "preguntas-entrevista-angular", "preguntas-angular-entrevista" }, "angular", "angular"),
        (new[] { "preguntas-entrevista-node", "preguntas-nodejs-entrevista" }, "nodejs", "nodejs"),
        (new[] { "preguntas-entrevista-css", "preguntas-css-entrevista" }, "frontend", "css"),
        (new[] { "preguntas-entrevista-html", "preguntas-html-entrevista" }, "frontend", "html"),
        (new[] { "preguntas-entrevista-git", "preguntas-git-entrevista" }, "git", "git"),
        (new[] { "preguntas-entrevista-docker", "preguntas-docker-entrevista" }, "docker", "docker"),
        (new[] { "preguntas-entrevista-typescript", "preguntas-typescript" }, "typescript", "typescript"),
    };

    public override string SourceName => "Platzi";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public PlatziScraper(
        ILogger<PlatziScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Platzi] Iniciando scraping — {Count} tecnologías", TechArticles.Count);

        // Paso 1: Descubrir artículos vía sitemap
        var discoveredUrls = await DiscoverFromSitemapAsync(cancellationToken);
        foreach (var url in discoveredUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                await ApplyRateLimitAsync(cancellationToken);
                var count = await ScrapeArticleAsync(url, "general", "general", result, cancellationToken);
                if (count > 0)
                    Logger.LogInformation("[Platzi] Sitemap: {Count} preguntas — {Url}", count, url);
            }
            catch (Exception ex)
            {
                Logger.LogDebug("[Platzi] Error sitemap URL {Url}: {Msg}", url, ex.Message);
            }
        }

        // Paso 2: Intentar URLs candidatas por tecnología
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
                        Logger.LogInformation("[Platzi] {Tech}: {Count} preguntas — {Url}", tech, count, url);
                        break;
                    }
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Logger.LogDebug("[Platzi] {Url} → {Code}", url, ex.StatusCode);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug("[Platzi] Error en {Url}: {Msg}", url, ex.Message);
                }
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[Platzi] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
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

            // Intentar varios formatos de sitemap
            var sitemapCandidates = new[] { "https://platzi.com/sitemap.xml", "https://platzi.com/blog/sitemap.xml" };

            foreach (var sitemapUrl in sitemapCandidates)
            {
                try
                {
                    var xml = await HttpClient.GetStringAsync(sitemapUrl, ct);
                    var pattern = new System.Text.RegularExpressions.Regex(
                        @"<loc>(https?://platzi\.com/blog/[^<]*(?:pregunta|entrevista)[^<]*)</loc>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    foreach (System.Text.RegularExpressions.Match match in pattern.Matches(xml))
                        urls.Add(match.Groups[1].Value);

                    // Sub-sitemaps
                    var subPattern = new System.Text.RegularExpressions.Regex(
                        @"<loc>(https?://platzi\.com/[^<]*sitemap[^<]*\.xml)</loc>",
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

                    if (urls.Count > 0) break;
                }
                catch { }
            }

            if (urls.Count > 0)
                Logger.LogInformation("[Platzi] Descubiertas {Count} URLs de entrevista en sitemap", urls.Count);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("[Platzi] Sitemap no disponible: {Msg}", ex.Message);
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
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'Post')]") ??
            doc.DocumentNode.SelectSingleNode("//*[contains(@class,'BlogPost')]") ??
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"platzi\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
