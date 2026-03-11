using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Models;

/// <summary>
/// Resultado de la clasificación de un documento de conocimiento para RAG.
/// </summary>
public class DocumentClassificationResult
{
    public ContentCategory Category { get; set; }
    public string? Subcategory { get; set; }
    public ContentType ContentType { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Technology { get; set; }
    public double ConfidenceScore { get; set; }
}
