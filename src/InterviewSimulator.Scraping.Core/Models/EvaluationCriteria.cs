namespace InterviewSimulator.Scraping.Core.Models;

public class EvaluationCriteria
{
    public int Id { get; set; }
    public int BehavioralQuestionId { get; set; }
    public string CriteriaText { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public int OrderIndex { get; set; }

    public BehavioralQuestion BehavioralQuestion { get; set; } = null!;
}
