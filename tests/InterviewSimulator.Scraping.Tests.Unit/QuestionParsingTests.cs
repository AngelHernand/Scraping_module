using System.Text.RegularExpressions;

namespace InterviewSimulator.Scraping.Tests.Unit;

/// <summary>
/// Tests unitarios para la lógica de parsing/extracción de preguntas.
/// Replica la lógica de BaseScraper.ExtractQuestionsFromText().
/// </summary>
public class QuestionParsingTests
{
    // ────────────── Numbered Lists ──────────────

    [Fact]
    public void ExtractQuestions_NumberedList_ExtractsAll()
    {
        var text = @"
Top interview questions:
1. What is polymorphism in OOP?
2. How does garbage collection work in .NET?
3. Can you explain the SOLID principles?
";
        var questions = ExtractQuestionsFromText(text);

        Assert.Equal(3, questions.Count);
        Assert.Contains(questions, q => q.Contains("polymorphism"));
        Assert.Contains(questions, q => q.Contains("garbage collection"));
        Assert.Contains(questions, q => q.Contains("SOLID principles"));
    }

    [Fact]
    public void ExtractQuestions_NumberedListWithParenthesis_ExtractsCorrectly()
    {
        var text = @"
1) What is dependency injection?
2) How do microservices communicate?
";
        var questions = ExtractQuestionsFromText(text);

        Assert.Equal(2, questions.Count);
    }

    // ────────────── Markdown Headers ──────────────

    [Fact]
    public void ExtractQuestions_MarkdownHeaders_ExtractsQuestions()
    {
        var text = @"
# Technical Interview Guide

## What is the difference between stack and heap memory?

Some explanation here.

### How does async/await work in C#?

Another explanation.
";
        var questions = ExtractQuestionsFromText(text);

        Assert.True(questions.Count >= 2, $"Expected >= 2 questions, got {questions.Count}");
        Assert.Contains(questions, q => q.Contains("stack and heap"));
        Assert.Contains(questions, q => q.Contains("async/await") || q.Contains("async"));
    }

    // ────────────── HTML List Items ──────────────

    [Fact]
    public void ExtractQuestions_HtmlListItems_ExtractsQuestions()
    {
        var text = @"
<ul>
    <li>What is the time complexity of binary search?</li>
    <li>How do you implement a singleton pattern?</li>
    <li>Not a question - just information</li>
</ul>
";
        var questions = ExtractQuestionsFromText(text);

        Assert.True(questions.Count >= 2);
        Assert.Contains(questions, q => q.Contains("binary search"));
        Assert.Contains(questions, q => q.Contains("singleton"));
    }

    // ────────────── Bold/Strong Tags ──────────────

    [Fact]
    public void ExtractQuestions_BoldQuestions_ExtractsCorrectly()
    {
        var text = @"
<p>Here are some common questions:</p>
<strong>What are the main pillars of object-oriented programming?</strong>
<b>Can you explain the difference between abstract class and interface?</b>
";
        var questions = ExtractQuestionsFromText(text);

        Assert.True(questions.Count >= 2);
    }

    // ────────────── Edge Cases ──────────────

    [Fact]
    public void ExtractQuestions_EmptyText_ReturnsEmptyList()
    {
        var questions = ExtractQuestionsFromText("");

        Assert.Empty(questions);
    }

    [Fact]
    public void ExtractQuestions_NullText_ReturnsEmptyList()
    {
        var questions = ExtractQuestionsFromText(null!);

        Assert.Empty(questions);
    }

    [Fact]
    public void ExtractQuestions_NoQuestions_ReturnsEmptyList()
    {
        var text = "This is just a paragraph with no questions. It talks about programming in general.";

        var questions = ExtractQuestionsFromText(text);

        Assert.Empty(questions);
    }

    [Fact]
    public void ExtractQuestions_VeryShortQuestions_Excluded()
    {
        var text = @"
1. Why?
2. How?
3. What is the difference between a process and a thread in operating systems?
";
        var questions = ExtractQuestionsFromText(text);

        // "Why?" y "How?" son demasiado cortas (<15 chars), solo la tercera debería extraerse
        Assert.Single(questions);
        Assert.Contains(questions, q => q.Contains("process and a thread"));
    }

    // ────────────── Deduplication within extraction ──────────────

    [Fact]
    public void ExtractQuestions_DuplicateQuestions_ReturnedOnce()
    {
        var text = @"
1. What is polymorphism in programming?
2. What is polymorphism in programming?
";
        var questions = ExtractQuestionsFromText(text);

        // El HashSet interno debería deduplicar
        Assert.Single(questions);
    }

    // ────────────── Mixed Content ──────────────

    [Fact]
    public void ExtractQuestions_MixedFormats_ExtractsFromAll()
    {
        var text = @"
# Interview Questions

1. What is dependency injection and why is it useful?

<li>How does the event loop work in Node.js?</li>

<strong>Can you describe the CAP theorem in distributed systems?</strong>
";
        var questions = ExtractQuestionsFromText(text);

        Assert.True(questions.Count >= 3, $"Expected >= 3 questions from mixed formats, got {questions.Count}");
    }

    // ────────────── Validation ──────────────

    [Theory]
    [InlineData("What is polymorphism in programming?", true)]
    [InlineData("Too short?", false)]       // < 15 chars
    [InlineData("No question mark here", false)]
    [InlineData("", false)]
    public void IsValidQuestion_ValidatesCorrectly(string text, bool expected)
    {
        Assert.Equal(expected, IsValidQuestion(text));
    }

    [Fact]
    public void CleanQuestion_RemovesHtmlAndNumbers()
    {
        var raw = "42. <b>What is polymorphism?</b>";
        var cleaned = CleanQuestion(raw);

        Assert.DoesNotContain("<b>", cleaned);
        Assert.DoesNotContain("</b>", cleaned);
        Assert.DoesNotMatch(@"^\d+\.", cleaned);
        Assert.Contains("What is polymorphism?", cleaned);
    }

    [Fact]
    public void StripHtml_RemovesAllTags()
    {
        var html = "<p>This is <strong>bold</strong> and <em>italic</em></p>";
        var stripped = StripHtml(html);

        Assert.Equal("This is bold and italic", stripped);
    }

    // ────────────── Helper methods (replican la lógica de BaseScraper) ──────────────

    private static List<string> ExtractQuestionsFromText(string text)
    {
        var questions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return questions.ToList();

        // Patrón 1: Numbered list
        var numberedPattern = new Regex(@"^\s*\d+[\.\)]\s*(.+\?)\s*$", RegexOptions.Multiline);
        foreach (Match match in numberedPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patrón 2: Markdown headers
        var headerPattern = new Regex(@"^#+\s*(.+\?)\s*$", RegexOptions.Multiline);
        foreach (Match match in headerPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patrón 3: Lines ending with ?
        var questionMarkPattern = new Regex(@"^(.{20,}?\?)\s*$", RegexOptions.Multiline);
        foreach (Match match in questionMarkPattern.Matches(text))
        {
            var q = CleanQuestion(match.Groups[1].Value);
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patrón 4: HTML <li> items
        var liPattern = new Regex(@"<li[^>]*>\s*(.*?\?)\s*</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in liPattern.Matches(text))
        {
            var q = CleanQuestion(StripHtml(match.Groups[1].Value));
            if (IsValidQuestion(q)) questions.Add(q);
        }

        // Patrón 5: <strong>/<b> tags
        var boldPattern = new Regex(@"<(?:strong|b)>(.*?\?)</(?:strong|b)>", RegexOptions.IgnoreCase);
        foreach (Match match in boldPattern.Matches(text))
        {
            var q = CleanQuestion(StripHtml(match.Groups[1].Value));
            if (IsValidQuestion(q)) questions.Add(q);
        }

        return questions.ToList();
    }

    private static bool IsValidQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length < 15) return false;
        if (text.Length > 2000) return false;
        if (!text.Contains('?')) return false;
        return true;
    }

    private static string CleanQuestion(string text)
    {
        var cleaned = StripHtml(text);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"^\d+[\.\)]\s*", "");
        return cleaned;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return Regex.Replace(html, @"<[^>]+>", "").Trim();
    }
}
