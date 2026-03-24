using System.Text.Json;
using InterviewSimulator.Scraping.Core.Interfaces;
using InterviewSimulator.Scraping.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.Scraping.Data.Repositories;

public class BehavioralQuestionRepository : IBehavioralQuestionRepository
{
    private readonly ScrapingDbContext _context;
    private readonly ILogger<BehavioralQuestionRepository> _logger;

    public BehavioralQuestionRepository(ScrapingDbContext context, ILogger<BehavioralQuestionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<BehavioralQuestion>> GetRandomQuestionsAsync(int count, string? difficulty = null)
    {
        var query = _context.BehavioralQuestions
            .Where(q => q.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(difficulty))
            query = query.Where(q => q.Difficulty == difficulty);

        // Agrupar por categoría, tomar 1 al azar de cada grupo
        var onePerCategory = await query
            .GroupBy(q => q.Category)
            .Select(g => g.OrderBy(_ => EF.Functions.Random()).First())
            .ToListAsync();

        // Del resultado, tomar N al azar
        var selectedIds = onePerCategory
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(q => q.Id)
            .ToList();

        // Cargar con includes
        return await _context.BehavioralQuestions
            .Where(q => selectedIds.Contains(q.Id))
            .Include(q => q.EvaluationCriteria.OrderBy(e => e.OrderIndex))
            .Include(q => q.RedFlags)
            .Include(q => q.ExampleAnswers)
            .ToListAsync();
    }

    public async Task<BehavioralQuestion?> GetByIdAsync(int id)
    {
        return await _context.BehavioralQuestions
            .Include(q => q.EvaluationCriteria.OrderBy(e => e.OrderIndex))
            .Include(q => q.RedFlags)
            .Include(q => q.ExampleAnswers)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.BehavioralQuestions
            .Where(q => q.IsActive)
            .CountAsync();
    }

    public async Task<List<string>> GetAvailableCategoriesAsync()
    {
        return await _context.BehavioralQuestions
            .Where(q => q.IsActive)
            .Select(q => q.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task SeedFromJsonAsync(string jsonContent)
    {
        var existingCount = await _context.BehavioralQuestions.CountAsync();
        if (existingCount > 0)
        {
            _logger.LogInformation("La tabla BehavioralQuestions ya tiene {Count} registros. Verificando duplicados...", existingCount);
        }

        var existingTexts = await _context.BehavioralQuestions
            .Select(q => q.QuestionText)
            .ToListAsync();

        var existingSet = new HashSet<string>(existingTexts, StringComparer.OrdinalIgnoreCase);

        var seedData = JsonSerializer.Deserialize<SeedRoot>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (seedData?.Questions == null || seedData.Questions.Count == 0)
        {
            _logger.LogWarning("El archivo JSON de seed no contiene preguntas");
            return;
        }

        var newQuestions = new List<BehavioralQuestion>();

        foreach (var q in seedData.Questions)
        {
            if (existingSet.Contains(q.QuestionText))
                continue;

            var question = new BehavioralQuestion
            {
                QuestionText = q.QuestionText,
                Category = q.Category,
                Competency = q.Competency,
                EvaluationMethod = q.EvaluationMethod,
                Difficulty = q.Difficulty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            if (q.EvaluationCriteria != null)
            {
                for (int i = 0; i < q.EvaluationCriteria.Count; i++)
                {
                    question.EvaluationCriteria.Add(new EvaluationCriteria
                    {
                        CriteriaText = q.EvaluationCriteria[i],
                        Weight = 1,
                        OrderIndex = i
                    });
                }
            }

            if (q.RedFlags != null)
            {
                foreach (var flag in q.RedFlags)
                {
                    question.RedFlags.Add(new RedFlag { FlagText = flag });
                }
            }

            if (!string.IsNullOrWhiteSpace(q.ExampleGoodAnswer))
            {
                question.ExampleAnswers.Add(new ExampleAnswer
                {
                    AnswerText = q.ExampleGoodAnswer,
                    Score = 9
                });
            }

            newQuestions.Add(question);
        }

        if (newQuestions.Count > 0)
        {
            await _context.BehavioralQuestions.AddRangeAsync(newQuestions);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Se insertaron {Count} preguntas conductuales nuevas", newQuestions.Count);
        }
        else
        {
            _logger.LogInformation("No hay preguntas conductuales nuevas para insertar");
        }
    }

    private sealed class SeedRoot
    {
        public List<SeedQuestion> Questions { get; set; } = new();
    }

    private sealed class SeedQuestion
    {
        public string QuestionText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Competency { get; set; } = string.Empty;
        public string EvaluationMethod { get; set; } = "STAR";
        public string Difficulty { get; set; } = "Junior";
        public List<string>? EvaluationCriteria { get; set; }
        public List<string>? RedFlags { get; set; }
        public string? ExampleGoodAnswer { get; set; }
    }
}
