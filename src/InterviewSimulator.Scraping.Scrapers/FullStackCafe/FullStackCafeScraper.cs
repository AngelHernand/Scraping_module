using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.FullStackCafe;

/// <summary>
/// Scraper para FullStack.Cafe (repositorio GitHub aershov24/full-stack-interview-questions).
/// Los archivos Markdown contienen preguntas + respuestas organizadas por tecnología.
/// Usa la API REST de GitHub para listar y descargar los archivos sin necesidad de HTML parsing.
/// </summary>
public class FullStackCafeScraper : BaseScraper
{
    private const string RepoApiUrl = "https://api.github.com/repos/aershov24/full-stack-interview-questions/contents/";
    private const string RawBaseUrl  = "https://raw.githubusercontent.com/aershov24/full-stack-interview-questions/master/";

    // Mapa de nombre de archivo (sin extensión, lowercase) → categoría semántica
    private static readonly Dictionary<string, string> CategoryByFilename = new(StringComparer.OrdinalIgnoreCase)
    {
        { "javascript",     "javascript" },
        { "typescript",     "typescript" },
        { "python",         "python" },
        { "java",           "java" },
        { "csharp",         "csharp" },
        { "dotnet",         "dotnet" },
        { "node",           "nodejs" },
        { "nodejs",         "nodejs" },
        { "react",          "react" },
        { "angular",        "angular" },
        { "vue",            "vuejs" },
        { "sql",            "sql" },
        { "nosql",          "database" },
        { "mongodb",        "mongodb" },
        { "postgresql",     "postgresql" },
        { "mysql",          "mysql" },
        { "redis",          "redis" },
        { "docker",         "docker" },
        { "kubernetes",     "kubernetes" },
        { "aws",            "aws" },
        { "azure",          "azure" },
        { "git",            "git" },
        { "linux",          "linux" },
        { "system-design",  "system-design" },
        { "design-patterns","design-patterns" },
        { "algorithms",     "algorithms" },
        { "data-structures","data-structures" },
        { "microservices",  "microservices" },
        { "rest-api",       "rest-api" },
        { "graphql",        "graphql" },
        { "devops",         "devops" },
        { "security",       "security" },
        { "testing",        "testing" },
        { "spring",         "spring-boot" },
        { "spring-boot",    "spring-boot" },
        { "html",           "frontend" },
        { "css",            "frontend" },
        { "php",            "php" },
        { "go",             "go" },
        { "rust",           "rust" },
        { "kotlin",         "kotlin" },
    };

    public override string SourceName => "FullStackCafe";
    public override SourceType SourceType => SourceType.BlogPlatform;

    public FullStackCafeScraper(
        ILogger<FullStackCafeScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();

        Logger.LogInformation("[FullStackCafe] Iniciando — descargando lista de archivos desde GitHub API");

        try
        {
            // 1. Obtener lista de archivos del repositorio
            var files = await ListRepoFilesAsync(cancellationToken);

            if (files == null || files.Count == 0)
            {
                Logger.LogWarning("[FullStackCafe] No se obtuvieron archivos del repositorio");
                result.Errors.Add("No se encontraron archivos en la API de GitHub");
                result.Success = false;
                return result;
            }

            Logger.LogInformation("[FullStackCafe] {Count} archivos encontrados en el repositorio", files.Count);

            // 2. Filtrar solo archivos .md
            var mdFiles = files
                .Where(f => f.Type == "file" &&
                            f.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                            !f.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Logger.LogInformation("[FullStackCafe] {Count} archivos Markdown elegibles", mdFiles.Count);

            // 3. Procesar cada archivo Markdown
            foreach (var file in mdFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var category = InferCategory(file.Name);
                    var rawUrl   = $"{RawBaseUrl}{Uri.EscapeDataString(file.Name)}";
                    var docUrl   = $"https://github.com/aershov24/full-stack-interview-questions/blob/master/{Uri.EscapeDataString(file.Name)}";

                    var count = await ProcessMarkdownFileAsync(rawUrl, docUrl, category, result, cancellationToken);
                    Logger.LogInformation("[FullStackCafe] {File}: {Count} preguntas", file.Name, count);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[FullStackCafe] Error procesando {File}", file.Name);
                    result.Errors.Add($"Error en {file.Name}: {ex.Message}");
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[FullStackCafe] Error fatal");
            result.Success = false;
            result.Errors.Add($"Error fatal: {ex.Message}");
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        Logger.LogInformation("[FullStackCafe] Completado — {Total} preguntas en {Elapsed:mm\\:ss}",
            result.TotalQuestionsFound, sw.Elapsed);
        return result;
    }

    // ---------------------------------------------------------------------------
    // Listar archivos del repositorio GitHub mediante la REST API
    // ---------------------------------------------------------------------------
    private async Task<List<GitHubFile>> ListRepoFilesAsync(CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "InterviewSimulator-Scraper/1.0");
        HttpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        var response = await HttpClient.GetAsync(RepoApiUrl, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<GitHubFile>>(cancellationToken: ct) ?? new List<GitHubFile>();
    }

    // ---------------------------------------------------------------------------
    // Descargar y procesar un archivo Markdown
    // ---------------------------------------------------------------------------
    private async Task<int> ProcessMarkdownFileAsync(
        string rawUrl,
        string docUrl,
        string category,
        ScrapingResult result,
        CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "InterviewSimulator-Scraper/1.0");

        var response = await HttpClient.GetAsync(rawUrl, ct);
        if (!response.IsSuccessStatusCode) return 0;

        var markdown = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(markdown)) return 0;

        // Usar el extractor base (soporta Markdown headers + numbered lists)
        var qaPairs = ExtractQuestionsWithAnswersFromText(markdown);

        // Fallback: extractor específico para Markdown Q&A con separadores ###
        if (qaPairs.Count == 0)
            qaPairs = ExtractMarkdownQaPairs(markdown);

        int count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (questionText, answerText) in qaPairs)
        {
            if (!seen.Add(questionText.ToLowerInvariant())) continue;
            if (answerText.Length < 30) continue;

            var q = CreateScrapedQuestion(questionText, docUrl, null, sourceId: 0, answerText: answerText);
            q.Category  = QuestionCategory.Technical;
            q.Technology = category;
            q.Subcategory = category;
            q.Tags = $"[\"{category}\",\"interview\",\"fullstackcafe\",\"github\"]";
            q.OriginalLanguage = "en";

            result.Questions.Add(q);
            count++;
        }

        return count;
    }

    // ---------------------------------------------------------------------------
    // Extractor específico de Markdown con formato ### Q: ... **Answer:** ...
    // Formato frecuente en FullStack.Cafe
    // ---------------------------------------------------------------------------
    private static List<(string Question, string Answer)> ExtractMarkdownQaPairs(string markdown)
    {
        var results = new List<(string, string)>();
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Patrón: ### N. ¿Pregunta? -> Answer block
        var pattern = new Regex(
            @"###\s*(?:\d+[\.\)]?\s*)?(.+\?)\s*\n(.*?)(?=###|\z)",
            RegexOptions.Singleline | RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(markdown))
        {
            var q = m.Groups[1].Value.Trim();
            var a = m.Groups[2].Value
                .Replace("**Answer:**", "").Replace("**A:**", "")
                .Trim();

            // Remover bullets y markdown syntax básico de la respuesta
            a = Regex.Replace(a, @"^[-*>]\s*", "", RegexOptions.Multiline);
            a = Regex.Replace(a, @"\*{1,2}([^*]+)\*{1,2}", "$1");
            a = Regex.Replace(a, @"`([^`]+)`", "$1");
            a = Regex.Replace(a, @"\s{2,}", " ").Trim();

            if (q.Length < 20 || q.Length > 500 || a.Length < 30) continue;
            if (!q.Contains('?')) continue;
            if (!seen.Add(q.ToLowerInvariant())) continue;

            results.Add((q, a.Length > 4000 ? a[..4000] : a));
        }

        return results;
    }

    // ---------------------------------------------------------------------------
    // Inferir categoría a partir del nombre del archivo
    // ---------------------------------------------------------------------------
    private static string InferCategory(string filename)
    {
        var keyLower = Path.GetFileNameWithoutExtension(filename).ToLowerInvariant();

        // Buscar coincidencia directa o parcial
        if (CategoryByFilename.TryGetValue(keyLower, out var cat)) return cat;

        foreach (var (key, value) in CategoryByFilename)
        {
            if (keyLower.Contains(key)) return value;
        }

        return "general";
    }

    // ---------------------------------------------------------------------------
    // DTO para respuesta JSON de GitHub API
    // ---------------------------------------------------------------------------
    private sealed class GitHubFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
