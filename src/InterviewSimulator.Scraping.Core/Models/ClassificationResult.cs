using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Models;

// Resultado de la clasificación de una pregunta.
public class ClassificationResult
{
    public QuestionCategory Category { get; set; }
    public string? Subcategory { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Technology { get; set; }
    public double ConfidenceScore { get; set; }
}
