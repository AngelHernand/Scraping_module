using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Recupera chunks relevantes de Qdrant para alimentar al LLM.
/// </summary>
public interface IRetrievalService
{
    /// <summary>
    /// Recupera los chunks más relevantes para un perfil de entrevista.
    /// </summary>
    Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera chunks y los ensambla en un bloque de contexto listo para el prompt del LLM.
    /// </summary>
    Task<string> RetrieveAndAssembleContextAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);
}
