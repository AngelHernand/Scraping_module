using InterviewSimulator.Scraping.Core.Models;
using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Core.Interfaces;

// Contrato para cada scraper de una fuente específica.
public interface IScraper
{
    //Nombre identificador de la fuente
    string SourceName { get; }

    //Tipo de fuente
    SourceType SourceType { get; }

    // Ejecuta el scraping de la fuente y retorna las preguntas encontradas
    Task<ScrapingResult> ScrapeAsync(CancellationToken cancellationToken = default);
}
