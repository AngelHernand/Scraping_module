using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.RefactoringGuru;

/// <summary>
/// Scraper de Refactoring.Guru — patrones de diseño, refactoring, SOLID.
/// Contenido de altísima calidad para entrevistas de arquitectura y diseño OO.
/// </summary>
public class RefactoringGuruScraper : BaseScraper
{
    public override string SourceName => "RefactoringGuru";
    public override SourceType SourceType => SourceType.EducationalPlatform;

    private static readonly (string Path, string Tech, ContentType CType)[] Pages =
    [
        // Design Patterns - Creational
        ("design-patterns/factory-method", "Design Patterns", ContentType.Pattern),
        ("design-patterns/abstract-factory", "Design Patterns", ContentType.Pattern),
        ("design-patterns/builder", "Design Patterns", ContentType.Pattern),
        ("design-patterns/prototype", "Design Patterns", ContentType.Pattern),
        ("design-patterns/singleton", "Design Patterns", ContentType.Pattern),

        // Design Patterns - Structural
        ("design-patterns/adapter", "Design Patterns", ContentType.Pattern),
        ("design-patterns/bridge", "Design Patterns", ContentType.Pattern),
        ("design-patterns/composite", "Design Patterns", ContentType.Pattern),
        ("design-patterns/decorator", "Design Patterns", ContentType.Pattern),
        ("design-patterns/facade", "Design Patterns", ContentType.Pattern),
        ("design-patterns/flyweight", "Design Patterns", ContentType.Pattern),
        ("design-patterns/proxy", "Design Patterns", ContentType.Pattern),

        // Design Patterns - Behavioral
        ("design-patterns/chain-of-responsibility", "Design Patterns", ContentType.Pattern),
        ("design-patterns/command", "Design Patterns", ContentType.Pattern),
        ("design-patterns/iterator", "Design Patterns", ContentType.Pattern),
        ("design-patterns/mediator", "Design Patterns", ContentType.Pattern),
        ("design-patterns/memento", "Design Patterns", ContentType.Pattern),
        ("design-patterns/observer", "Design Patterns", ContentType.Pattern),
        ("design-patterns/state", "Design Patterns", ContentType.Pattern),
        ("design-patterns/strategy", "Design Patterns", ContentType.Pattern),
        ("design-patterns/template-method", "Design Patterns", ContentType.Pattern),
        ("design-patterns/visitor", "Design Patterns", ContentType.Pattern),

        // Refactoring
        ("refactoring", "Refactoring", ContentType.Guide),
        ("refactoring/what-is-refactoring", "Refactoring", ContentType.Article),
        ("refactoring/smells", "Refactoring", ContentType.Guide),
        ("refactoring/techniques", "Refactoring", ContentType.Guide),

        // SOLID
        ("design-patterns/solid-principles", "SOLID", ContentType.Guide),
    ];

    public RefactoringGuruScraper(
        ILogger<RefactoringGuruScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapingResult();
        Logger.LogInformation("[{Source}] Iniciando scraping de Refactoring.Guru", SourceName);

        foreach (var (path, tech, ctype) in Pages)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Inglés
                var enUrl = $"https://refactoring.guru/{path}";
                await ScrapePageAsync(enUrl, tech, ctype, result, cancellationToken);
                await ApplyRateLimitAsync(cancellationToken);

                // Español
                var esUrl = $"https://refactoring.guru/es/{path}";
                await ScrapePageAsync(esUrl, tech, ctype, result, cancellationToken);
                await ApplyRateLimitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[{Source}] Error: {Path}", SourceName, path);
                result.Errors.Add($"Error en {path}: {ex.Message}");
            }
        }

        result.TotalDocumentsFound = result.Documents.Count;
        result.TotalQuestionsFound = result.Questions.Count;
        Logger.LogInformation("[{Source}] Completado: {Docs} documentos", SourceName, result.Documents.Count);

        return result;
    }

    private async Task ScrapePageAsync(string url, string technology, ContentType contentType,
        ScrapingResult result, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GetRandomUserAgent());

        var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content')]")
            ?? doc.DocumentNode.SelectSingleNode("//main");

        if (contentNode == null) return;

        var pageTitle = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim()
            ?? "Refactoring.Guru";

        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "RefactoringGuru",
            sourceId: 0, technology: technology, contentType: contentType);
        result.Documents.AddRange(docs);

        Logger.LogDebug("[{Source}] {Url}: {Count} docs", SourceName, url, docs.Count);
    }
}
