using InterviewSimulator.Scraping.Core.Models;

namespace InterviewSimulator.Scraping.Core.Interfaces;

/// <summary>
/// Contrato para clasificar documentos de conocimiento técnico para el corpus RAG.
/// </summary>
public interface IContentClassifier
{
    /// <summary>
    /// Clasifica un documento basándose en su título y contenido.
    /// Determina categoría, subcategoría, tipo de contenido, dificultad, tags y tecnología.
    /// </summary>
    DocumentClassificationResult ClassifyDocument(string title, string content);

    /// <summary>
    /// Determina si un contenido es relevante para IT/desarrollo de software.
    /// Filtra contenido no técnico (marketing, noticias sin valor educativo, etc.)
    /// </summary>
    bool IsITRelevantContent(string title, string content);
}
