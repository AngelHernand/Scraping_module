using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using InterviewSimulator.RAG.Core.Constants;
using InterviewSimulator.RAG.Core.Interfaces;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Processing.Chunking;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.RAG.Processing.Cleaning;

public class HtmlTextCleaner : ITextCleaner
{
    private readonly ILogger<HtmlTextCleaner> _logger;

    private static readonly Regex HtmlTagPattern = new(@"<\s*(p|div|article|span|section|h[1-6])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> NodesToRemove = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "iframe", "nav", "footer",
        "header", "aside", "svg", "form", "button"
    };

    private static readonly string[] ClassesToRemove = new[]
    {
        "sidebar", "footer", "header", "nav", "menu", "ad", "advertisement",
        "banner", "cookie", "popup", "modal", "social", "share", "comment",
        "related", "recommended", "newsletter", "subscribe", "signup",
        "author-bio", "author-info"
    };

    public HtmlTextCleaner(ILogger<HtmlTextCleaner> logger)
    {
        _logger = logger;
    }

    public Task<CleanedDocument> CleanAsync(string rawContent, string questionText, string sourceName)
    {
        var result = new CleanedDocument();
        var warnings = new List<string>();

        try
        {
            string cleanedText;

            if (IsHtml(rawContent))
            {
                cleanedText = ExtractFromHtml(rawContent, warnings);
            }
            else
            {
                cleanedText = rawContent;
            }

            // Limpieza específica por fuente
            cleanedText = CleanBySource(cleanedText, sourceName);

            // Normalización general
            cleanedText = TextNormalizer.Normalize(cleanedText);

            // Limpiar el texto de la pregunta
            string cleanedQuestion = TextNormalizer.Normalize(
                IsHtml(questionText) ? ExtractFromHtml(questionText, warnings) : questionText);

            // Detección de idioma
            string language = LanguageDetector.Detect(cleanedText);
            if (language == "mixed")
            {
                warnings.Add("Mixed language detected, defaulting to 'en'");
                language = "en";
            }

            // Estimación de tokens y validación
            int tokenCount = TokenCounter.EstimateTokens(cleanedText, language);
            bool hasSufficient = tokenCount >= RagConstants.MinTokensForContent;

            if (!hasSufficient)
                warnings.Add($"Insufficient content: {tokenCount} tokens (min: {RagConstants.MinTokensForContent})");
            else if (tokenCount < RagConstants.LowContentThreshold)
                warnings.Add($"Low content: {tokenCount} tokens");

            result.CleanedText = cleanedText;
            result.CleanedQuestionText = cleanedQuestion;
            result.DetectedLanguage = language;
            result.EstimatedTokenCount = tokenCount;
            result.CharCount = cleanedText.Length;
            result.HasSufficientContent = hasSufficient;
            result.CleaningWarnings = warnings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar contenido del ScrapedQuestion");
            result.HasSufficientContent = false;
            result.CleaningWarnings.Add($"Cleaning error: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private static bool IsHtml(string content)
    {
        return !string.IsNullOrWhiteSpace(content) && HtmlTagPattern.IsMatch(content);
    }

    private string ExtractFromHtml(string html, List<string> warnings)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remover nodos no deseados
        RemoveUnwantedNodes(doc);

        // Buscar contenido principal
        var contentNode = doc.DocumentNode.SelectSingleNode("//article")
                       ?? doc.DocumentNode.SelectSingleNode("//main")
                       ?? doc.DocumentNode.SelectSingleNode("//body")
                       ?? doc.DocumentNode;

        var sb = new StringBuilder();
        ExtractTextRecursive(contentNode, sb);

        return sb.ToString();
    }

    private static void RemoveUnwantedNodes(HtmlDocument doc)
    {
        var nodesToDelete = new List<HtmlNode>();

        foreach (var node in doc.DocumentNode.DescendantsAndSelf())
        {
            if (NodesToRemove.Contains(node.Name))
            {
                nodesToDelete.Add(node);
                continue;
            }

            var classAttr = node.GetAttributeValue("class", "");
            var idAttr = node.GetAttributeValue("id", "");
            var combined = $"{classAttr} {idAttr}".ToLowerInvariant();

            if (ClassesToRemove.Any(c => combined.Contains(c)))
            {
                nodesToDelete.Add(node);
            }
        }

        foreach (var node in nodesToDelete)
        {
            node.Remove();
        }
    }

    private static void ExtractTextRecursive(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
                sb.Append(text.Trim());
            return;
        }

        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
                sb.AppendLine();
                sb.Append("# ");
                AppendChildText(node, sb);
                sb.AppendLine();
                return;
            case "h2":
                sb.AppendLine();
                sb.Append("## ");
                AppendChildText(node, sb);
                sb.AppendLine();
                return;
            case "h3":
                sb.AppendLine();
                sb.Append("### ");
                AppendChildText(node, sb);
                sb.AppendLine();
                return;
            case "h4":
            case "h5":
            case "h6":
                sb.AppendLine();
                sb.Append("#### ");
                AppendChildText(node, sb);
                sb.AppendLine();
                return;
            case "p":
                sb.AppendLine();
                foreach (var child in node.ChildNodes)
                    ExtractTextRecursive(child, sb);
                sb.AppendLine();
                return;
            case "pre":
                sb.AppendLine();
                sb.AppendLine("```");
                var codeNode = node.SelectSingleNode(".//code") ?? node;
                sb.AppendLine(HtmlEntity.DeEntitize(codeNode.InnerText).Trim());
                sb.AppendLine("```");
                return;
            case "code":
                if (node.ParentNode?.Name != "pre")
                {
                    sb.Append('`');
                    sb.Append(HtmlEntity.DeEntitize(node.InnerText).Trim());
                    sb.Append('`');
                }
                return;
            case "li":
                sb.AppendLine();
                sb.Append("- ");
                foreach (var child in node.ChildNodes)
                    ExtractTextRecursive(child, sb);
                return;
            case "br":
                sb.AppendLine();
                return;
            case "strong":
            case "b":
                sb.Append("**");
                AppendChildText(node, sb);
                sb.Append("**");
                return;
            case "em":
            case "i":
                sb.Append('*');
                AppendChildText(node, sb);
                sb.Append('*');
                return;
            case "a":
                AppendChildText(node, sb);
                return;
            case "img":
                return; // Descartar imágenes
        }

        foreach (var child in node.ChildNodes)
            ExtractTextRecursive(child, sb);
    }

    private static void AppendChildText(HtmlNode node, StringBuilder sb)
    {
        foreach (var child in node.ChildNodes)
            ExtractTextRecursive(child, sb);
    }

    private static string CleanBySource(string text, string sourceName)
    {
        return sourceName?.ToLowerInvariant() switch
        {
            "devto" => CleanDevTo(text),
            "medium" => CleanMedium(text),
            "leetcode" => CleanLeetCode(text),
            "glassdoor" => CleanGlassdoor(text),
            "indeed" => CleanIndeed(text),
            _ => text
        };
    }

    private static string CleanDevTo(string text)
    {
        var lines = text.Split('\n')
            .Where(l =>
            {
                var trimmed = l.Trim().ToLowerInvariant();
                return !trimmed.StartsWith("cover image")
                    && !trimmed.StartsWith("discussion (")
                    && !trimmed.StartsWith("top comments")
                    && !trimmed.StartsWith("originally published at");
            });
        return string.Join('\n', lines);
    }

    private static string CleanMedium(string text)
    {
        var lines = text.Split('\n')
            .Where(l =>
            {
                var trimmed = l.Trim().ToLowerInvariant();
                return !trimmed.StartsWith("member-only story")
                    && !Regex.IsMatch(trimmed, @"^\d+ min read$")
                    && !trimmed.StartsWith("written by")
                    && !trimmed.StartsWith("more from");
            });
        return string.Join('\n', lines);
    }

    private static string CleanLeetCode(string text)
    {
        var lines = text.Split('\n')
            .Where(l =>
            {
                var trimmed = l.Trim().ToLowerInvariant();
                return !trimmed.StartsWith("accepted")
                    && !trimmed.StartsWith("submissions")
                    && !trimmed.StartsWith("acceptance rate")
                    && !trimmed.StartsWith("similar questions")
                    && !trimmed.StartsWith("related topics");
            });
        return string.Join('\n', lines);
    }

    private static string CleanGlassdoor(string text)
    {
        var lines = text.Split('\n')
            .Where(l =>
            {
                var trimmed = l.Trim().ToLowerInvariant();
                return !trimmed.StartsWith("interview question")
                    && !trimmed.StartsWith("add tags")
                    && !trimmed.StartsWith("no answers yet")
                    && !Regex.IsMatch(trimmed, @"^\d+(\.\d+)?$");
            });
        return string.Join('\n', lines);
    }

    private static string CleanIndeed(string text)
    {
        var lines = text.Split('\n')
            .Where(l =>
            {
                var trimmed = l.Trim().ToLowerInvariant();
                return !trimmed.StartsWith("related:")
                    && !trimmed.StartsWith("tips:")
                    && !trimmed.StartsWith("read more:");
            });
        return string.Join('\n', lines);
    }
}
