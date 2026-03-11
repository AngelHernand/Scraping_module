using InterviewSimulator.Scraping.Core.Models;

namespace InterviewSimulator.Scraping.Core.Interfaces;

/// Contrato para clasificar preguntas de entrevista por categoría y dificultad.
public interface IQuestionClassifier
{
    /// Clasifica una pregunta de entrevista basándose en su texto.
    ClassificationResult Classify(string questionText);

    /// Determina si una pregunta (con su respuesta) es relevante para entrevistas de IT/desarrollo.
    bool IsITRelevant(string questionText, string? answerText);
}
