using InterviewSimulator.Scraping.Core.Interfaces;

namespace InterviewSimulator.Scraping.Worker;

// Background Service que ejecuta el scraping una sola vez y termina el proceso.
public class ScrapingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScrapingWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ScrapingWorker(
        IServiceProvider serviceProvider,
        ILogger<ScrapingWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScrapingWorker iniciado — ejecución única");

        await RunScrapingAsync(stoppingToken);

        _logger.LogInformation("ScrapingWorker finalizado — deteniendo el host");
        _lifetime.StopApplication();
    }

    private async Task RunScrapingAsync(CancellationToken ct)
    {
        _logger.LogInformation("");
        _logger.LogInformation("   INICIANDO SESIÓN DE SCRAPING");
        _logger.LogInformation("   {Time}", DateTime.UtcNow);
        _logger.LogInformation("");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IScrapingOrchestrator>();

            var result = await orchestrator.RunAllScrapersAsync(ct);

            _logger.LogInformation("");
            _logger.LogInformation("   RESUMEN DE SCRAPING");
            _logger.LogInformation("   Fuentes procesadas:     {Sources}", result.TotalSourcesProcessed);
            _logger.LogInformation("   ── Preguntas Q&A ──");
            _logger.LogInformation("   Encontradas:            {Found}", result.TotalQuestionsFound);
            _logger.LogInformation("   Nuevas:                 {New}", result.TotalNewQuestions);
            _logger.LogInformation("   Duplicadas:             {Dups}", result.TotalDuplicates);
            _logger.LogInformation("   ── Documentos RAG ──");
            _logger.LogInformation("   Encontrados:            {Found}", result.TotalDocumentsFound);
            _logger.LogInformation("   Nuevos:                 {New}", result.TotalNewDocuments);
            _logger.LogInformation("   Duplicados:             {Dups}", result.TotalDuplicateDocuments);
            _logger.LogInformation("   ────────────────────");
            _logger.LogInformation("   Errores:                {Errors}", result.TotalErrors);
            _logger.LogInformation("   Duración total:         {Duration}", result.TotalDuration);
            _logger.LogInformation("");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico durante la sesión de scraping");
        }
    }
}
