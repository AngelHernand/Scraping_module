namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Query de búsqueda para el servicio de retrieval.
/// </summary>
public class RetrievalQuery
{
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public string? DifficultyLevel { get; set; }
    public List<string>? Tags { get; set; }
    public string? PreferredLanguage { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public int TopK { get; set; } = 10;
    public float MinScore { get; set; } = 0.5f;
    public List<int>? ExcludeScrapedQuestionIds { get; set; }
}
