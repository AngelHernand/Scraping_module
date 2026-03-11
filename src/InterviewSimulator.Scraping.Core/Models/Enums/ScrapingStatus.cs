namespace InterviewSimulator.Scraping.Core.Models.Enums;

// Estado de un job de scraping
public enum ScrapingStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    CompletedWithErrors = 3,
    Failed = 4
}
