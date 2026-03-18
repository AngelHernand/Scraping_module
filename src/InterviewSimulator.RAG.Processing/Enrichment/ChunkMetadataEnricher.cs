using System.Text.Json;
using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.Scraping.Core.Models;
using Microsoft.Extensions.Logging;

namespace InterviewSimulator.RAG.Processing.Enrichment;

public class ChunkMetadataEnricher
{
    private readonly ILogger<ChunkMetadataEnricher> _logger;

    private static readonly Dictionary<string, string[]> TechnologyKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = new[] { "c#", "csharp", ".net", "asp.net", "linq", "entity framework", "blazor" },
        ["Java"] = new[] { "java", "spring", "spring boot", "hibernate", "maven", "gradle", "jvm" },
        ["Python"] = new[] { "python", "django", "flask", "fastapi", "pandas", "numpy", "pip" },
        ["JavaScript"] = new[] { "javascript", "js", "node.js", "nodejs", "react", "angular", "vue", "typescript", "express" },
        ["SQL"] = new[] { "sql", "mysql", "postgresql", "sql server", "oracle db", "sqlite", "nosql", "mongodb" },
        ["DevOps"] = new[] { "docker", "kubernetes", "ci/cd", "jenkins", "terraform", "ansible", "aws", "azure", "gcp" },
        ["Data Structures"] = new[] { "array", "linked list", "tree", "graph", "hash", "stack", "queue", "heap" },
        ["Algorithms"] = new[] { "sorting", "searching", "dynamic programming", "recursion", "big o", "complexity" },
        ["System Design"] = new[] { "microservices", "load balancer", "caching", "message queue", "api gateway", "scalability" },
        ["React"] = new[] { "react", "jsx", "hooks", "usestate", "useeffect", "redux", "next.js" },
        ["Angular"] = new[] { "angular", "rxjs", "ngrx", "directive", "component", "module" },
    };

    public ChunkMetadataEnricher(ILogger<ChunkMetadataEnricher> logger)
    {
        _logger = logger;
    }

    public List<TextChunk> Enrich(List<TextChunk> chunks, ScrapedQuestion source)
    {
        var tags = ParseTags(source.Tags);

        foreach (var chunk in chunks)
        {
            // Copiar metadata de la fuente
            chunk.Category = source.Category.ToString();
            chunk.Subcategory = source.Subcategory ?? string.Empty;
            chunk.DifficultyLevel = source.DifficultyLevel.ToString();
            chunk.Tags = new List<string>(tags);
            chunk.SourceUrl = source.SourceUrl ?? string.Empty;
            chunk.SourceName = source.Source?.Name ?? string.Empty;
            chunk.ScrapedAt = source.ScrapedAt;
            chunk.Language = source.OriginalLanguage ?? "en";

            // Detectar tecnologías adicionales en el texto del chunk
            var detectedTags = DetectTechnologyTags(chunk.Text);
            foreach (var tag in detectedTags)
            {
                if (!chunk.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    chunk.Tags.Add(tag);
            }

            // Re-clasificar categoría Unknown basándose en el contenido
            if (chunk.Category == "Unknown")
            {
                chunk.Category = InferCategory(chunk.Text);
            }
        }

        return chunks;
    }

    private static List<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> DetectTechnologyTags(string text)
    {
        var detected = new List<string>();
        var lowerText = text.ToLowerInvariant();

        foreach (var (technology, keywords) in TechnologyKeywords)
        {
            if (keywords.Any(kw => lowerText.Contains(kw)))
            {
                detected.Add(technology);
            }
        }

        return detected;
    }

    private static string InferCategory(string text)
    {
        var lower = text.ToLowerInvariant();

        // Patrones técnicos
        if (lower.Contains("code") || lower.Contains("algorithm") || lower.Contains("function")
            || lower.Contains("class") || lower.Contains("method") || lower.Contains("api")
            || lower.Contains("database") || lower.Contains("sql") || lower.Contains("framework")
            || lower.Contains("código") || lower.Contains("algoritmo") || lower.Contains("función"))
            return "Technical";

        // Patrones comportamentales
        if (lower.Contains("teamwork") || lower.Contains("conflict") || lower.Contains("challenge")
            || lower.Contains("leadership") || lower.Contains("tell me about a time")
            || lower.Contains("trabajo en equipo") || lower.Contains("conflicto") || lower.Contains("liderazgo"))
            return "Behavioral";

        // Patrones situacionales
        if (lower.Contains("what would you do") || lower.Contains("how would you handle")
            || lower.Contains("qué harías") || lower.Contains("cómo manejarías"))
            return "Situational";

        return "General";
    }
}
