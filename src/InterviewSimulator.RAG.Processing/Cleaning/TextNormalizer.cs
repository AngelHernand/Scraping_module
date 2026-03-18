using System.Text.RegularExpressions;

namespace InterviewSimulator.RAG.Processing.Cleaning;

/// <summary>
/// Normaliza y limpia texto plano post-procesamiento HTML.
/// </summary>
public static class TextNormalizer
{
    private static readonly Regex MultipleSpaces = new(@" {2,}", RegexOptions.Compiled);
    private static readonly Regex MultipleNewlines = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex UrlPattern = new(@"https?://\S+", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"\S+@\S+\.\S+", RegexOptions.Compiled);
    private static readonly Regex SeparatorLines = new(@"^[-=_*]{3,}$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly string[] CtaPatterns = new[]
    {
        "read more", "continue reading", "follow me on", "like and share",
        "subscribe", "leer más", "seguir", "suscríbete", "compartir",
        "originally published at", "cover image", "top comments"
    };

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Reemplazar entidades HTML residuales
        text = text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&nbsp;", " ")
            .Replace("&quot;", "\"");

        // Eliminar URLs sueltas
        text = UrlPattern.Replace(text, "");

        // Eliminar emails
        text = EmailPattern.Replace(text, "");

        // Eliminar líneas separadoras
        text = SeparatorLines.Replace(text, "");

        // Eliminar líneas CTA
        var lines = text.Split('\n')
            .Where(line =>
            {
                var trimmed = line.Trim().ToLowerInvariant();
                return !CtaPatterns.Any(cta => trimmed.StartsWith(cta));
            })
            .Select(line => line.TrimEnd());

        text = string.Join('\n', lines);

        // Normalizar espacios y newlines
        text = MultipleSpaces.Replace(text, " ");
        text = MultipleNewlines.Replace(text, "\n\n");

        return text.Trim();
    }
}
