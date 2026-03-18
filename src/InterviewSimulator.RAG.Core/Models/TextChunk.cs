using InterviewSimulator.RAG.Core.Models.Enums;

namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Representa un fragmento de texto listo para generar embedding.
/// </summary>
public class TextChunk
{
    public Guid ChunkId { get; set; } = Guid.NewGuid();
    public int ScrapedQuestionId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? QuestionText { get; set; }
    public ChunkType Type { get; set; }
    public int TokenCount { get; set; }
    public int CharCount { get; set; }
    public string Language { get; set; } = "en";

    // Metadata heredada del ScrapedQuestion
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string DifficultyLevel { get; set; } = "Unknown";
    public List<string> Tags { get; set; } = new();
    public string SourceUrl { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; }
}
