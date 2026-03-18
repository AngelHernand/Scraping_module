namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Resultado de la limpieza de un documento scrapeado, listo para chunking.
/// </summary>
public class CleanedDocument
{
    public int ScrapedQuestionId { get; set; }
    public string CleanedText { get; set; } = string.Empty;
    public string CleanedQuestionText { get; set; } = string.Empty;
    public string DetectedLanguage { get; set; } = "unknown";
    public int EstimatedTokenCount { get; set; }
    public int CharCount { get; set; }
    public bool HasSufficientContent { get; set; }
    public List<string> CleaningWarnings { get; set; } = new();
}
