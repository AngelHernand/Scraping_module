namespace InterviewSimulator.Scraping.Core.Models;

/// <summary>
/// Resultado de la ejecución del orquestador (todos los scrapers).
/// Incluye métricas tanto de preguntas Q&A como de documentos RAG.
/// </summary>
public class OrchestratorResult
{
    public int TotalSourcesProcessed { get; set; }

    // ── Preguntas Q&A ──
    public int TotalQuestionsFound { get; set; }
    public int TotalNewQuestions { get; set; }
    public int TotalDuplicates { get; set; }

    // ── Documentos RAG ──
    public int TotalDocumentsFound { get; set; }
    public int TotalNewDocuments { get; set; }
    public int TotalDuplicateDocuments { get; set; }

    public int TotalErrors { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<ScrapingResult> ScraperResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
