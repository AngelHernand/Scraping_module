using Cronos;
using InterviewSimulator.Scraping.Core.Interfaces;

namespace InterviewSimulator.Scraping.Worker;

// Background Service que ejecuta el scraping según un cron schedule configurado.
// Usa Cronos para parsear la expresión cron.
public class ScrapingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScrapingWorker> _logger;
    private readonly IConfiguration _configuration;

    public ScrapingWorker(
        IServiceProvider serviceProvider,
        ILogger<ScrapingWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScrapingWorker iniciado");

        var cronExpression = _configuration.GetValue<string>("ScrapingSettings:CronSchedule") ?? "0 3 * * *";
        var cron = CronExpression.Parse(cronExpression);

        _logger.LogInformation("Cron schedule: {Cron}", cronExpression);

        // Ejecutar inmediatamente la primera vez
        await RunScrapingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextOccurrence = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (nextOccurrence == null)
            {
                _logger.LogWarning("No se pudo determinar la próxima ejecución. Esperando 1 hora.");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                continue;
            }

            var delay = nextOccurrence.Value - now;
            _logger.LogInformation("Próxima ejecución de scraping: {Next} (en {Delay})",
                nextOccurrence.Value, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ScrapingWorker detenido durante espera");
                break;
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await RunScrapingAsync(stoppingToken);
            }
        }

        _logger.LogInformation("ScrapingWorker finalizado");
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
