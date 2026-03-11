namespace InterviewSimulator.Scraping.Core.Models.Enums;

/// <summary>
/// Tipo de contenido extraído para el corpus RAG.
/// </summary>
public enum ContentType
{
    /// Documentación oficial de lenguajes/frameworks
    Documentation = 0,

    /// Tutorial paso a paso
    Tutorial = 1,

    /// Artículo de blog o plataforma educativa
    Article = 2,

    /// Referencia técnica (API docs, specs)
    Reference = 3,

    /// Guía de arquitectura o buenas prácticas
    Guide = 4,

    /// Cheatsheet o resumen rápido
    Cheatsheet = 5,

    /// Patrón de diseño con explicación
    Pattern = 6,

    /// Comparación entre tecnologías (X vs Y)
    Comparison = 7,

    /// Preguntas y respuestas de entrevista (legacy)
    InterviewQA = 8,

    /// Repositorio de GitHub (markdown)
    GitHubRepo = 9,

    /// No clasificado
    Unknown = 99
}
