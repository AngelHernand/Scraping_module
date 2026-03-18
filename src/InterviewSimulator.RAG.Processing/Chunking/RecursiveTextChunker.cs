using System.Text.RegularExpressions;
using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.RAG.Processing.Chunking;

public class RecursiveTextChunker
{
    private readonly ILogger<RecursiveTextChunker> _logger;
    private readonly RagPipelineSettings _settings;

    // Separadores ordenados de mayor a menor granularidad
    private static readonly string[] Separators = new[]
    {
        "\n## ",   // Headers nivel 2
        "\n### ",  // Headers nivel 3
        "\n#### ", // Headers nivel 4
        "\n\n",    // Párrafos dobles
        "\n",      // Líneas
        ". ",      // Oraciones
        " "        // Palabras (último recurso)
    };

    public RecursiveTextChunker(ILogger<RecursiveTextChunker> logger, IOptions<RagPipelineSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public List<TextChunk> Chunk(CleanedDocument document)
    {
        var rawChunks = RecursiveSplit(document.CleanedText, 0, document.DetectedLanguage);
        var chunks = new List<TextChunk>();

        for (int i = 0; i < rawChunks.Count; i++)
        {
            string text = rawChunks[i];
            int tokenCount = TokenCounter.EstimateTokens(text, document.DetectedLanguage);

            if (tokenCount < _settings.MinChunkTokens)
                continue;

            // Añadir overlap del chunk anterior
            if (i > 0 && _settings.OverlapTokens > 0)
            {
                string overlap = GetOverlapText(rawChunks[i - 1], _settings.OverlapTokens, document.DetectedLanguage);
                if (!string.IsNullOrEmpty(overlap))
                {
                    text = overlap + "\n" + text;
                    tokenCount = TokenCounter.EstimateTokens(text, document.DetectedLanguage);
                }
            }

            // Determinar tipo de chunk
            var chunkType = DetectChunkType(text);

            chunks.Add(new TextChunk
            {
                ScrapedQuestionId = document.ScrapedQuestionId,
                ChunkIndex = chunks.Count,
                Text = text,
                QuestionText = document.CleanedQuestionText,
                Type = chunkType,
                TokenCount = tokenCount,
                CharCount = text.Length,
                Language = document.DetectedLanguage
            });
        }

        return chunks;
    }

    private List<string> RecursiveSplit(string text, int separatorIndex, string language)
    {
        if (separatorIndex >= Separators.Length)
            return new List<string> { text };

        int tokenCount = TokenCounter.EstimateTokens(text, language);
        if (tokenCount <= _settings.TargetChunkTokens)
            return new List<string> { text };

        string separator = Separators[separatorIndex];
        var parts = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1)
            return RecursiveSplit(text, separatorIndex + 1, language);

        var result = new List<string>();
        var currentChunk = "";

        foreach (var part in parts)
        {
            string candidate = string.IsNullOrEmpty(currentChunk)
                ? part
                : currentChunk + separator + part;

            int candidateTokens = TokenCounter.EstimateTokens(candidate, language);

            if (candidateTokens > _settings.TargetChunkTokens && !string.IsNullOrEmpty(currentChunk))
            {
                // Flush current chunk
                int currentTokens = TokenCounter.EstimateTokens(currentChunk, language);
                if (currentTokens > _settings.TargetChunkTokens)
                {
                    // Recursivamente dividir el chunk acumulado
                    result.AddRange(RecursiveSplit(currentChunk, separatorIndex + 1, language));
                }
                else
                {
                    result.Add(currentChunk);
                }
                currentChunk = part;
            }
            else
            {
                currentChunk = candidate;
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
        {
            int currentTokens = TokenCounter.EstimateTokens(currentChunk, language);
            if (currentTokens > _settings.TargetChunkTokens)
                result.AddRange(RecursiveSplit(currentChunk, separatorIndex + 1, language));
            else
                result.Add(currentChunk);
        }

        return result;
    }

    private static string GetOverlapText(string previousChunk, int overlapTokens, string language)
    {
        var words = previousChunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return string.Empty;

        // Tomar palabras del final hasta alcanzar los tokens de overlap
        var overlapWords = new List<string>();
        int tokens = 0;
        for (int i = words.Length - 1; i >= 0 && tokens < overlapTokens; i--)
        {
            overlapWords.Insert(0, words[i]);
            tokens = TokenCounter.EstimateTokens(string.Join(' ', overlapWords), language);
        }

        return string.Join(' ', overlapWords);
    }

    private static ChunkType DetectChunkType(string text)
    {
        if (text.Contains("```"))
            return ChunkType.CodeExample;

        if (Regex.IsMatch(text, @"^Q:|^Question\s*\d*[:\.]|^\d+\.\s+.*\?", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            return ChunkType.QuestionAnswer;

        return ChunkType.Explanation;
    }
}
