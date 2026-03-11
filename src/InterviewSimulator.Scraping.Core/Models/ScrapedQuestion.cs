using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Models;

// Representa una pregunta de entrevista extraída por web scraping.
public class ScrapedQuestion
{
    public int Id { get; set; }

    //FK a la fuente de donde se extrajo
    public int SourceId { get; set; }

    //Texto completo de la pregunta
    public string QuestionText { get; set; } = string.Empty;

    //Texto normalizado (lowercase, sin signos) para deduplicación
    public string QuestionTextNormalized { get; set; } = string.Empty;

    //Categoría de la pregunta
    public QuestionCategory Category { get; set; } = QuestionCategory.Unknown;

    //Subcategoría técnica ( Bases de datos, Algoritmos, etc)
    public string? Subcategory { get; set; }

    //Nivel de dificultad estimado
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Unknown;

    //Tags como JSON array: [SQL, JOIN, Normalization]
    public string? Tags { get; set; }

    //Idioma original: es, en
    public string OriginalLanguage { get; set; } = "en";

    //URL específica de donde se extrajo la pregunta
    public string? SourceUrl { get; set; }

    //Texto de la respuesta asociada a la pregunta
    public string? AnswerText { get; set; }

    //Tecnología principal detectada (Java, Python, C#, SQL, React, etc.)
    public string? Technology { get; set; }

    //Contenido original sin procesar (para futuro RAG)
    public string? RawContent { get; set; }

    //Indica si la pregunta está activa
    public bool IsActive { get; set; } = true;

    //Indica si fue marcada como duplicada
    public bool IsDuplicate { get; set; }

    //Referencia a la pregunta original si es duplicada
    public int? DuplicateOfId { get; set; }

    //SHA-256 del texto normalizado para deduplicación rápida
    public string HashFingerprint { get; set; } = string.Empty;

    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ScrapedSource? Source { get; set; }
    public ScrapedQuestion? DuplicateOf { get; set; }
}
