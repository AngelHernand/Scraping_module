using System.Text.RegularExpressions;

namespace InterviewSimulator.RAG.Processing.Cleaning;

/// <summary>
/// Detector simple de idioma basado en palabras frecuentes.
/// </summary>
public static class LanguageDetector
{
    private static readonly HashSet<string> SpanishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "que", "qué", "como", "cómo", "para", "por", "una", "los", "las",
        "del", "con", "esta", "pero", "más", "también", "puede", "entre",
        "cuando", "sobre", "todo", "desde", "donde", "cual", "cuál",
        "ejemplo", "datos", "sistema", "proceso", "función", "desarrollo",
        "mediante", "permite", "cada", "debe", "tiene", "ser", "está"
    };

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "that", "with", "this", "from", "your",
        "which", "when", "what", "how", "about", "would", "should",
        "between", "example", "function", "return", "data",
        "their", "have", "been", "each", "into", "then", "than"
    };

    /// <summary>
    /// Detecta el idioma de un texto. Retorna "es", "en" o "mixed".
    /// </summary>
    public static string Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";

        var tokens = Regex.Split(text.ToLowerInvariant(), @"[\s\p{P}]+")
            .Where(t => t.Length > 1)
            .ToList();

        int spanishCount = tokens.Count(t => SpanishWords.Contains(t));
        int englishCount = tokens.Count(t => EnglishWords.Contains(t));

        if (spanishCount > englishCount * 1.2)
            return "es";
        if (englishCount > spanishCount * 1.2)
            return "en";

        return "mixed";
    }
}
