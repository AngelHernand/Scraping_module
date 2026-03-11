using System.Diagnostics;
using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.KeepCoding;

/// <summary>
/// Scraper para KeepCoding (keepcoding.io/blog/) — plataforma educativa española
/// con artículos de preguntas de entrevista técnica en español nativo.
/// Intenta múltiples patrones de URL por tecnología para maximizar cobertura.
/// </summary>
public class KeepCodingScraper : BaseScraper
{
    private const string BaseUrl = "https://keepcoding.io/blog";

    // Tecnologías con múltiples variantes de slug de URL
    private static readonly List<(string[] Slugs, string Category, string Tech)> TechArticles = new()
    {
        (new[] { "preguntas-de-entrevista-de-java", "preguntas-entrevista-java", "preguntas-frecuentes-entrevista-java" }, "java", "java"),
        (new[] { "preguntas-de-entrevista-de-python", "preguntas-entrevista-python", "preguntas-frecuentes-python" }, "python", "python"),
        (new[] { "preguntas-de-entrevista-javascript", "preguntas-entrevista-javascript", "preguntas-javascript" }, "javascript", "javascript"),
        (new[] { "preguntas-entrevista-sql", "preguntas-de-sql-entrevista" }, "sql", "sql"),
        (new[] { "preguntas-de-entrevista-react", "preguntas-entrevista-react" }, "react", "react"),
        (new[] { "preguntas-de-entrevista-angular", "preguntas-entrevista-angular" }, "angular", "angular"),
        (new[] { "preguntas-entrevista-node", "preguntas-entrevista-nodejs" }, "nodejs", "nodejs"),
        (new[] { "preguntas-de-entrevista-devops", "preguntas-entrevista-devops" }, "devops", "devops"),
        (new[] { "preguntas-entrevista-docker", "preguntas-de-entrevista-docker" }, "docker", "docker"),
        (new[] { "preguntas-de-entrevista-csharp", "preguntas-entrevista-csharp", "preguntas-entrevista-c-sharp" }, "csharp", "csharp"),
        (new[] { "preguntas-de-entrevista-programador", "preguntas-entrevista-programador", "preguntas-tecnicas-programacion" }, "backend", "backend"),
        (new[] { "preguntas-de-entrevista-frontend", "preguntas-entrevista-frontend", "preguntas-entrevista-desarrollador-web" }, "frontend", "frontend"),
        (new[] { "preguntas-de-entrevista-backend", "preguntas-entrevista-backend" }, "backend", "backend"),
        (new[] { "preguntas-de-entrevista-fullstack", "preguntas-entrevista-full-stack" }, "fullstack", "fullstack"),
        (new[] { "preguntas-entrevista-git", "preguntas-de-entrevista-de-git" }, "git", "git"),
        (new[] { "preguntas-de-entrevista-aws", "preguntas-entrevista-aws" }, "aws", "aws"),
        (new[] { "preguntas-entrevista-kubernetes", "preguntas-de-kubernetes" }, "kubernetes", "kubernetes"),
        (new[] { "preguntas-entrevista-typescript", "preguntas-de-entrevista-typescript" }, "typescript", "typescript"),
        (new[] { "preguntas-entrevista-css", "preguntas-de-entrevista-css" }, "frontend", "css"),
        (new[] { "preguntas-entrevista-html", "preguntas-de-entrevista-html" }, "frontend", "html"),
    };

    public override string SourceName => "KeepCoding";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public KeepCodingScraper(
        ILogger<KeepCodingScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[KeepCoding] Iniciando scraping — {Count} tecnologías", TechArticles.Count);

        // Paso 1: Intentar descubrir artículos vía sitemap
        var sitemapUrls = await DiscoverFromSitemapAsync(cancellationToken);
        foreach (var sitemapUrl in sitemapUrls)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                await ApplyRateLimitAsync(cancellationToken);
                var count = await ScrapeArticleAsync(sitemapUrl, "general", "general", result, cancellationToken);
                if (count > 0)
                    Logger.LogInformation("[KeepCoding] Sitemap: {Count} preguntas — {Url}", count, sitemapUrl);
            }
            catch (Exception ex)
            {
                Logger.LogDebug("[KeepCoding] Error sitemap URL {Url}: {Msg}", sitemapUrl, ex.Message);
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

                // Saltar si ya lo procesamos del sitemap
                if (sitemapUrls.Contains(url)) continue;

                try
                {
                    await ApplyRateLimitAsync(cancellationToken);
                    var count = await ScrapeArticleAsync(url, category, tech, result, cancellationToken);
                    if (count > 0)
                    {
                        Logger.LogInformation("[KeepCoding] {Tech}: {Count} preguntas — {Url}", tech, count, url);
                        break; // Si encontramos uno que funciona, no probar más variantes
                    }
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Logger.LogDebug("[KeepCoding] {Url} → {Code}", url, ex.StatusCode);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug("[KeepCoding] Error en {Url}: {Msg}", url, ex.Message);
                }
            }
        }

        result.Success = true;
        result.TotalQuestionsFound = result.Questions.Count;
        sw.Stop();
        result.Duration = sw.Elapsed;

        Logger.LogInformation("[KeepCoding] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
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

            var sitemapXml = await HttpClient.GetStringAsync("https://keepcoding.io/sitemap.xml", ct);

            // Buscar URLs que contengan "pregunta" o "entrevista"
            var urlPattern = new System.Text.RegularExpressions.Regex(
                @"<loc>(https?://keepcoding\.io/blog/[^<]*(?:pregunta|entrevista)[^<]*)</loc>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in urlPattern.Matches(sitemapXml))
            {
                urls.Add(match.Groups[1].Value);
            }

            // También buscar en sub-sitemaps si existen
            var subSitemapPattern = new System.Text.RegularExpressions.Regex(
                @"<loc>(https?://keepcoding\.io/[^<]*sitemap[^<]*\.xml)</loc>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in subSitemapPattern.Matches(sitemapXml))
            {
                try
                {
                    await ApplyRateLimitAsync(ct);
                    var subXml = await HttpClient.GetStringAsync(match.Groups[1].Value, ct);
                    foreach (System.Text.RegularExpressions.Match subMatch in urlPattern.Matches(subXml))
                    {
                        urls.Add(subMatch.Groups[1].Value);
                    }
                }
                catch { /* ignore sub-sitemap errors */ }
            }

            if (urls.Count > 0)
                Logger.LogInformation("[KeepCoding] Descubiertas {Count} URLs de entrevista en sitemap", urls.Count);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("[KeepCoding] Sitemap no disponible: {Msg}", ex.Message);
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
            q.Tags = $"[\"{tech}\",\"{category}\",\"interview\",\"keepcoding\"]";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }
}
