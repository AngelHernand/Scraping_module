namespace InterviewSimulator.Scraping.Core.Models;

public class BehavioralQuestion
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Competency { get; set; } = string.Empty;
    public string EvaluationMethod { get; set; } = "STAR";
    public string Difficulty { get; set; } = "Junior";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<EvaluationCriteria> EvaluationCriteria { get; set; } = new List<EvaluationCriteria>();
    public ICollection<RedFlag> RedFlags { get; set; } = new List<RedFlag>();
    public ICollection<ExampleAnswer> ExampleAnswers { get; set; } = new List<ExampleAnswer>();
}
