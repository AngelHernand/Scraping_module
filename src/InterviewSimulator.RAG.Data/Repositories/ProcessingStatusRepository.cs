using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InterviewSimulator.RAG.Data.Repositories;

public class ProcessingStatusRepository : IProcessingStatusRepository
{
    private readonly ProcessingDbContext _context;

    public ProcessingStatusRepository(ProcessingDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessingStatus?> GetByScrapedQuestionIdAsync(int scrapedQuestionId)
    {
        return await _context.ProcessingStatuses
            .FirstOrDefaultAsync(p => p.ScrapedQuestionId == scrapedQuestionId);
    }

    public async Task<List<ProcessingStatus>> GetPendingOrFailedAsync(int maxRetryCount, int batchSize)
    {
        return await _context.ProcessingStatuses
            .Where(p => p.State == ProcessingState.Pending
                     || (p.State == ProcessingState.Failed && p.RetryCount < maxRetryCount))
            .OrderBy(p => p.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<ProcessingStatus> CreateAsync(ProcessingStatus status)
    {
        _context.ProcessingStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task UpdateAsync(ProcessingStatus status)
    {
        status.UpdatedAt = DateTime.UtcNow;
        _context.ProcessingStatuses.Update(status);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStateAsync(int id, ProcessingState state, string? errorMessage = null)
    {
        var status = await _context.ProcessingStatuses.FindAsync(id);
        if (status is null) return;

        status.State = state;
        status.UpdatedAt = DateTime.UtcNow;
        if (errorMessage is not null)
            status.ErrorMessage = errorMessage;

        if (state == ProcessingState.Failed)
            status.RetryCount++;

        if (state == ProcessingState.Stored)
            status.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCountByStateAsync(ProcessingState state)
    {
        return await _context.ProcessingStatuses.CountAsync(p => p.State == state);
    }

    public async Task<HashSet<int>> GetExcludedScrapedQuestionIdsAsync(int maxRetryCount)
    {
        var ids = await _context.ProcessingStatuses
            .Where(p => p.State == ProcessingState.Stored
                     || p.State == ProcessingState.Skipped
                     || (p.State == ProcessingState.Failed && p.RetryCount >= maxRetryCount))
            .Select(p => p.ScrapedQuestionId)
            .ToListAsync();
        return ids.ToHashSet();
    }
}
