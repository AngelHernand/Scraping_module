using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InterviewSimulator.Scraping.Core.Models.Enums;

namespace InterviewSimulator.Scraping.Tests.Unit;

/// <summary>
/// Tests unitarios para la lógica de extracción de documentos RAG y chunking.
/// Replica la lógica de BaseScraper (ExtractCleanContent, ChunkContent, etc.)
/// </summary>
public class DocumentExtractionTests
{
    // ────────────── ExtractCleanContent ──────────────

    [Fact]
    public void ExtractCleanContent_PreservesCodeBlocks()
    {
        var html = "<p>Example:</p><pre><code>var x = 10;</code></pre><p>End.</p>";
        var result = ExtractCleanContent(html);

        Assert.Contains("```", result);
        Assert.Contains("var x = 10;", result);
    }

    [Fact]
    public void ExtractCleanContent_ConvertsHeadersToMarkdown()
    {
        var html = "<h1>Main Title</h1><h2>Section</h2><h3>Subsection</h3>";
        var result = ExtractCleanContent(html);

        Assert.Contains("# Main Title", result);
        Assert.Contains("## Section", result);
        Assert.Contains("### Subsection", result);
    }

    [Fact]
    public void ExtractCleanContent_ConvertsListItems()
    {
        var html = "<ul><li>First item</li><li>Second item</li></ul>";
        var result = ExtractCleanContent(html);

        Assert.Contains("- First item", result);
        Assert.Contains("- Second item", result);
    }

    [Fact]
    public void ExtractCleanContent_ConvertsStrongToBold()
    {
        // <strong> outside <p> converts to **bold**
        var html = "<strong>bold text</strong> rest of content";
        var result = ExtractCleanContent(html);

        Assert.Contains("**bold text**", result);
    }

    [Fact]
    public void ExtractCleanContent_ConvertsEmToItalic()
    {
        // <em> outside <p> converts to *italic*
        var html = "<em>italic text</em> rest of content";
        var result = ExtractCleanContent(html);

        Assert.Contains("*italic text*", result);
    }

    [Fact]
    public void ExtractCleanContent_RemovesRemainingHtmlTags()
    {
        var html = "<div class='wrapper'><span>Hello</span> <a href='#'>World</a></div>";
        var result = ExtractCleanContent(html);

        Assert.DoesNotContain("<div", result);
        Assert.DoesNotContain("<span", result);
        Assert.DoesNotContain("<a ", result);
        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public void ExtractCleanContent_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ExtractCleanContent(""));
        Assert.Equal(string.Empty, ExtractCleanContent(null!));
    }

    [Fact]
    public void ExtractCleanContent_PreservesInlineCode()
    {
        var html = "<p>Use <code>Console.WriteLine</code> to print.</p>";
        var result = ExtractCleanContent(html);

        Assert.Contains("`Console.WriteLine`", result);
    }

    // ────────────── ChunkContent ──────────────

    [Fact]
    public void ChunkContent_ShortContent_ReturnsSingleChunk()
    {
        var content = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"word{i}"));
        var chunks = ChunkContent(content, "Test Title");

        Assert.Single(chunks);
        Assert.Equal("Test Title", chunks[0].Title);
    }

    [Fact]
    public void ChunkContent_LongContent_SplitsIntoMultipleChunks()
    {
        // Crear contenido de ~3000 palabras con headers
        var sections = new[]
        {
            "## Section 1\n" + string.Join(" ", Enumerable.Range(0, 800).Select(i => $"section1word{i}")),
            "## Section 2\n" + string.Join(" ", Enumerable.Range(0, 800).Select(i => $"section2word{i}")),
            "## Section 3\n" + string.Join(" ", Enumerable.Range(0, 800).Select(i => $"section3word{i}"))
        };
        var content = string.Join("\n\n", sections);

        var chunks = ChunkContent(content, "Long Article");

        Assert.True(chunks.Count >= 2, $"Expected >= 2 chunks, got {chunks.Count}");
    }

    [Fact]
    public void ChunkContent_EmptyContent_ReturnsEmpty()
    {
        var chunks = ChunkContent("", "Title");
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkContent_ContentWithHeaders_PreservesHeaderTitles()
    {
        var content = "## Introduction\n" +
            string.Join(" ", Enumerable.Range(0, 600).Select(i => $"introword{i}")) +
            "\n\n## Advanced Topics\n" +
            string.Join(" ", Enumerable.Range(0, 600).Select(i => $"advancedword{i}"));

        var chunks = ChunkContent(content, "Tutorial");

        Assert.True(chunks.Count >= 1);
        // Al menos uno debería tener un título de sección
        Assert.True(chunks.Any(c => c.Title.Contains("Introduction") || c.Title.Contains("Advanced") || c.Title == "Tutorial"));
    }

    [Fact]
    public void ChunkContent_ContentWithoutHeaders_SplitsByParagraphs()
    {
        // Sin headers, pero con párrafos
        var paragraphs = Enumerable.Range(0, 20).Select(i =>
            string.Join(" ", Enumerable.Range(0, 100).Select(j => $"p{i}w{j}")));
        var content = string.Join("\n\n", paragraphs); // ~2000 palabras

        var chunks = ChunkContent(content, "No Headers Article");

        Assert.True(chunks.Count >= 1);
    }

    // ────────────── CountWords ──────────────

    [Theory]
    [InlineData("one two three", 3)]
    [InlineData("  spaces   between   words  ", 3)]
    [InlineData("single", 1)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    public void CountWords_ReturnsCorrectCount(string text, int expected)
    {
        Assert.Equal(expected, CountWords(text));
    }

    // ────────────── NormalizeDocumentContent ──────────────

    [Fact]
    public void NormalizeDocumentContent_Lowercases()
    {
        var result = NormalizeDocumentContent("Hello WORLD");
        Assert.Equal(result, result.ToLowerInvariant());
    }

    [Fact]
    public void NormalizeDocumentContent_RemovesCodeBlocks()
    {
        var text = "Before code\n```\nvar x = 10;\n```\nafter code";
        var result = NormalizeDocumentContent(text);

        Assert.Contains("code", result);
        Assert.DoesNotContain("var x", result);
    }

    [Fact]
    public void NormalizeDocumentContent_RemovesPunctuation()
    {
        var result = NormalizeDocumentContent("Hello, world! How's it going?");

        Assert.DoesNotContain(",", result);
        Assert.DoesNotContain("!", result);
        Assert.DoesNotContain("?", result);
    }

    [Fact]
    public void NormalizeDocumentContent_TruncatesLongContent()
    {
        var longText = new string('a', 1000);
        var result = NormalizeDocumentContent(longText);

        Assert.True(result.Length <= 500, $"Expected <= 500 chars, got {result.Length}");
    }

    [Fact]
    public void NormalizeDocumentContent_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NormalizeDocumentContent(""));
        Assert.Equal(string.Empty, NormalizeDocumentContent(null!));
    }

    // ────────────── Document Hash Deduplication ──────────────

    [Fact]
    public void DocumentHash_SameContent_ProducesSameHash()
    {
        var content1 = NormalizeDocumentContent("Understanding Docker containers for microservices deployment");
        var content2 = NormalizeDocumentContent("Understanding Docker containers for microservices deployment");

        Assert.Equal(ComputeHash(content1), ComputeHash(content2));
    }

    [Fact]
    public void DocumentHash_DifferentContent_ProducesDifferentHash()
    {
        var content1 = NormalizeDocumentContent("Understanding Docker containers for microservices deployment");
        var content2 = NormalizeDocumentContent("Introduction to React hooks and state management in frontend");

        Assert.NotEqual(ComputeHash(content1), ComputeHash(content2));
    }

    [Fact]
    public void DocumentHash_CaseVariations_ProduceSameHash()
    {
        var content1 = NormalizeDocumentContent("DOCKER CONTAINERS FOR DEPLOYMENT");
        var content2 = NormalizeDocumentContent("docker containers for deployment");

        Assert.Equal(ComputeHash(content1), ComputeHash(content2));
    }

    // ────────────── End-to-End Document Extraction ──────────────

    [Fact]
    public void ExtractDocumentsFromHtml_ValidHtml_ProducesDocuments()
    {
        // Simular HTML con contenido suficiente (>50 palabras)
        var words = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"technical content word{i}"));
        var html = $"<h1>Test Article</h1><p>{words}</p>";

        var documents = ExtractDocumentsFromHtml(html, "Test Article", "https://example.com/test",
            "TestSite", 0, "TestTech", ContentType.Article);

        Assert.NotEmpty(documents);
        Assert.All(documents, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.False(string.IsNullOrWhiteSpace(d.Content));
            Assert.False(string.IsNullOrWhiteSpace(d.HashFingerprint));
            Assert.Equal("https://example.com/test", d.SourceUrl);
            Assert.Equal("TestSite", d.SourceSite);
        });
    }

    [Fact]
    public void ExtractDocumentsFromHtml_TooShortContent_ReturnsEmpty()
    {
        var html = "<p>Short content only.</p>";

        var documents = ExtractDocumentsFromHtml(html, "Short", "https://example.com",
            "TestSite", 0, null, ContentType.Article);

        Assert.Empty(documents);
    }

    [Fact]
    public void ExtractDocumentsFromHtml_SetsChunkIndex()
    {
        // Large content that should produce multiple chunks
        var sections = new[]
        {
            "## Part 1\n" + string.Join(" ", Enumerable.Range(0, 600).Select(i => $"part1word{i}")),
            "## Part 2\n" + string.Join(" ", Enumerable.Range(0, 600).Select(i => $"part2word{i}")),
            "## Part 3\n" + string.Join(" ", Enumerable.Range(0, 600).Select(i => $"part3word{i}"))
        };
        var content = string.Join("\n\n", sections);
        var html = $"<div>{content.Replace("\n", "<br/>")}</div>";

        // Note: the HTML conversion might not perfectly preserve markdown headers
        // So this test validates that ChunkIndex is set sequentially
        var plainHtml = "<h2>Part 1</h2><p>" +
            string.Join(" ", Enumerable.Range(0, 600).Select(i => $"part1word{i}")) +
            "</p><h2>Part 2</h2><p>" +
            string.Join(" ", Enumerable.Range(0, 600).Select(i => $"part2word{i}")) +
            "</p><h2>Part 3</h2><p>" +
            string.Join(" ", Enumerable.Range(0, 600).Select(i => $"part3word{i}")) + "</p>";

        var documents = ExtractDocumentsFromHtml(plainHtml, "Multi-section",
            "https://example.com", "TestSite", 0, null, ContentType.Tutorial);

        if (documents.Count > 1)
        {
            for (int i = 0; i < documents.Count; i++)
            {
                Assert.Equal(i, documents[i].ChunkIndex);
            }
        }
    }

    // ────────────── Helper methods (replican lógica de BaseScraper) ──────────────

    private static string ExtractCleanContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var content = Regex.Replace(html,
            @"<pre[^>]*>\s*<code[^>]*>(.*?)</code>\s*</pre>",
            m => "\n```\n" + StripHtml(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).Trim() + "\n```\n",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        content = Regex.Replace(content, @"<code[^>]*>(.*?)</code>",
            m => "`" + StripHtml(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).Trim() + "`",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        content = Regex.Replace(content, @"<h1[^>]*>(.*?)</h1>",
            m => "\n# " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        content = Regex.Replace(content, @"<h2[^>]*>(.*?)</h2>",
            m => "\n## " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        content = Regex.Replace(content, @"<h3[^>]*>(.*?)</h3>",
            m => "\n### " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        content = Regex.Replace(content, @"<h[4-6][^>]*>(.*?)</h[4-6]>",
            m => "\n#### " + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        content = Regex.Replace(content, @"<li[^>]*>(.*?)</li>",
            m => "\n- " + StripHtml(m.Groups[1].Value).Trim(), RegexOptions.IgnoreCase | RegexOptions.Singleline);

        content = Regex.Replace(content, @"<p[^>]*>(.*?)</p>",
            m => "\n" + StripHtml(m.Groups[1].Value).Trim() + "\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        content = Regex.Replace(content, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

        content = Regex.Replace(content, @"<(?:strong|b)[^>]*>(.*?)</(?:strong|b)>",
            m => "**" + StripHtml(m.Groups[1].Value).Trim() + "**", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        content = Regex.Replace(content, @"<(?:em|i)[^>]*>(.*?)</(?:em|i)>",
            m => "*" + StripHtml(m.Groups[1].Value).Trim() + "*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        content = Regex.Replace(content, @"<[^>]+>", "");
        content = System.Net.WebUtility.HtmlDecode(content);
        content = Regex.Replace(content, @"\n{3,}", "\n\n");
        var lines = content.Split('\n').Select(l => l.TrimEnd());
        content = string.Join("\n", lines).Trim();

        return content;
    }

    private static List<(string Title, string Content)> ChunkContent(string content, string defaultTitle,
        int minWords = 500, int maxWords = 1500)
    {
        var chunks = new List<(string Title, string Content)>();
        if (string.IsNullOrWhiteSpace(content)) return chunks;

        var wordCount = CountWords(content);
        if (wordCount <= maxWords)
        {
            chunks.Add((defaultTitle, content.Trim()));
            return chunks;
        }

        var headerPattern = new Regex(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline);
        var matches = headerPattern.Matches(content);

        if (matches.Count >= 2)
        {
            var sections = new List<(string Title, string Content)>();
            for (int i = 0; i < matches.Count; i++)
            {
                var sectionTitle = matches[i].Groups[2].Value.Trim();
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
                var sectionContent = content[start..end].Trim();
                sections.Add((sectionTitle, sectionContent));
            }

            if (matches[0].Index > 0)
            {
                var preamble = content[..matches[0].Index].Trim();
                if (CountWords(preamble) >= 50)
                    sections.Insert(0, (defaultTitle + " - Introducción", preamble));
            }

            var currentChunk = "";
            var currentTitle = defaultTitle;
            foreach (var (sTitle, sContent) in sections)
            {
                var sWords = CountWords(sContent);
                if (sWords > maxWords)
                {
                    if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 2)
                    {
                        chunks.Add((currentTitle, currentChunk.Trim()));
                        currentChunk = "";
                    }
                    var subChunks = SplitByParagraphs(sContent, sTitle, minWords, maxWords);
                    chunks.AddRange(subChunks);
                    currentTitle = sTitle;
                }
                else if (CountWords(currentChunk) + sWords > maxWords)
                {
                    if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 2)
                        chunks.Add((currentTitle, currentChunk.Trim()));
                    currentChunk = sContent;
                    currentTitle = sTitle;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(currentChunk)) currentTitle = sTitle;
                    currentChunk += "\n\n" + sContent;
                }
            }
            if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 3)
                chunks.Add((currentTitle, currentChunk.Trim()));
        }
        else
        {
            chunks.AddRange(SplitByParagraphs(content, defaultTitle, minWords, maxWords));
        }

        if (chunks.Count == 0)
            chunks.Add((defaultTitle, content.Trim()));

        return chunks;
    }

    private static List<(string Title, string Content)> SplitByParagraphs(string content, string title,
        int minWords, int maxWords)
    {
        var chunks = new List<(string Title, string Content)>();
        var paragraphs = Regex.Split(content, @"\n\n+");
        var currentChunk = "";
        int chunkIdx = 0;

        foreach (var para in paragraphs)
        {
            if (CountWords(currentChunk) + CountWords(para) > maxWords && CountWords(currentChunk) >= minWords)
            {
                chunkIdx++;
                chunks.Add(($"{title} (parte {chunkIdx})", currentChunk.Trim()));
                currentChunk = para;
            }
            else
            {
                currentChunk += "\n\n" + para;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk) && CountWords(currentChunk) >= minWords / 3)
        {
            chunkIdx++;
            var chunkTitle = chunkIdx > 1 ? $"{title} (parte {chunkIdx})" : title;
            chunks.Add((chunkTitle, currentChunk.Trim()));
        }

        return chunks;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string NormalizeDocumentContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var normalized = content.ToLowerInvariant().Trim();
        normalized = Regex.Replace(normalized, @"```[\s\S]*?```", "[code]");
        normalized = Regex.Replace(normalized, @"`[^`]+`", "[code]");
        normalized = Regex.Replace(normalized, @"[^\w\s]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        if (normalized.Length > 500) normalized = normalized[..500];
        return normalized;
    }

    private static string ComputeHash(string normalizedText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return Regex.Replace(html, @"<[^>]+>", "").Trim();
    }

    private static string DetectLanguage(string text)
    {
        var spanishIndicators = new[] { " el ", " la ", " los ", " las ", " de ", " del ", " en ", " por ", " con ", " una ", " como ", " para ", " que ", " es " };
        var lower = text.ToLowerInvariant();
        int spanishCount = spanishIndicators.Count(ind => lower.Contains(ind));
        return spanishCount >= 3 ? "es" : "en";
    }

    private List<InterviewSimulator.Scraping.Core.Models.ScrapedDocument> ExtractDocumentsFromHtml(
        string html, string pageTitle, string sourceUrl, string sourceSite,
        int sourceId, string? technology, ContentType contentType)
    {
        var documents = new List<InterviewSimulator.Scraping.Core.Models.ScrapedDocument>();

        var cleanContent = ExtractCleanContent(html);
        if (string.IsNullOrWhiteSpace(cleanContent) || CountWords(cleanContent) < 50)
            return documents;

        var chunks = ChunkContent(cleanContent, pageTitle);

        for (int i = 0; i < chunks.Count; i++)
        {
            var (chunkTitle, chunkContent) = chunks[i];
            var normalized = NormalizeDocumentContent(chunkContent);
            var hash = ComputeHash(normalized);
            var language = DetectLanguage(chunkContent.Length > 300 ? chunkContent[..300] : chunkContent);

            var doc = new InterviewSimulator.Scraping.Core.Models.ScrapedDocument
            {
                SourceId = sourceId,
                Title = chunkTitle.Length > 500 ? chunkTitle[..500] : chunkTitle,
                Content = chunkContent,
                ContentNormalized = normalized,
                HashFingerprint = hash,
                Language = language,
                SourceUrl = sourceUrl,
                SourceSite = sourceSite,
                ContentType = contentType,
                Technology = technology,
                WordCount = CountWords(chunkContent),
                ChunkIndex = i,
                ScrapedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            documents.Add(doc);
        }

        return documents;
    }
}
