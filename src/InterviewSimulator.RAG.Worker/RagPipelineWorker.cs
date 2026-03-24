using InterviewSimulator.RAG.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.RAG.Worker;

public class RagPipelineWorker : BackgroundService
{
    private readonly IRagPipelineOrchestrator _orchestrator;
    private readonly ILogger<RagPipelineWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public RagPipelineWorker(
        IRagPipelineOrchestrator orchestrator,
        ILogger<RagPipelineWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RAG Pipeline Worker started — single execution");

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

        _logger.LogInformation("RAG Pipeline Worker finished — stopping host");
        _lifetime.StopApplication();
    }
}
