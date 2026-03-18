namespace InterviewSimulator.RAG.Core.Models;

/// <summary>
/// Resultado agregado de una ejecución del pipeline RAG.
/// </summary>
public class PipelineResult
{
    public int TotalProcessed { get; set; }
    public int Successful { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int TotalChunksGenerated { get; set; }
    public int TotalTokensEmbedded { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
}
