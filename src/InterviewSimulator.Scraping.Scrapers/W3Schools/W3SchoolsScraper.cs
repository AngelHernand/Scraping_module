using HtmlAgilityPack;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.W3Schools;

/// <summary>
/// Scraper de W3Schools — tutoriales de HTML, CSS, JavaScript, SQL, Python, C#, Java, etc.
/// Excelente contenido para nivel principiante e intermedio.
/// </summary>
public class W3SchoolsScraper : BaseScraper
{
    public override string SourceName => "W3Schools";
    public override SourceType SourceType => SourceType.EducationalPlatform;

    private static readonly (string Path, string Tech)[] TutorialPaths =
    [
        // JavaScript
        ("js/js_intro.asp", "JavaScript"), ("js/js_variables.asp", "JavaScript"),
        ("js/js_arrays.asp", "JavaScript"), ("js/js_functions.asp", "JavaScript"),
        ("js/js_objects.asp", "JavaScript"), ("js/js_classes.asp", "JavaScript"),
        ("js/js_async.asp", "JavaScript"), ("js/js_promises.asp", "JavaScript"),
        ("js/js_json.asp", "JavaScript"), ("js/js_es6.asp", "JavaScript"),

        // Python
        ("python/python_intro.asp", "Python"), ("python/python_lists.asp", "Python"),
        ("python/python_dictionaries.asp", "Python"), ("python/python_functions.asp", "Python"),
        ("python/python_classes.asp", "Python"), ("python/python_inheritance.asp", "Python"),
        ("python/python_iterators.asp", "Python"), ("python/python_lambda.asp", "Python"),

        // SQL
        ("sql/sql_intro.asp", "SQL"), ("sql/sql_select.asp", "SQL"),
        ("sql/sql_join.asp", "SQL"), ("sql/sql_groupby.asp", "SQL"),
        ("sql/sql_having.asp", "SQL"), ("sql/sql_create_table.asp", "SQL"),
        ("sql/sql_indexes.asp", "SQL"), ("sql/sql_stored_procedures.asp", "SQL"),

        // HTML/CSS
        ("html/html_intro.asp", "HTML"), ("html/html5_semantic_elements.asp", "HTML"),
        ("css/css_intro.asp", "CSS"), ("css/css3_flexbox.asp", "CSS"),
        ("css/css_grid.asp", "CSS"), ("css/css_rwd_intro.asp", "CSS"),

        // C#
        ("cs/cs_intro.asp", "C#"), ("cs/cs_classes.asp", "C#"),
        ("cs/cs_oop.asp", "C#"), ("cs/cs_inheritance.asp", "C#"),
        ("cs/cs_polymorphism.asp", "C#"), ("cs/cs_interface.asp", "C#"),

        // Java
        ("java/java_intro.asp", "Java"), ("java/java_oop.asp", "Java"),
        ("java/java_classes.asp", "Java"), ("java/java_inheritance.asp", "Java"),
        ("java/java_interface.asp", "Java"), ("java/java_arraylist.asp", "Java"),

        // TypeScript
        ("typescript/typescript_intro.asp", "TypeScript"),
        ("typescript/typescript_simple_types.asp", "TypeScript"),

        // React
        ("react/react_intro.asp", "React"), ("react/react_components.asp", "React"),
        ("react/react_hooks.asp", "React"), ("react/react_usestate.asp", "React"),
        ("react/react_useeffect.asp", "React"),

        // Node.js
        ("nodejs/nodejs_intro.asp", "Node.js"), ("nodejs/nodejs_npm.asp", "Node.js"),
        ("nodejs/nodejs_http.asp", "Node.js"),

        // Git
        ("git/git_intro.asp", "Git"), ("git/git_branch.asp", "Git"),

        // DSA
        ("dsa/dsa_intro.asp", "DSA"), ("dsa/dsa_arrays.asp", "DSA"),
        ("dsa/dsa_linked_lists.asp", "DSA"), ("dsa/dsa_stacks.asp", "DSA"),
        ("dsa/dsa_queues.asp", "DSA"), ("dsa/dsa_binary_trees.asp", "DSA"),
        ("dsa/dsa_sorting.asp", "DSA"), ("dsa/dsa_binary_search.asp", "DSA"),
    ];

    public W3SchoolsScraper(
        ILogger<W3SchoolsScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ScrapingResult();
        Logger.LogInformation("[{Source}] Iniciando scraping de W3Schools", SourceName);

        foreach (var (path, tech) in TutorialPaths)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var url = $"https://www.w3schools.com/{path}";
                await ScrapeTutorialAsync(url, tech, result, cancellationToken);
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

    private async Task ScrapeTutorialAsync(string url, string technology, ScrapingResult result, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", GetRandomUserAgent());

        var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return;

        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='main']")
            ?? doc.DocumentNode.SelectSingleNode("//div[@id='mainLe498']")
            ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'w3-main')]");

        if (contentNode == null) return;

        // Limpiar navegación y ads
        var garbage = contentNode.SelectNodes(
            ".//div[contains(@class,'w3-sidebar')] | .//div[@id='midcontentadcontainer'] | .//script | .//style");
        if (garbage != null)
            foreach (var g in garbage) g.Remove();

        var pageTitle = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim()
            ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim()
            ?? "W3Schools Tutorial";

        var docs = ExtractDocumentsFromHtml(
            contentNode.InnerHtml, pageTitle, url, "W3Schools",
            sourceId: 0, technology: technology, contentType: ContentType.Tutorial);
        result.Documents.AddRange(docs);

        Logger.LogDebug("[{Source}] {Url}: {Count} docs", SourceName, url, docs.Count);
    }
}
