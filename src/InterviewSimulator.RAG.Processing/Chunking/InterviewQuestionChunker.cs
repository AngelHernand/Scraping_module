using System.Text.RegularExpressions;
using InterviewSimulator.RAG.Core.Configuration;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewSimulator.RAG.Processing.Chunking;

public class InterviewQuestionChunker
{
    private readonly ILogger<InterviewQuestionChunker> _logger;
    private readonly RagPipelineSettings _settings;

    // Patrones para detectar preguntas en texto
    private static readonly Regex[] QuestionPatterns = new[]
    {
        new Regex(@"^(?:Q\d*[\.\):]?\s*|Question\s*\d*[\.\):]?\s*)(.+\??)\s*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
        new Regex(@"^(?:\d+[\.\)]\s+)(.+\?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"^#{1,4}\s+(.+\?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"^\*\*(.+\?)\*\*\s*$", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"^(?:What|How|Why|When|Which|Where|Who|Is|Can|Does|Do|Explain|Describe|Define|Compare|Difference|Qué|Cómo|Por qué|Cuándo|Cuál|Dónde|Quién|Explica|Describe|Define|Compara|Diferencia)\b.+\?\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase)
    };

    public InterviewQuestionChunker(ILogger<InterviewQuestionChunker> logger, IOptions<RagPipelineSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public List<TextChunk> Chunk(CleanedDocument document)
    {
        var chunks = new List<TextChunk>();
        var qaBlocks = ExtractQABlocks(document.CleanedText);

        if (qaBlocks.Count == 0)
        {
            // Sin bloques Q&A detectados — crear un solo chunk general
            return CreateSingleChunk(document);
        }

        for (int i = 0; i < qaBlocks.Count; i++)
        {
            var (question, answer) = qaBlocks[i];
            string chunkText = $"Q: {question.Trim()}\nA: {answer.Trim()}";
            int tokenCount = TokenCounter.EstimateTokens(chunkText, document.DetectedLanguage);

            if (tokenCount > _settings.MaxChunkTokens)
            {
                // Si excede el límite, particionar la respuesta
                var subChunks = SplitLargeQABlock(question, answer, document.DetectedLanguage, document.ScrapedQuestionId, i, chunks.Count);
                chunks.AddRange(subChunks);
            }
            else if (tokenCount >= _settings.MinChunkTokens)
            {
                chunks.Add(CreateChunk(document, chunkText, chunks.Count, ChunkType.QuestionAnswer, tokenCount));
            }
            else
            {
                _logger.LogDebug("Chunk with {Tokens} tokens skipped (below minimum) for ScrapedQuestionId {Id}",
                    tokenCount, document.ScrapedQuestionId);
            }
        }

        return chunks;
    }

    public int CountDetectedQuestions(string text)
    {
        var questions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in QuestionPatterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                questions.Add(match.Value.Trim());
            }
        }
        return questions.Count;
    }

    private List<(string Question, string Answer)> ExtractQABlocks(string text)
    {
        var blocks = new List<(string, string)>();
        var allMatches = new List<(int Index, string Question)>();

        foreach (var pattern in QuestionPatterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                allMatches.Add((match.Index, match.Value.Trim()));
            }
        }

        // Ordenar por posición y deduplicar por posición cercana
        allMatches = allMatches
            .OrderBy(m => m.Index)
            .ToList();

        var deduplicated = new List<(int Index, string Question)>();
        foreach (var m in allMatches)
        {
            if (deduplicated.Count == 0 || m.Index - deduplicated[^1].Index > 10)
                deduplicated.Add(m);
        }

        for (int i = 0; i < deduplicated.Count; i++)
        {
            int start = deduplicated[i].Index + deduplicated[i].Question.Length;
            int end = i + 1 < deduplicated.Count ? deduplicated[i + 1].Index : text.Length;
            string answer = text[start..end].Trim();

            blocks.Add((deduplicated[i].Question, answer));
        }

        return blocks;
    }

    private List<TextChunk> SplitLargeQABlock(string question, string answer, string language, int scrapedQuestionId, int blockIndex, int startChunkIndex)
    {
        var chunks = new List<TextChunk>();
        var sentences = Regex.Split(answer, @"(?<=[.!?])\s+");
        var currentText = $"Q: {question.Trim()}\nA: ";
        int currentTokens = TokenCounter.EstimateTokens(currentText, language);

        foreach (var sentence in sentences)
        {
            int sentenceTokens = TokenCounter.EstimateTokens(sentence, language);

            if (currentTokens + sentenceTokens > _settings.TargetChunkTokens && currentTokens > TokenCounter.EstimateTokens($"Q: {question}\nA: ", language))
            {
                chunks.Add(new TextChunk
                {
                    ScrapedQuestionId = scrapedQuestionId,
                    ChunkIndex = startChunkIndex + chunks.Count,
                    Text = currentText.Trim(),
                    Type = ChunkType.QuestionAnswer,
                    TokenCount = currentTokens,
                    CharCount = currentText.Length,
                    Language = language
                });
                currentText = $"Q: {question.Trim()} (cont.)\nA: ";
                currentTokens = TokenCounter.EstimateTokens(currentText, language);
            }

            currentText += sentence + " ";
            currentTokens += sentenceTokens;
        }

        if (currentTokens > _settings.MinChunkTokens)
        {
            chunks.Add(new TextChunk
            {
                ScrapedQuestionId = scrapedQuestionId,
                ChunkIndex = startChunkIndex + chunks.Count,
                Text = currentText.Trim(),
                Type = ChunkType.QuestionAnswer,
                TokenCount = currentTokens,
                CharCount = currentText.Length,
                Language = language
            });
        }

        return chunks;
    }

    private List<TextChunk> CreateSingleChunk(CleanedDocument document)
    {
        var chunks = new List<TextChunk>();
        if (document.EstimatedTokenCount >= _settings.MinChunkTokens)
        {
            chunks.Add(CreateChunk(document, document.CleanedText, 0, ChunkType.GeneralContent, document.EstimatedTokenCount));
        }
        return chunks;
    }

    private static TextChunk CreateChunk(CleanedDocument document, string text, int index, ChunkType type, int tokenCount)
    {
        return new TextChunk
        {
            ScrapedQuestionId = document.ScrapedQuestionId,
            ChunkIndex = index,
            Text = text,
            QuestionText = document.CleanedQuestionText,
            Type = type,
            TokenCount = tokenCount,
            CharCount = text.Length,
            Language = document.DetectedLanguage
        };
    }
}
