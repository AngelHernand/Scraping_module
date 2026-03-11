using InterviewSimulator.Scraping.Classifier;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace InterviewSimulator.Scraping.Tests.Unit;

/// <summary>
/// Tests unitarios para ContentClassifier — clasificador de documentos RAG.
/// Valida clasificación de categoría, tipo de contenido, dificultad, tags y relevancia IT.
/// </summary>
public class ContentClassifierTests
{
    private readonly ContentClassifier _classifier;

    public ContentClassifierTests()
    {
        var loggerMock = new Mock<ILogger<ContentClassifier>>();
        _classifier = new ContentClassifier(loggerMock.Object);
    }

    // ────────────── Category Classification ──────────────

    [Theory]
    [InlineData("Introduction to Java", "Java is a programming language for building enterprise applications with JVM and Maven")]
    [InlineData("Java Collections Framework", "Understanding ArrayList, HashMap and JDK data structures in Java")]
    public void ClassifyDocument_JavaContent_ReturnsCategoryJava(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.Equal(ContentCategory.Java, result.Category);
    }

    [Theory]
    [InlineData("Python Tutorial", "Learn python programming with pip, virtualenv, and jupyter notebooks")]
    [InlineData("Advanced Python", "CPython internals, pypi packages, and pep8 style guide for python")]
    public void ClassifyDocument_PythonContent_ReturnsCategoryPython(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.Equal(ContentCategory.Python, result.Category);
    }

    [Theory]
    [InlineData("React Hooks Guide", "Learn React hooks, JSX components, and Redux state management")]
    [InlineData("Next.js Tutorial", "Building React applications with Next.js and React Router")]
    public void ClassifyDocument_ReactContent_ReturnsCategoryReact(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.Equal(ContentCategory.React, result.Category);
    }

    [Theory]
    [InlineData("Docker Containers", "Docker containers, Docker Compose, and Dockerfile best practices for containerization")]
    [InlineData("Kubernetes Guide", "Introduction to Kubernetes pods, deployments, services, and kubectl")]
    public void ClassifyDocument_DevOpsContent_ReturnsDevOpsCategory(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.True(
            result.Category == ContentCategory.Docker || result.Category == ContentCategory.Kubernetes,
            $"Expected Docker or Kubernetes category but got {result.Category}");
    }

    [Theory]
    [InlineData("SQL Database Guide", "Understanding SQL queries, joins, indexes, and PostgreSQL stored procedures")]
    [InlineData("MongoDB NoSQL", "MongoDB collections, documents, aggregation pipeline, and BSON format")]
    public void ClassifyDocument_DatabaseContent_ReturnsDatabaseCategory(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.True(
            result.Category == ContentCategory.Sql || result.Category == ContentCategory.PostgreSql ||
            result.Category == ContentCategory.MongoDb,
            $"Expected database category but got {result.Category}");
    }

    [Theory]
    [InlineData("Microservices Architecture", "Designing microservices with API gateway, service mesh, event sourcing, and CQRS patterns")]
    [InlineData("Clean Architecture", "Understanding clean architecture, hexagonal architecture, and domain-driven design DDD")]
    public void ClassifyDocument_ArchitectureContent_ReturnsArchitectureCategory(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.True(
            result.Category == ContentCategory.Microservices ||
            result.Category == ContentCategory.CleanArchitecture ||
            result.Category == ContentCategory.Ddd,
            $"Expected architecture category but got {result.Category}");
    }

    [Theory]
    [InlineData("C# LINQ Tutorial", "Using C# LINQ queries with lambda expressions in dotnet applications")]
    [InlineData("ASP.NET Core Guide", "Building web APIs with ASP.NET Core and Entity Framework in C#")]
    public void ClassifyDocument_CSharpContent_ReturnsCSharpCategory(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.True(
            result.Category == ContentCategory.CSharp || result.Category == ContentCategory.AspNetCore,
            $"Expected C# or ASP.NET Core but got {result.Category}");
    }

    // ────────────── ContentType Classification ──────────────

    [Theory]
    [InlineData("Step-by-step Tutorial", "In this tutorial we will learn step by step how to implement a REST API. Let's build together.")]
    [InlineData("How to Setup Docker", "This hands-on guide shows how to install Docker. Follow these steps to set up your environment.")]
    public void ClassifyDocument_TutorialContent_ReturnsTutorialType(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.Equal(ContentType.Tutorial, result.ContentType);
    }

    [Theory]
    [InlineData("API Reference", "This reference documentation describes the available API endpoints, parameters, and return values.")]
    [InlineData("Configuration Specification", "The following specification defines the syntax, parameters, and valid values for configuration.")]
    public void ClassifyDocument_ReferenceContent_ReturnsReferenceOrDocumentationType(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.True(
            result.ContentType == ContentType.Reference || result.ContentType == ContentType.Documentation,
            $"Expected Reference or Documentation but got {result.ContentType}");
    }

    // ────────────── Difficulty Classification ──────────────

    [Theory]
    [InlineData("Intro to Programming", "This beginner tutorial covers the basics and fundamentals for getting started with coding")]
    [InlineData("HTML Basics", "Learn the fundamentals of HTML. This introduction is perfect for beginners getting started")]
    public void ClassifyDocument_BeginnerContent_ReturnsJuniorDifficulty(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.Equal(DifficultyLevel.Junior, result.Difficulty);
    }

    [Theory]
    [InlineData("Advanced Architecture", "Scalability patterns with distributed tracing, concurrency optimization, and high availability")]
    [InlineData("System Design", "Advanced distributed systems with fault tolerance, consensus algorithms, and system architecture")]
    public void ClassifyDocument_AdvancedContent_ReturnsSeniorDifficulty(string title, string content)
    {
        var result = _classifier.ClassifyDocument(title, content);
        Assert.Equal(DifficultyLevel.Senior, result.Difficulty);
    }

    // ────────────── Technology Name ──────────────

    [Fact]
    public void ClassifyDocument_JavaContent_ReturnsTechnologyNameJava()
    {
        var result = _classifier.ClassifyDocument("Java Programming", "Java enterprise development with JVM, JDK and Maven build system");
        Assert.Equal("Java", result.Technology);
    }

    [Fact]
    public void ClassifyDocument_ReactContent_ReturnsTechnologyNameReact()
    {
        var result = _classifier.ClassifyDocument("React Hooks", "Understanding React hooks, useState, useEffect, JSX and Redux");
        Assert.Equal("React", result.Technology);
    }

    // ────────────── Tags Extraction ──────────────

    [Fact]
    public void ClassifyDocument_ContentWithMultipleTechnologies_ExtractsTags()
    {
        var result = _classifier.ClassifyDocument(
            "Full Stack Development",
            "Building a full stack application with Python Django backend, React frontend, Docker containers, and PostgreSQL database API");

        Assert.NotNull(result.Tags);
        Assert.NotEmpty(result.Tags);
        // Should pick up at least some cross-technology tags
        Assert.True(result.Tags.Count >= 2, $"Expected >= 2 tags, got {result.Tags.Count}");
    }

    [Fact]
    public void ClassifyDocument_ContentWithTransversalTopics_IncludesTransversalTags()
    {
        var result = _classifier.ClassifyDocument(
            "REST API Security",
            "Securing REST API endpoints with authentication, authorization, and performance optimization in a cloud microservice");

        Assert.NotNull(result.Tags);
        // Should pick up transversal tags like API, Security, Performance, Cloud
        Assert.True(result.Tags.Any(t =>
            t.Equals("API", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Security", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Performance", StringComparison.OrdinalIgnoreCase)),
            $"Expected transversal tags, got: {string.Join(", ", result.Tags)}");
    }

    // ────────────── IT Relevance ──────────────

    [Theory]
    [InlineData("Docker Tutorial", "Learn how to build and deploy Docker containers for microservices")]
    [InlineData("Python Machine Learning", "Introduction to machine learning algorithms using Python and scikit-learn")]
    [InlineData("SQL Performance", "Optimizing SQL database queries with proper indexing and query plans in software development")]
    public void IsITRelevantContent_TechnicalContent_ReturnsTrue(string title, string content)
    {
        Assert.True(_classifier.IsITRelevantContent(title, content));
    }

    [Theory]
    [InlineData("Best Recipes 2024", "Discover the top cooking recipes for delicious meals this holiday season")]
    [InlineData("Sports News Today", "The latest football scores and basketball highlights from today's games")]
    [InlineData("Fashion Trends", "New clothing trends and celebrity style updates for the summer collection")]
    public void IsITRelevantContent_NonTechnicalContent_ReturnsFalse(string title, string content)
    {
        Assert.False(_classifier.IsITRelevantContent(title, content));
    }

    // ────────────── Edge Cases ──────────────

    [Fact]
    public void ClassifyDocument_EmptyContent_ReturnsDefaults()
    {
        var result = _classifier.ClassifyDocument("", "");
        Assert.Equal(ContentCategory.Unknown, result.Category);
    }

    [Fact]
    public void ClassifyDocument_NullTitle_DoesNotThrow()
    {
        var result = _classifier.ClassifyDocument(null!, "Some content about Python programming");
        Assert.NotNull(result);
    }

    [Fact]
    public void ClassifyDocument_VeryLongContent_CompletesWithinReasonableTime()
    {
        // Generar contenido largo con keywords de múltiples categorías
        var longContent = string.Join(" ",
            Enumerable.Range(0, 1000).Select(_ =>
                "java python react docker kubernetes sql microservices api tutorial programming"));

        var result = _classifier.ClassifyDocument("Technical Overview", longContent);

        Assert.NotNull(result);
        Assert.True(result.Tags.Count > 0);
    }

    // ────────────── Confidence Score ──────────────

    [Fact]
    public void ClassifyDocument_StrongSignals_HasHighConfidence()
    {
        var result = _classifier.ClassifyDocument(
            "Java Collections Framework Deep Dive",
            "Understanding Java ArrayList, HashMap, JDK collections, and JVM internals for Java enterprise development");

        Assert.True(result.ConfidenceScore >= 0.5, $"Expected high confidence, got {result.ConfidenceScore}");
    }

    [Fact]
    public void ClassifyDocument_WeakSignals_HasLowerConfidence()
    {
        var result = _classifier.ClassifyDocument(
            "Some article",
            "General overview of various topics in technology");

        Assert.True(result.ConfidenceScore < 0.8, $"Expected moderate/low confidence, got {result.ConfidenceScore}");
    }

    // ────────────── Subcategory ──────────────

    [Fact]
    public void ClassifyDocument_CSharpLINQ_ReturnsLINQSubcategory()
    {
        var result = _classifier.ClassifyDocument(
            "C# LINQ Guide",
            "Using LINQ queries with lambda expressions, Select, Where, GroupBy in C# dotnet applications");

        Assert.Equal(ContentCategory.CSharp, result.Category);
        Assert.NotNull(result.Subcategory);
        Assert.Contains("LINQ", result.Subcategory);
    }

    [Fact]
    public void ClassifyDocument_ReactHooks_ReturnsHooksSubcategory()
    {
        var result = _classifier.ClassifyDocument(
            "React Hooks Tutorial",
            "Understanding React hooks like useState, useEffect, useContext, and custom hooks in React JSX");

        Assert.Equal(ContentCategory.React, result.Category);
        Assert.NotNull(result.Subcategory);
        Assert.Contains("Hooks", result.Subcategory);
    }
}
