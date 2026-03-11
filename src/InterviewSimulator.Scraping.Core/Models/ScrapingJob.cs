using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Models;

// Representa un job de scraping ejecutado para una fuente específica.
public class ScrapingJob
{
    public int Id { get; set; }

    //FK a la fuente que se scrapeo
    public int SourceId { get; set; }

    // Momento en que inició el job
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // Momento en que finalizó el job
    public DateTime? FinishedAt { get; set; }

    //Estado del job
    public ScrapingStatus Status { get; set; } = ScrapingStatus.Pending;

    // Cantidad total de preguntas encontradas
    public int QuestionsFound { get; set; }

    // Cantidad de preguntas nuevas insertadas
    public int QuestionsNew { get; set; }

    // Cantidad total de documentos RAG encontrados
    public int DocumentsFound { get; set; }

    // Cantidad de documentos RAG nuevos insertados
    public int DocumentsNew { get; set; }

    // Mensaje de error si el job falló
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ScrapedSource? Source { get; set; }
}
