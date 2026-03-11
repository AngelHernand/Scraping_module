using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewSimulator.Scraping.Core.Configuration;
using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;
using InterviewSimulator.Scraping.Scrapers.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.Scraping.Scrapers.LeetCode;

// Scraper para LeetCode usando su API GraphQL pública.
// Extrae problemas de coding interviews.
public class LeetCodeScraper : BaseScraper
{
    private const string GraphQlEndpoint = "https://leetcode.com/graphql";

    private static readonly string[] InterviewLists =
    {
        "top-interview-150",
        "top-100-liked-questions"
    };

    private static readonly Dictionary<string, string> TopicMapping = new()
    {
        { "Array", "Algoritmos y Estructuras de Datos" },
        { "String", "Algoritmos y Estructuras de Datos" },
        { "Hash Table", "Algoritmos y Estructuras de Datos" },
        { "Dynamic Programming", "Algoritmos y Estructuras de Datos" },
        { "Math", "Algoritmos y Estructuras de Datos" },
        { "Sorting", "Algoritmos y Estructuras de Datos" },
        { "Greedy", "Algoritmos y Estructuras de Datos" },
        { "Depth-First Search", "Algoritmos y Estructuras de Datos" },
        { "Binary Search", "Algoritmos y Estructuras de Datos" },
        { "Tree", "Algoritmos y Estructuras de Datos" },
        { "Graph", "Algoritmos y Estructuras de Datos" },
        { "Linked List", "Algoritmos y Estructuras de Datos" },
        { "Stack", "Algoritmos y Estructuras de Datos" },
        { "Queue", "Algoritmos y Estructuras de Datos" },
        { "Heap (Priority Queue)", "Algoritmos y Estructuras de Datos" },
        { "Database", "Bases de Datos" },
        { "SQL", "Bases de Datos" },
        { "Shell", "Sistemas Operativos" },
        { "Concurrency", "Sistemas Operativos" }
    };

    public override string SourceName => "LeetCode";
    public override SourceType SourceType => SourceType.CodingPlatform;

    public LeetCodeScraper(
        ILogger<LeetCodeScraper> logger,
        IOptions<ScrapingSettings> settings,
        IHttpClientFactory httpClientFactory)
        : base(logger, settings, httpClientFactory)
    {
    }

    public override async Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ScrapingResult();
        var scraperSettings = Settings.Scrapers.GetValueOrDefault("LeetCode");
        var maxProblems = scraperSettings?.MaxProblems ?? 150;

        Logger.LogInformation("[LeetCode] Iniciando scraping via GraphQL...");

        try
        {
            foreach (var listId in InterviewLists)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    Logger.LogInformation("[LeetCode] Obteniendo lista: {ListId}", listId);

                    var problems = await FetchProblemListAsync(listId, maxProblems, cancellationToken);
                    if (problems == null) continue;

                    foreach (var problem in problems)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (problem.IsPaidOnly) continue; // Solo problemas gratuitos

                        try
                        {
                            // Crear pregunta estilo entrevista a partir del problema
                            var questionText = FormatAsInterviewQuestion(problem);
                            var difficulty = MapDifficulty(problem.Difficulty);
                            var tags = problem.TopicTags?.Select(t => t.Name).ToList() ?? new List<string>();
                            var subcategory = DetermineSubcategory(tags);

                            var scrapedQuestion = CreateScrapedQuestion(
                                questionText,
                                $"https://leetcode.com/problems/{problem.TitleSlug}/",
                                problem.Content,
                                sourceId: 0
                            );

                            scrapedQuestion.Category = QuestionCategory.Technical;
                            scrapedQuestion.Subcategory = subcategory;
                            scrapedQuestion.DifficultyLevel = difficulty;
                            scrapedQuestion.Tags = JsonSerializer.Serialize(tags);

                            result.Questions.Add(scrapedQuestion);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "[LeetCode] Error procesando problema: {Title}", problem.Title);
                            result.Errors.Add($"Error en problema '{problem.Title}': {ex.Message}");
                        }
                    }

                    await ApplyRateLimitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[LeetCode] Error obteniendo lista: {ListId}", listId);
                    result.Errors.Add($"Error en lista '{listId}': {ex.Message}");
                }
            }

            result.Success = true;
            result.TotalQuestionsFound = result.Questions.Count;
            Logger.LogInformation("[LeetCode] Scraping completado. {Count} preguntas encontradas", result.TotalQuestionsFound);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[LeetCode] Error fatal durante scraping");
            result.Success = false;
            result.Errors.Add($"Error fatal: {ex.Message}");
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private async Task<List<LeetCodeProblem>?> FetchProblemListAsync(string listId, int limit, CancellationToken ct)
    {
        var query = @"
            query problemsetQuestionList($categorySlug: String, $limit: Int, $skip: Int, $filters: QuestionListFilterInput) {
                problemsetQuestionList: questionList(
                    categorySlug: $categorySlug
                    limit: $limit
                    skip: $skip
                    filters: $filters
                ) {
                    total: totalNum
                    questions: data {
                        questionId
                        questionFrontendId
                        title
                        titleSlug
                        difficulty
                        topicTags {
                            name
                            slug
                        }
                        content
                        isPaidOnly
                    }
                }
            }";

        var variables = new
        {
            categorySlug = "all-code-essentials",
            skip = 0,
            limit,
            filters = new { listId }
        };

        var requestBody = new { query, variables };

        var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
        {
            Content = JsonContent.Create(requestBody)
        };

        request.Headers.Add("Referer", "https://leetcode.com");
        request.Headers.Add("Origin", "https://leetcode.com");
        request.Headers.Add("User-Agent", GetRandomUserAgent());

        var response = await HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var jsonResponse = await response.Content.ReadFromJsonAsync<LeetCodeGraphQlResponse>(options, ct);

        return jsonResponse?.Data?.ProblemsetQuestionList?.Questions;
    }

    private static string FormatAsInterviewQuestion(LeetCodeProblem problem)
    {
        var tags = problem.TopicTags?.Select(t => t.Name).ToList() ?? new List<string>();
        var tagContext = tags.Count > 0 ? $" ({string.Join(", ", tags.Take(3))})" : "";

        return $"{problem.Title}{tagContext}: {StripHtml(problem.Content ?? problem.Title).Take(500)}. What is the optimal approach and its time complexity?";
    }

    private static DifficultyLevel MapDifficulty(string difficulty) => difficulty?.ToLower() switch
    {
        "easy" => DifficultyLevel.Junior,
        "medium" => DifficultyLevel.Mid,
        "hard" => DifficultyLevel.Senior,
        _ => DifficultyLevel.Unknown
    };

    private static string DetermineSubcategory(List<string> tags)
    {
        foreach (var tag in tags)
        {
            if (TopicMapping.TryGetValue(tag, out var subcategory))
                return subcategory;
        }
        return "Algoritmos y Estructuras de Datos";
    }

    #region DTO Models

    private class LeetCodeGraphQlResponse
    {
        public LeetCodeData? Data { get; set; }
    }

    private class LeetCodeData
    {
        public LeetCodeQuestionList? ProblemsetQuestionList { get; set; }
    }

    private class LeetCodeQuestionList
    {
        public int Total { get; set; }
        public List<LeetCodeProblem>? Questions { get; set; }
    }

    private class LeetCodeProblem
    {
        public string QuestionId { get; set; } = string.Empty;
        public string QuestionFrontendId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TitleSlug { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public List<TopicTag>? TopicTags { get; set; }
        public string? Content { get; set; }
        public bool IsPaidOnly { get; set; }
    }

    private class TopicTag
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }

    #endregion
}
