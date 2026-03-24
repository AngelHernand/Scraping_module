using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Repositorio para la tabla ProcessingStatus (tracking del pipeline RAG).
/// </summary>
public interface IProcessingStatusRepository
{
    Task<ProcessingStatus?> GetByScrapedQuestionIdAsync(int scrapedQuestionId);
    Task<List<ProcessingStatus>> GetPendingOrFailedAsync(int maxRetryCount, int batchSize);
    Task<ProcessingStatus> CreateAsync(ProcessingStatus status);
    Task UpdateAsync(ProcessingStatus status);
    Task UpdateStateAsync(int id, ProcessingState state, string? errorMessage = null);
    Task<int> GetCountByStateAsync(ProcessingState state);
    Task<HashSet<int>> GetExcludedScrapedQuestionIdsAsync(int maxRetryCount);
}
