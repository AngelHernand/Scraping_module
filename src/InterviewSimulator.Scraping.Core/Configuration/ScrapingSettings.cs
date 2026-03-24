namespace InterviewSimulator.Scraping.Core.Configuration;


// Configuración global para el modulo de scraping.

public class ScrapingSettings
{
    public const string SectionName = "ScrapingSettings";

    public List<string> EnabledScrapers { get; set; } = new();
    public int DefaultFrequencyHours { get; set; } = 24;
    public int MaxPagesPerSource { get; set; } = 10;
    public int MinDelayBetweenRequestsMs { get; set; } = 2000;
    public int MaxDelayBetweenRequestsMs { get; set; } = 5000;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public List<string> UserAgents { get; set; } = new();
    public List<string> AllowedLanguages { get; set; } = new() { "es", "en" };
    public string CronSchedule { get; set; } = "0 3 * * *";
    public Dictionary<string, ScraperSourceSettings> Scrapers { get; set; } = new();
}

/// Configuración específica para cada fuente de scraping.
public class ScraperSourceSettings
{
    public bool Enabled { get; set; } = true;
    public int FrequencyHours { get; set; } = 24;
    public int MaxPages { get; set; } = 5;
    public int MaxProblems { get; set; } = 150;
}
