using InterviewSimulator.Scraping.Core.Models;

namespace InterviewSimulator.Scraping.Core.Interfaces;

// Contrato para el orquestador que coordina la ejecución de todos los scrapers
public interface IScrapingOrchestrator
{
    // Ejecuta todos los scrapers activos que necesitan scraping según su frecuencia
    Task<OrchestratorResult> RunAllScrapersAsync(CancellationToken cancellationToken = default);

    // Ejecuta un scraper específico por nombre.
    Task<ScrapingResult> RunScraperAsync(string sourceName, CancellationToken cancellationToken = default);
}
