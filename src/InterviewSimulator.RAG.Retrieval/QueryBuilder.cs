using InterviewSimulator.RAG.Core.Models;

namespace InterviewSimulator.RAG.Retrieval;

public static class QueryBuilder
{
    public static Dictionary<string, object> BuildFilters(RetrievalQuery query)
    {
        var filters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(query.Category))
            filters["category"] = query.Category;

        if (!string.IsNullOrWhiteSpace(query.Subcategory))
            filters["subcategory"] = query.Subcategory;

        if (!string.IsNullOrWhiteSpace(query.DifficultyLevel))
            filters["difficulty_level"] = query.DifficultyLevel;

        if (!string.IsNullOrWhiteSpace(query.PreferredLanguage))
            filters["original_language"] = query.PreferredLanguage;

        if (query.Tags is { Count: > 0 })
            filters["tags"] = query.Tags.ToArray();

        return filters;
    }

    public static string BuildQueryText(RetrievalQuery query)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Category))
            parts.Add($"Category: {query.Category}");

        if (!string.IsNullOrWhiteSpace(query.Subcategory))
            parts.Add($"Subcategory: {query.Subcategory}");

        if (!string.IsNullOrWhiteSpace(query.DifficultyLevel))
            parts.Add($"Difficulty: {query.DifficultyLevel}");

        parts.Add(query.QueryText);

        return string.Join("\n", parts);
    }
}
