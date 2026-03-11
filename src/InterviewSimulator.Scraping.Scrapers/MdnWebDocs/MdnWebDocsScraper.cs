using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.MdnWebDocs;

/// <summary>
/// Scraper de MDN Web Docs — documentación de referencia para HTML, CSS, JavaScript, Web APIs.
/// Fuente autoritativa para desarrollo frontend y estándares web.
/// </summary>
public class MdnWebDocsScraper : BaseScraper
{
    public override string SourceName => "MdnWebDocs";
    public override SourceType SourceType => SourceType.OfficialDocumentation;

    private static readonly (string Path, string Tech)[] DocPaths =
    [
        // JavaScript
        ("Web/JavaScript/Guide", "JavaScript"),
        ("Web/JavaScript/Reference/Global_Objects/Array", "JavaScript"),
        ("Web/JavaScript/Reference/Global_Objects/Promise", "JavaScript"),
        ("Web/JavaScript/Reference/Global_Objects/Map", "JavaScript"),
        ("Web/JavaScript/Reference/Statements/async_function", "JavaScript"),
        ("Web/JavaScript/Closures", "JavaScript"),
        ("Web/JavaScript/Inheritance_and_the_prototype_chain", "JavaScript"),
        ("Web/JavaScript/EventLoop", "JavaScript"),
        ("Web/JavaScript/Memory_management", "JavaScript"),

        // HTML
        ("Web/HTML/Element", "HTML"),
        ("Web/HTML/Attributes", "HTML"),
        ("Learn/HTML/Introduction_to_HTML", "HTML"),

        // CSS
        ("Web/CSS/CSS_flexible_box_layout/Basic_concepts_of_flexbox", "CSS"),
        ("Web/CSS/CSS_grid_layout", "CSS"),
        ("Web/CSS/CSS_selectors", "CSS"),
        ("Learn/CSS/Building_blocks", "CSS"),

        // Web APIs
        ("Web/API/Fetch_API/Using_Fetch", "JavaScript"),
        ("Web/API/Web_Workers_API/Using_web_workers", "JavaScript"),
        ("Web/API/WebSockets_API", "JavaScript"),
        ("Web/API/Service_Worker_API", "JavaScript"),

        // HTTP
        ("Web/HTTP/Overview", "HTTP"),
        ("Web/HTTP/Methods", "HTTP"),
        ("Web/HTTP/Status", "HTTP"),
        ("Web/HTTP/CORS", "HTTP"),
        ("Web/HTTP/Caching", "HTTP"),

        // Performance
        ("Web/Performance", "Web Performance"),
        ("Learn/Performance", "Web Performance"),

        // Security
        ("Web/Security", "Web Security"),

        // Accessibility
        ("Web/Accessibility", "Accessibility"),

        // Versiones español
        ("Web/JavaScript/Guide", "JavaScript"),  // MDN tiene localización a /es/
        ("Web/CSS/CSS_flexible_box_layout/Basic_concepts_of_flexbox", "CSS"),
    ];

    public MdnWebDocsScraper(
        ILogger<MdnWebDocsScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapingResult();
        Logger.LogInformation("[{Source}] Iniciando scraping de MDN Web Docs", SourceName);

        foreach (var (path, tech) in DocPaths)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Intentar versiones EN y ES
                var enUrl = $"https://developer.mozilla.org/en-US/docs/{path}";
                await ScrapeDocPageAsync(enUrl, tech, result, cancellationToken);
                await ApplyRateLimitAsync(cancellationToken);

                // Versión español si existe
                var esUrl = $"https://developer.mozilla.org/es/docs/{path}";
                await ScrapeDocPageAsync(esUrl, tech, result, cancellationToken);
                await ApplyRateLimitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[{Source}] Error scrapeando: {Path}", SourceName, path);
                result.Errors.Add($"Error en {path}: {ex.Message}");
            }
        }

        result.TotalDocumentsFound = result.Documents.Count;
        result.TotalQuestionsFound = result.Questions.Count;
        Logger.LogInformation("[{Source}] Completado: {Docs} documentos extraídos", SourceName, result.Documents.Count);

        return result;
    }

    private async Task ScrapeDocPageAsync(string url, string technology, ScrapingResult result, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GetRandomUserAgent());

        var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = doc.DocumentNode.SelectSingleNode("//article[@id='content']")
            ?? doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode.SelectSingleNode("//article");

        if (contentNode == null) return;

        // Limpiar
        var garbage = contentNode.SelectNodes(
            "//nav | //aside | //footer | //div[contains(@class,'bc-table')] | //div[contains(@class,'metadata')]");
        if (garbage != null)
            foreach (var g in garbage) g.Remove();

        var pageTitle = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim() ?? "MDN Web Docs";

        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "MdnWebDocs",
            sourceId: 0, technology: technology, contentType: ContentType.Documentation);
        result.Documents.AddRange(docs);

        Logger.LogDebug("[{Source}] {Url}: {Count} docs", SourceName, url, docs.Count);
    }
}
