namespace InterviewSimulator.RAG.Processing.Chunking;

/// <summary>
/// Estimador de tokens compatible con modelos OpenAI.
/// Inglés: 1 token ≈ 4 caracteres, o ≈ 0.75 palabras
/// Español: 1 token ≈ 3.5 caracteres, o ≈ 0.65 palabras
/// </summary>
public static class TokenCounter
{
    public static int EstimateTokens(string text, string language = "en")
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        double charsPerToken = language == "es" ? 3.5 : 4.0;
        return (int)Math.Ceiling(text.Length / charsPerToken);
    }

    public static int EstimateTokensByWords(string text, string language = "en")
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        int wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;

        double wordsPerToken = language == "es" ? 0.65 : 0.75;
        return (int)Math.Ceiling(wordCount / wordsPerToken);
    }
}
