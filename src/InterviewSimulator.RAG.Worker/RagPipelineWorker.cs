using Cronos;
using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.RAG.Worker;

public class RagPipelineWorker : BackgroundService
{
    private readonly IRagPipelineOrchestrator _orchestrator;
    private readonly RagPipelineSettings _settings;
    private readonly ILogger<RagPipelineWorker> _logger;

    public RagPipelineWorker(
        IRagPipelineOrchestrator orchestrator,
        IOptions<RagPipelineSettings> settings,
        ILogger<RagPipelineWorker> logger)
    {
        _orchestrator = orchestrator;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RAG Pipeline Worker started with schedule: {Schedule}", _settings.CronSchedule);

        var cronExpression = CronExpression.Parse(_settings.CronSchedule);

        while (!stoppingToken.IsCancellationRequested)
        {
            var utcNow = DateTime.UtcNow;
            var nextOccurrence = cronExpression.GetNextOccurrence(utcNow, TimeZoneInfo.Utc);

            if (nextOccurrence == null)
            {
                _logger.LogWarning("No next occurrence found for cron schedule: {Schedule}", _settings.CronSchedule);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var delay = nextOccurrence.Value - utcNow;
            _logger.LogInformation("Next pipeline execution at {NextRun} (in {Delay})", nextOccurrence, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                _logger.LogInformation("Starting RAG pipeline execution");
                var result = await _orchestrator.ProcessPendingAsync(stoppingToken);

                _logger.LogInformation(
                    "Pipeline execution completed: {Processed} processed, {Successful} ok, {Failed} failed, {Skipped} skipped in {Duration}",
                    result.TotalProcessed, result.Successful, result.Failed, result.Skipped, result.Duration);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Pipeline execution failed");
            }
        }

        _logger.LogInformation("RAG Pipeline Worker stopped");
    }
}
