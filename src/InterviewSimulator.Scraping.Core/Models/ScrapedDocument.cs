using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Models;

/// <summary>
/// Representa un documento/chunk de conocimiento técnico extraído para el corpus RAG.
/// Cada documento es un fragmento autocontenido de 500-1500 palabras sobre un solo concepto.
/// </summary>
public class ScrapedDocument
{
    public int Id { get; set; }

    /// FK a la fuente de donde se extrajo
    public int SourceId { get; set; }

    /// Título del artículo/sección
    public string Title { get; set; } = string.Empty;

    /// Texto completo del contenido técnico (incluye código en bloques ```)
    public string Content { get; set; } = string.Empty;

    /// Texto normalizado (lowercase, sin signos) para deduplicación
    public string ContentNormalized { get; set; } = string.Empty;

    /// Categoría principal del contenido
    public ContentCategory Category { get; set; } = ContentCategory.Unknown;

    /// Subcategoría específica (e.g., "arrays", "sorting", "rest-api")
    public string? Subcategory { get; set; }

    /// Tags como JSON array: ["react","hooks","useState","frontend"]
    public string? Tags { get; set; }

    /// URL exacta de donde se extrajo
    public string SourceUrl { get; set; } = string.Empty;

    /// Nombre del sitio fuente (Microsoft Learn, MDN, GeeksForGeeks, etc.)
    public string SourceSite { get; set; } = string.Empty;

    /// Idioma del contenido: "es" o "en"
    public string Language { get; set; } = "en";

    /// Tipo de contenido
    public ContentType ContentType { get; set; } = ContentType.Unknown;

    /// Nivel de dificultad estimado
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Unknown;

    /// Tecnología principal detectada (Java, Python, C#, SQL, React, etc.)
    public string? Technology { get; set; }

    /// Número de palabras del contenido
    public int WordCount { get; set; }

    /// Índice del chunk si el documento original fue dividido (0 = primer chunk o documento único)
    public int ChunkIndex { get; set; }

    /// ID del documento padre si este es un chunk (null si es documento completo)
    public int? ParentDocumentId { get; set; }

    /// SHA-256 del texto normalizado para deduplicación rápida
    public string HashFingerprint { get; set; } = string.Empty;

    /// Indica si el documento está activo
    public bool IsActive { get; set; } = true;

    /// Indica si fue marcado como duplicado
    public bool IsDuplicate { get; set; }

    /// Referencia al documento original si es duplicado
    public int? DuplicateOfId { get; set; }

    /// Score de confianza de la clasificación (0.0 - 1.0)
    public double ClassificationConfidence { get; set; }

    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ScrapedSource? Source { get; set; }
    public ScrapedDocument? ParentDocument { get; set; }
    public ScrapedDocument? DuplicateOf { get; set; }
    public ICollection<ScrapedDocument> Chunks { get; set; } = new List<ScrapedDocument>();
}
