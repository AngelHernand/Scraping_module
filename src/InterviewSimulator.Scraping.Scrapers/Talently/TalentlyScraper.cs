using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.Talently;

/// <summary>
/// Scraper para Talently (talently.tech/blog/) — plataforma latinoamericana
/// de carreras en tecnología con preguntas de entrevista por rol.
/// Contenido nativo en español orientado al mercado LATAM.
/// </summary>
public class TalentlyScraper : BaseScraper
{
    private const string BaseUrl = "https://talently.tech/blog";

    private static readonly List<(string[] Slugs, string Category, string Tech)> RoleArticles = new()
    {
        (new[] { "preguntas-de-entrevista-para-programador-web", "preguntas-entrevista-programador", "preguntas-de-entrevista-para-desarrollador" }, "backend", "backend"),
        (new[] { "preguntas-de-entrevista-para-frontend", "preguntas-entrevista-frontend", "preguntas-de-entrevista-frontend-developer" }, "frontend", "frontend"),
        (new[] { "preguntas-de-entrevista-para-backend", "preguntas-entrevista-backend", "preguntas-de-entrevista-backend-developer" }, "backend", "backend"),
        (new[] { "preguntas-de-entrevista-para-fullstack", "preguntas-entrevista-fullstack", "preguntas-de-entrevista-full-stack" }, "fullstack", "fullstack"),
        (new[] { "preguntas-de-entrevista-para-data-engineer", "preguntas-entrevista-data-engineer", "preguntas-entrevista-ingeniero-datos" }, "data-science", "data-engineer"),
        (new[] { "preguntas-de-entrevista-para-devops", "preguntas-entrevista-devops" }, "devops", "devops"),
        (new[] { "preguntas-de-entrevista-para-qa", "preguntas-entrevista-qa", "preguntas-entrevista-testing" }, "testing", "testing"),
        (new[] { "preguntas-de-entrevista-para-mobile", "preguntas-entrevista-mobile-developer" }, "mobile", "mobile"),
        (new[] { "preguntas-de-entrevista-para-data-scientist", "preguntas-entrevista-cientifico-datos" }, "data-science", "data-science"),
        (new[] { "preguntas-de-entrevista-para-ingeniero-software", "preguntas-entrevista-software-engineer" }, "backend", "software-engineering"),
        (new[] { "preguntas-de-entrevista-java", "preguntas-entrevista-java" }, "java", "java"),
        (new[] { "preguntas-de-entrevista-python", "preguntas-entrevista-python" }, "python", "python"),
        (new[] { "preguntas-de-entrevista-javascript", "preguntas-entrevista-javascript" }, "javascript", "javascript"),
        (new[] { "preguntas-de-entrevista-react", "preguntas-entrevista-react" }, "react", "react"),
    };

    public override string SourceName => "Talently";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public TalentlyScraper(
        ILogger<TalentlyScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[Talently] Iniciando scraping — {Count} roles/tecnologías", RoleArticles.Count);

        // Descubrir vía sitemap
        var discoveredUrls = await DiscoverFromSitemapAsync(cancellationToken);
        foreach (var url in discoveredUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                await ApplyRateLimitAsync(cancellationToken);
                var count = await ScrapeArticleAsync(url, "general", "general", result, cancellationToken);
                if (count > 0)
                    Logger.LogInformation("[Talently] Sitemap: {Count} preguntas — {Url}", count, url);
            }
            catch (Exception ex)
            {
                Logger.LogDebug("[Talently] Error sitemap URL: {Msg}", ex.Message);
            }
        }

        // URLs candidatas
        foreach (var (slugs, category, tech) in RoleArticles)
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
                        Logger.LogInformation("[Talently] {Tech}: {Count} preguntas — {Url}", tech, count, url);
                        break;
                    }
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Logger.LogDebug("[Talently] {Url} → {Code}", url, ex.StatusCode);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug("[Talently] Error en {Url}: {Msg}", url, ex.Message);
                }
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[Talently] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
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

            var xml = await HttpClient.GetStringAsync("https://talently.tech/sitemap.xml", ct);
            var pattern = new System.Text.RegularExpressions.Regex(
                @"<loc>(https?://talently\.tech/blog/[^<]*(?:pregunta|entrevista)[^<]*)</loc>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(xml))
                urls.Add(match.Groups[1].Value);

            var subPattern = new System.Text.RegularExpressions.Regex(
                @"<loc>(https?://talently\.tech/[^<]*sitemap[^<]*\.xml)</loc>",
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
                Logger.LogInformation("[Talently] Descubiertas {Count} URLs de entrevista en sitemap", urls.Count);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("[Talently] Sitemap no disponible: {Msg}", ex.Message);
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"talently\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
