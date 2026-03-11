using InterviewSimulator.Scraping.Classifier;
using InterviewSimulator.Scraping.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace InterviewSimulator.Scraping.Tests.Unit;

/// <summary>
/// Tests unitarios para el KeywordClassifier.
/// Valida clasificación por categoría, subcategoría y dificultad.
/// </summary>
public class ClassifierTests
{
    private readonly KeywordClassifier _classifier;

    public ClassifierTests()
    {
        var loggerMock = new Mock<ILogger<KeywordClassifier>>();
        _classifier = new KeywordClassifier(loggerMock.Object);
    }

    // ────────────── Category Classification ──────────────

    [Theory]
    [InlineData("What is polymorphism in object-oriented programming and how does inheritance work?")]
    [InlineData("Explain the difference between an abstract class and an interface in OOP design patterns?")]
    [InlineData("How does garbage collection and memory management work in .NET C#?")]
    public void Classify_TechnicalQuestions_ReturnsTechnical(string question)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(QuestionCategory.Technical, result.Category);
        Assert.True(result.ConfidenceScore > 0, "ConfidenceScore should be > 0 for Technical questions.");
    }

    [Theory]
    [InlineData("Tell me about a time you dealt with a difficult coworker?")]
    [InlineData("Describe a situation where you showed leadership in your team?")]
    [InlineData("What is your greatest weakness and how do you handle it?")]
    public void Classify_BehavioralQuestions_ReturnsBehavioral(string question)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(QuestionCategory.Behavioral, result.Category);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Theory]
    [InlineData("What would you do if your project deadline was suddenly moved up by two weeks?")]
    [InlineData("How would you handle a situation where your manager disagrees with your approach?")]
    public void Classify_SituationalQuestions_ReturnsSituational(string question)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(QuestionCategory.Situational, result.Category);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Theory]
    [InlineData("Why do you want to work at this company?")]
    [InlineData("Where do you see yourself in 5 years?")]
    public void Classify_GeneralQuestions_ReturnsGeneral(string question)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(QuestionCategory.General, result.Category);
        Assert.True(result.ConfidenceScore > 0);
    }

    // ────────────── Edge Cases ──────────────

    [Fact]
    public void Classify_EmptyString_ReturnsUnknown()
    {
        var result = _classifier.Classify("");

        Assert.Equal(QuestionCategory.Unknown, result.Category);
        Assert.Equal(DifficultyLevel.Unknown, result.Difficulty);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public void Classify_NullString_ReturnsUnknown()
    {
        var result = _classifier.Classify(null!);

        Assert.Equal(QuestionCategory.Unknown, result.Category);
        Assert.Equal(DifficultyLevel.Unknown, result.Difficulty);
    }

    [Fact]
    public void Classify_VeryShortText_ReturnsUnknownOrLowConfidence()
    {
        var result = _classifier.Classify("hi?");

        // Con tan poco texto, no debería alcanzar el umbral mínimo de 2
        Assert.True(
            result.Category == QuestionCategory.Unknown || result.ConfidenceScore < 0.3,
            "Very short text should produce Unknown or very low confidence.");
    }

    // ────────────── Subcategory ──────────────

    [Fact]
    public void Classify_AlgorithmsQuestion_ReturnsAlgorithmsSubcategory()
    {
        var result = _classifier.Classify(
            "Can you explain the time complexity of quicksort algorithm and how binary search works?");

        Assert.Equal(QuestionCategory.Technical, result.Category);
        Assert.NotNull(result.Subcategory);
        Assert.Equal("Algoritmos y Estructuras de Datos", result.Subcategory);
    }

    [Fact]
    public void Classify_DatabaseQuestion_ReturnsDatabasesSubcategory()
    {
        var result = _classifier.Classify(
            "What is the difference between SQL joins and subqueries? How does indexing improve database query performance?");

        Assert.Equal(QuestionCategory.Technical, result.Category);
        Assert.NotNull(result.Subcategory);
        Assert.Equal("Bases de Datos", result.Subcategory);
    }

    // ────────────── Difficulty ──────────────

    [Theory]
    [InlineData("What are the basic concepts of HTML and CSS?", DifficultyLevel.Junior)]
    [InlineData("Explain the fundamentals of version control with Git?", DifficultyLevel.Junior)]
    public void Classify_JuniorIndicators_ReturnsJuniorDifficulty(string question, DifficultyLevel expected)
    {
        var result = _classifier.Classify(question);

        Assert.Equal(expected, result.Difficulty);
    }

    [Fact]
    public void Classify_SeniorIndicators_ReturnsSeniorDifficulty()
    {
        var result = _classifier.Classify(
            "How would you architect a distributed microservices system with scalability, fault tolerance, and system design best practices?");

        Assert.Equal(DifficultyLevel.Senior, result.Difficulty);
    }

    // ────────────── Tags ──────────────

    [Fact]
    public void Classify_TechnicalQuestion_ProducesTags()
    {
        var result = _classifier.Classify(
            "Explain how REST API endpoints work and why HTTP methods like GET, POST, PUT are important for web development?");

        Assert.Equal(QuestionCategory.Technical, result.Category);
        // Tags should be populated for technical questions with matching subcategory keywords
        Assert.NotNull(result.Tags);
    }

    // ────────────── Confidence Score ──────────────

    [Fact]
    public void Classify_StronglyTechnicalQuestion_HasHighConfidence()
    {
        var result = _classifier.Classify(
            "How does polymorphism work in object-oriented programming, specifically with abstract classes, interfaces, and design patterns like the factory pattern?");

        Assert.Equal(QuestionCategory.Technical, result.Category);
        Assert.True(result.ConfidenceScore >= 0.5, $"Expected high confidence but got {result.ConfidenceScore}");
    }
}
