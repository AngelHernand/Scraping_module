using InterviewSimulator.RAG.Core.Models;
using InterviewSimulator.RAG.Retrieval;

namespace InterviewSimulator.RAG.Tests.Unit;

public class QueryBuilderTests
{
    [Fact]
    public void BuildFilters_WithCategory_IncludesCategoryFilter()
    {
        var query = new RetrievalQuery
        {
            Category = "Technical",
            QueryText = "What is dependency injection?"
        };

        var filters = QueryBuilder.BuildFilters(query);

        Assert.Contains("category", filters.Keys);
        Assert.Equal("Technical", filters["category"]);
    }

    [Fact]
    public void BuildFilters_WithAllFields_IncludesAllFilters()
    {
        var query = new RetrievalQuery
        {
            Category = "Technical",
            Subcategory = "C#",
            DifficultyLevel = "Senior",
            PreferredLanguage = "en",
            Tags = new List<string> { "LINQ", "async" },
            QueryText = "LINQ query"
        };

        var filters = QueryBuilder.BuildFilters(query);

        Assert.Equal(5, filters.Count);
        Assert.Equal("Technical", filters["category"]);
        Assert.Equal("C#", filters["subcategory"]);
        Assert.Equal("Senior", filters["difficulty_level"]);
        Assert.Equal("en", filters["original_language"]);
        Assert.IsType<string[]>(filters["tags"]);
    }

    [Fact]
    public void BuildFilters_EmptyQuery_ReturnsEmptyDictionary()
    {
        var query = new RetrievalQuery
        {
            QueryText = "general question"
        };

        var filters = QueryBuilder.BuildFilters(query);

        Assert.Empty(filters);
    }

    [Fact]
    public void BuildQueryText_WithMetadata_PrependsContext()
    {
        var query = new RetrievalQuery
        {
            Category = "Technical",
            Subcategory = "Java",
            DifficultyLevel = "Mid",
            QueryText = "Explain Spring Boot dependency injection"
        };

        string text = QueryBuilder.BuildQueryText(query);

        Assert.Contains("Category: Technical", text);
        Assert.Contains("Subcategory: Java", text);
        Assert.Contains("Difficulty: Mid", text);
        Assert.Contains("Explain Spring Boot dependency injection", text);
    }

    [Fact]
    public void BuildQueryText_MinimalQuery_ReturnsQueryTextOnly()
    {
        var query = new RetrievalQuery
        {
            QueryText = "What is a binary tree?"
        };

        string text = QueryBuilder.BuildQueryText(query);

        Assert.Equal("What is a binary tree?", text);
    }
}
