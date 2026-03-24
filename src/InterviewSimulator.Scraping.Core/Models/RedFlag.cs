namespace InterviewSimulator.Scraping.Core.Models;

public class RedFlag
{
    public int Id { get; set; }
    public int BehavioralQuestionId { get; set; }
    public string FlagText { get; set; } = string.Empty;

    public BehavioralQuestion BehavioralQuestion { get; set; } = null!;
}
