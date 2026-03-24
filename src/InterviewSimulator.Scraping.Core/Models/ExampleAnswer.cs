namespace InterviewSimulator.Scraping.Core.Models;

public class ExampleAnswer
{
    public int Id { get; set; }
    public int BehavioralQuestionId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public int Score { get; set; }

    public BehavioralQuestion BehavioralQuestion { get; set; } = null!;
}
