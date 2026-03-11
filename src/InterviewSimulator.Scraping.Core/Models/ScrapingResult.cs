namespace InterviewSimulator.Scraping.Core.Models;

/// <summary>
/// Resultado de la ejecución de un scraper individual.
/// Soporta tanto preguntas Q&A como documentos RAG.
/// </summary>
public class ScrapingResult
{
    public bool Success { get; set; }

    // ── Preguntas Q&A (legacy) ──
    public int TotalQuestionsFound { get; set; }
    public int NewQuestions { get; set; }
    public int DuplicateQuestions { get; set; }
    public List<ScrapedQuestion> Questions { get; set; } = new();

    // ── Documentos RAG (corpus) ──
    public int TotalDocumentsFound { get; set; }
    public int NewDocuments { get; set; }
    public int DuplicateDocuments { get; set; }
    public List<ScrapedDocument> Documents { get; set; } = new();

    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}
