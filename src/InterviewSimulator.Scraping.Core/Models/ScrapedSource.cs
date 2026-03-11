using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Models;

// Representa una fuente web de donde se extraen preguntas de entrevista
public class ScrapedSource
{
    public int Id { get; set; }

    //Nombre identificador de la fuente (DevTo, Medium, etc)
    public string Name { get; set; } = string.Empty;

    // URL base de la fuente
    public string BaseUrl { get; set; } = string.Empty;

    //Tipo de fuente
    public SourceType SourceType { get; set; }

    //Indica si la fuente está activa para scraping
    public bool IsActive { get; set; } = true;

    //Frecuencia de scraping en horas
    public int ScrapingFrequencyHours { get; set; } = 24;

    //Última vez que se ejecutó scraping para esta fuente
    public DateTime? LastScrapedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Notas adicionales sobre la fuente
    public string? Notes { get; set; }

    // Navigation properties
    public ICollection<ScrapedQuestion> Questions { get; set; } = new List<ScrapedQuestion>();
    public ICollection<ScrapedDocument> Documents { get; set; } = new List<ScrapedDocument>();
    public ICollection<ScrapingJob> Jobs { get; set; } = new List<ScrapingJob>();
}
