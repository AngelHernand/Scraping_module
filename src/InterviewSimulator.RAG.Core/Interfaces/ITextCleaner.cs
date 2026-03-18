using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Core.Interfaces;

/// <summary>
/// Limpia el contenido crudo del scraping y devuelve texto procesado.
/// </summary>
public interface ITextCleaner
{
    Task<CleanedDocument> CleanAsync(string rawContent, string questionText, string sourceName);
}
