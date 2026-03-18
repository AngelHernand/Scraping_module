using System.Text;
using InterviewSimulator.RAG.Core.Constants;
using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Retrieval;

public static class ContextAssembler
{
    private const int MaxContextTokens = 5000;

    /// <summary>
    /// Ensambla los chunks recuperados en un contexto formateado para el prompt del LLM.
    /// Respeta el límite de tokens máximo.
    /// </summary>
    public static string Assemble(List<RetrievedChunk> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CONTEXTO DE REFERENCIA ===");
        sb.AppendLine();

        // Estrategia B: si hay chunks en más de un idioma, inyectar instrucción multilingüe
        var languages = chunks
            .Select(c => c.Language?.ToLowerInvariant())
            .Where(l => !string.IsNullOrEmpty(l))
            .Distinct()
            .ToList();

        if (languages.Count > 1 || languages.Any(l => l == "en"))
        {
            sb.AppendLine("NOTA: El contexto proporcionado puede contener información en inglés y español.");
            sb.AppendLine("Independientemente del idioma del contexto, SIEMPRE genera tus preguntas");
            sb.AppendLine("y retroalimentación en español. Interpreta el contenido en inglés y");
            sb.AppendLine("adáptalo naturalmente al español, no hagas traducción literal.");
            sb.AppendLine();
        }

        int currentTokens = EstimateTokens(sb.ToString());
        int chunkNumber = 1;

        foreach (var chunk in chunks)
        {
            var chunkBlock = FormatChunk(chunk, chunkNumber);
            int chunkTokens = EstimateTokens(chunkBlock);

            if (currentTokens + chunkTokens > MaxContextTokens)
                break;

            sb.Append(chunkBlock);
            currentTokens += chunkTokens;
            chunkNumber++;
        }

        sb.AppendLine("=== FIN DEL CONTEXTO ===");
        return sb.ToString();
    }

    private static string FormatChunk(RetrievedChunk chunk, int number)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Fragmento #{number} (Score: {chunk.SimilarityScore:F3}) ---");

        if (!string.IsNullOrWhiteSpace(chunk.Category))
            sb.AppendLine($"Categoría: {chunk.Category}");

        if (!string.IsNullOrWhiteSpace(chunk.DifficultyLevel) && chunk.DifficultyLevel != "Unknown")
            sb.AppendLine($"Dificultad: {chunk.DifficultyLevel}");

        if (!string.IsNullOrWhiteSpace(chunk.SourceName))
            sb.AppendLine($"Fuente: {chunk.SourceName}");

        if (!string.IsNullOrWhiteSpace(chunk.Language))
            sb.AppendLine($"Idioma: {chunk.Language}");

        sb.AppendLine();
        sb.AppendLine(chunk.Text);
        sb.AppendLine();

        return sb.ToString();
    }

    private static int EstimateTokens(string text)
    {
        // Aproximación: ~4 caracteres por token en inglés
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
