using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Orquesta el pipeline completo: Limpieza → Chunking → Enrichment → Embedding → Almacenamiento.
/// </summary>
public interface IRagPipelineOrchestrator
{
    /// <summary>
    /// Ejecuta el pipeline completo para todas las ScrapedQuestions pendientes.
    /// </summary>
    Task<PipelineResult> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reprocesa una ScrapedQuestion específica (útil para debug).
    /// </summary>
    Task<PipelineResult> ReprocessAsync(int scrapedQuestionId, CancellationToken cancellationToken = default);
}
