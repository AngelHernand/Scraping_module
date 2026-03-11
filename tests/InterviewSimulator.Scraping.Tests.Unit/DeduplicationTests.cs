using System.Security.Cryptography;
using System.Text;

namespace InterviewSimulator.Scraping.Tests.Unit;

/// <summary>
/// Tests unitarios para la lógica de deduplicación: normalización y hashing SHA-256.
/// Replica la lógica de BaseScraper para verificar comportamiento determinístico.
/// </summary>
public class DeduplicationTests
{
    // ────────────── Hash Consistency ──────────────

    [Fact]
    public void ComputeHash_SameInput_ProducesSameHash()
    {
        var text = "what is polymorphism";
        var hash1 = ComputeHash(NormalizeQuestionText(text));
        var hash2 = ComputeHash(NormalizeQuestionText(text));

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInput_ProducesDifferentHash()
    {
        var hash1 = ComputeHash(NormalizeQuestionText("What is polymorphism?"));
        var hash2 = ComputeHash(NormalizeQuestionText("What is inheritance?"));

        Assert.NotEqual(hash1, hash2);
    }

    // ────────────── Normalization ──────────────

    [Fact]
    public void NormalizeQuestionText_CaseInsensitive()
    {
        var upper = NormalizeQuestionText("What Is Polymorphism?");
        var lower = NormalizeQuestionText("what is polymorphism?");

        Assert.Equal(upper, lower);
    }

    [Fact]
    public void NormalizeQuestionText_RemovesArticles()
    {
        var withArticles = NormalizeQuestionText("Explain the difference between an abstract class and an interface");
        var withoutArticles = NormalizeQuestionText("Explain difference between abstract class and interface");

        // Ambos deberían producir el mismo texto normalizado
        Assert.Equal(withArticles, withoutArticles);
    }

    [Fact]
    public void NormalizeQuestionText_RemovesPunctuation()
    {
        var withPunctuation = NormalizeQuestionText("What is, object-oriented programming?!!");
        
        // No debería contener comas ni exclamaciones
        Assert.DoesNotContain(",", withPunctuation);
        Assert.DoesNotContain("!", withPunctuation);
    }

    [Fact]
    public void NormalizeQuestionText_NormalizesWhitespace()
    {
        var withExtraSpaces = NormalizeQuestionText("  What   is    polymorphism  ?  ");
        
        // No debería tener espacios múltiples ni leading/trailing
        Assert.DoesNotContain("  ", withExtraSpaces);
        Assert.Equal(withExtraSpaces, withExtraSpaces.Trim());
    }

    // ────────────── Deduplication End-to-End ──────────────

    [Fact]
    public void DuplicateDetection_IdenticalQuestionsWithDifferentFormatting_SameHash()
    {
        // Simulación: la misma pregunta con variaciones de formato
        var q1 = "What is the difference between an abstract class and an interface?";
        var q2 = "what is THE difference between AN abstract class and AN interface?";
        var q3 = "  What   is  the  difference   between  an  abstract  class  and  an  interface?  ";

        var hash1 = ComputeHash(NormalizeQuestionText(q1));
        var hash2 = ComputeHash(NormalizeQuestionText(q2));
        var hash3 = ComputeHash(NormalizeQuestionText(q3));

        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void DuplicateDetection_SimilarButDifferentQuestions_DifferentHash()
    {
        var q1 = "What is polymorphism in Java?";
        var q2 = "What is encapsulation in Java?";

        var hash1 = ComputeHash(NormalizeQuestionText(q1));
        var hash2 = ComputeHash(NormalizeQuestionText(q2));

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsSha256HexString()
    {
        var hash = ComputeHash("test input");

        // SHA-256 produce 64 caracteres hex
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void NormalizeQuestionText_EmptyString_ReturnsEmpty()
    {
        var result = NormalizeQuestionText("");
        Assert.Equal("", result);
    }

    // ────────────── Helper methods (replican la lógica de BaseScraper) ──────────────

    /// <summary>
    /// Réplica de BaseScraper.NormalizeQuestionText para testear en aislamiento.
    /// </summary>
    private static string NormalizeQuestionText(string text)
    {
        var normalized = text.ToLowerInvariant().Trim();

        var articles = new[] { " the ", " a ", " an ", " el ", " la ", " un ", " una ", " los ", " las ", " unos ", " unas " };
        foreach (var article in articles)
        {
            normalized = normalized.Replace(article, " ");
        }

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s]", "");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    /// <summary>
    /// Réplica de BaseScraper.ComputeHash para testear en aislamiento.
    /// </summary>
    private static string ComputeHash(string normalizedText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
