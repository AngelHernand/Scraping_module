using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.MicrosoftLearn;

/// <summary>
/// Scraper de Microsoft Learn — documentación oficial de .NET, C#, ASP.NET Core, EF Core, Azure.
/// Optimizado para RAG corpus (documentos técnicos de referencia).
/// </summary>
public class MicrosoftLearnScraper : BaseScraper
{
    public override string SourceName => "MicrosoftLearn";
    public override SourceType SourceType => SourceType.OfficialDocumentation;

    /// <summary>
    /// Rutas de documentación a scrapear. Cada tupla = (ruta relativa, tecnología).
    /// Se construye la URL: https://learn.microsoft.com/en-us/dotnet/{ruta}
    /// </summary>
    private static readonly (string Path, string Tech, string BaseUrl)[] DocumentationPaths =
    [
        // C# Language
        ("dotnet/csharp/fundamentals/types/", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/csharp/fundamentals/object-oriented/", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/csharp/programming-guide/generics/", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/csharp/linq/", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/csharp/asynchronous-programming/", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/csharp/language-reference/keywords/", "C#", "https://learn.microsoft.com/en-us/"),

        // .NET Core
        ("dotnet/core/extensions/dependency-injection", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/core/extensions/logging", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/core/extensions/configuration", "C#", "https://learn.microsoft.com/en-us/"),
        ("dotnet/architecture/microservices/", ".NET", "https://learn.microsoft.com/en-us/"),

        // ASP.NET Core
        ("aspnet/core/fundamentals/", "ASP.NET Core", "https://learn.microsoft.com/en-us/"),
        ("aspnet/core/web-api/", "ASP.NET Core", "https://learn.microsoft.com/en-us/"),
        ("aspnet/core/mvc/overview", "ASP.NET Core", "https://learn.microsoft.com/en-us/"),
        ("aspnet/core/security/authentication/", "ASP.NET Core", "https://learn.microsoft.com/en-us/"),
        ("aspnet/core/signalr/introduction", "ASP.NET Core", "https://learn.microsoft.com/en-us/"),
        ("aspnet/core/blazor/", "ASP.NET Core", "https://learn.microsoft.com/en-us/"),

        // Entity Framework Core
        ("ef/core/", "Entity Framework", "https://learn.microsoft.com/en-us/"),
        ("ef/core/querying/", "Entity Framework", "https://learn.microsoft.com/en-us/"),
        ("ef/core/modeling/", "Entity Framework", "https://learn.microsoft.com/en-us/"),
        ("ef/core/saving/", "Entity Framework", "https://learn.microsoft.com/en-us/"),

        // Azure
        ("azure/architecture/best-practices/api-design", "Azure", "https://learn.microsoft.com/en-us/"),
        ("azure/architecture/patterns/", "Azure", "https://learn.microsoft.com/en-us/"),

        // Versiones español
        ("dotnet/csharp/fundamentals/types/", "C#", "https://learn.microsoft.com/es-es/"),
        ("dotnet/csharp/linq/", "C#", "https://learn.microsoft.com/es-es/"),
        ("aspnet/core/fundamentals/", "ASP.NET Core", "https://learn.microsoft.com/es-es/"),
    ];

    public MicrosoftLearnScraper(
        ILogger<MicrosoftLearnScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapingResult();
        Logger.LogInformation("[{Source}] Iniciando scraping de documentación Microsoft Learn", SourceName);

        foreach (var (path, tech, baseUrl) in DocumentationPaths)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var url = baseUrl + path;
                await ScrapeDocPageAsync(url, tech, result, cancellationToken);
                await ApplyRateLimitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[{Source}] Error scrapeando path: {Path}", SourceName, path);
                result.Errors.Add($"Error en {path}: {ex.Message}");
            }
        }

        result.TotalDocumentsFound = result.Documents.Count;
        result.TotalQuestionsFound = result.Questions.Count;
        Logger.LogInformation("[{Source}] Completado: {Docs} documentos, {QA} preguntas extraídos",
            SourceName, result.Documents.Count, result.Questions.Count);

        return result;
    }

    private async Task ScrapeDocPageAsync(string url, string technology, ScrapingResult result, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GetRandomUserAgent());
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9,es;q=0.8");

        var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogDebug("[{Source}] HTTP {Code} para {Url}", SourceName, response.StatusCode, url);
            return;
        }

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Contenido principal de MS Learn
        var contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='main-column']")
            ?? doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode.SelectSingleNode("//article");

        if (contentNode == null) return;

        // Limpiar nodos innecesarios
        RemoveNodes(contentNode, [
            "//nav", "//aside", "//footer", "//header",
            "//div[contains(@class,'feedback')]",
            "//div[contains(@class,'navigation')]",
            "//div[contains(@class,'breadcrumb')]",
            "//div[contains(@class,'metadata')]"
        ]);

        var pageTitle = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim()
            ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim()
            ?? "Microsoft Learn Documentation";

        // Extraer documentos RAG
        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "MicrosoftLearn",
            sourceId: 0, technology: technology, contentType: ContentType.Documentation);
        result.Documents.AddRange(docs);

        // También extraer Q&A si hay patrones de preguntas
        var questions = ExtractQuestionsWithAnswersFromText(contentNode.InnerHtml);
        foreach (var (q, a) in questions)
        {
            var question = CreateScrapedQuestion(q, url, null, 0, a);
            result.Questions.Add(question);
        }

        Logger.LogDebug("[{Source}] {Url}: {Docs} docs, {QA} Q&A", SourceName, url, docs.Count, questions.Count);
    }

    private static void RemoveNodes(HtmlNode root, string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var nodes = root.SelectNodes(xpath);
            if (nodes == null) continue;
            foreach (var node in nodes)
                node.Remove();
        }
    }
}
