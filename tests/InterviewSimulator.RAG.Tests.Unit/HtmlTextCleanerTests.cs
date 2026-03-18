using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Processing.Cleaning;
using Microsoft.Extensions.Logging;
using Moq;

namespace InterviewSimulator.RAG.Tests.Unit;

public class HtmlTextCleanerTests
{
    private readonly HtmlTextCleaner _cleaner;

    public HtmlTextCleanerTests()
    {
        var logger = new Mock<ILogger<HtmlTextCleaner>>();
        _cleaner = new HtmlTextCleaner(logger.Object);
    }

    [Fact]
    public async Task CleanAsync_PlainText_ReturnsNormalizedText()
    {
        var result = await _cleaner.CleanAsync(
            "This is a plain text answer about programming concepts and data structures.",
            "What are data structures?",
            "testSource");

        Assert.False(string.IsNullOrWhiteSpace(result.CleanedText));
        Assert.False(string.IsNullOrWhiteSpace(result.CleanedQuestionText));
        Assert.True(result.CharCount > 0);
    }

    [Fact]
    public async Task CleanAsync_HtmlContent_ExtractsText()
    {
        string html = @"<html><body>
            <article>
                <h1>Interview Question</h1>
                <p>This is a detailed explanation about <strong>important concepts</strong> in software engineering.</p>
                <pre><code>var x = 42;</code></pre>
                <p>More text about algorithms and data structures in programming interviews.</p>
            </article>
            <footer>Site footer</footer>
        </body></html>";

        var result = await _cleaner.CleanAsync(html, "What is software engineering?", "testSource");

        Assert.Contains("Interview Question", result.CleanedText);
        Assert.Contains("important concepts", result.CleanedText);
        Assert.Contains("var x = 42", result.CleanedText);
        Assert.DoesNotContain("Site footer", result.CleanedText);
    }

    [Fact]
    public async Task CleanAsync_RemovesScriptAndStyleTags()
    {
        string html = @"<div>
            <script>alert('test');</script>
            <style>.class { color: red; }</style>
            <p>Actual content about programming and interview preparation.</p>
        </div>";

        var result = await _cleaner.CleanAsync(html, "Test?", "testSource");

        Assert.DoesNotContain("alert", result.CleanedText);
        Assert.DoesNotContain("color: red", result.CleanedText);
        Assert.Contains("Actual content", result.CleanedText);
    }

    [Fact]
    public async Task CleanAsync_DetectsLanguage()
    {
        var resultEn = await _cleaner.CleanAsync(
            "This is a question about programming and software development concepts.",
            "What is programming?",
            "testSource");

        var resultEs = await _cleaner.CleanAsync(
            "Esta es una explicación detallada sobre conceptos de programación y desarrollo de software para entrevistas.",
            "¿Qué es la programación?",
            "testSource");

        Assert.Equal("en", resultEn.DetectedLanguage);
        Assert.Equal("es", resultEs.DetectedLanguage);
    }

    [Fact]
    public async Task CleanAsync_InsufficientContent_ReturnsFalse()
    {
        var result = await _cleaner.CleanAsync("short", "Q?", "testSource");

        Assert.False(result.HasSufficientContent);
        Assert.True(result.CleaningWarnings.Count > 0);
    }

    [Fact]
    public async Task CleanAsync_ConvertHeadersToMarkdown()
    {
        string html = @"<div>
            <h1>Main Title</h1>
            <p>Content about programming interview topics and preparation strategies.</p>
            <h2>Sub Title</h2>
            <p>More detailed content about technical interview questions.</p>
        </div>";

        var result = await _cleaner.CleanAsync(html, "Question?", "testSource");

        Assert.Contains("# Main Title", result.CleanedText);
        Assert.Contains("## Sub Title", result.CleanedText);
    }

    [Fact]
    public async Task CleanAsync_SourceSpecificCleaning_DevTo()
    {
        string text = "Good content about programming.\nOriginally published at example.com\nMore content about interviews.";

        var result = await _cleaner.CleanAsync(text, "Q?", "devto");

        Assert.DoesNotContain("Originally published at", result.CleanedText);
        Assert.Contains("Good content", result.CleanedText);
    }
}
